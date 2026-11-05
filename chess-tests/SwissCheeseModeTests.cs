using Chess.Core.Model;
using Chess.Core.Rules;
using Chess.Core.Services;
using NUnit.Framework;
using static Chess.Tests.GameTestSupport;

namespace Chess.Tests;

[TestFixture]
public class SwissCheeseModeTests
{
    private static ChessGame Swiss(string fen = FenSerializer.InitialPosition) =>
        Create(GameMode.QuesoSuizo, fen);

    [Test]
    public void Holes_are_symmetric_empty_and_only_active_in_swiss_mode()
    {
        var game = Swiss();
        Assert.That(SwissCheeseRules.Holes.Select(square => square.ToString()),
            Is.EquivalentTo(new[] { "c3", "f3", "d4", "e5", "c6", "f6" }));
        foreach (var hole in SwissCheeseRules.Holes)
        {
            Assert.That(game.Board.IsBlocked(hole), Is.True, hole.ToString());
            Assert.That(game.Board.At(hole), Is.Null, hole.ToString());
            Assert.That(Create().Board.IsBlocked(hole), Is.False, "Classic has no holes.");
        }
        Assert.That(game.Board.IsBlocked(Square("e4")), Is.False);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    [Test]
    public void Pawns_cannot_enter_or_cross_holes_but_other_openings_remain_legal()
    {
        var game = Swiss();
        var white = game.LegalMoves();
        Assert.That(white, Does.Not.Contain(Move("c2", "c3")));
        Assert.That(white, Does.Not.Contain(Move("c2", "c4")), "A two-square pawn move cannot cross c3.");
        Assert.That(white, Does.Not.Contain(Move("f2", "f3")));
        Assert.That(white, Does.Not.Contain(Move("f2", "f4")));
        Assert.That(white, Does.Not.Contain(Move("g1", "f3")));
        Assert.That(white, Does.Not.Contain(Move("b1", "c3")));
        Assert.That(white, Does.Contain(Move("g1", "h3")));
        Assert.That(white, Does.Contain(Move("b1", "d2")));
        Assert.That(white, Does.Contain(Move("e2", "e4")));
        var before = game.Board;
        Assert.That(game.TryMove(Move("c2", "c4")), Is.False);
        Assert.That(game.Board, Is.SameAs(before));
        Assert.That(game.TryMove(Move("g1", "h3")), Is.True);

        var blackTurn = Swiss().Board.WithState(PlayerColor.Black, 0, 1);
        var black = MoveGenerator.Legal(blackTurn, PlayerColor.Black);
        Assert.That(black, Does.Not.Contain(Move("c7", "c5")));
        Assert.That(black, Does.Not.Contain(Move("f7", "f5")));
        Assert.That(black, Does.Not.Contain(Move("e7", "e5")), "e5 is a hole.");
        Assert.That(black, Does.Contain(Move("e7", "e6")));
    }

    [Test]
    public void Sliding_pieces_stop_at_the_first_hole()
    {
        var rook = Swiss("7k/8/8/8/8/8/8/K1R5 w - - 0 1");
        Assert.That(rook.LegalMoves(), Does.Contain(Move("c1", "c2")));
        Assert.That(rook.LegalMoves(), Does.Not.Contain(Move("c1", "c3")));
        Assert.That(rook.LegalMoves(), Does.Not.Contain(Move("c1", "c4")));
        Assert.Throws<InvalidOperationException>(() => rook.Board.WithMove(Move("c1", "c3")));

        var bishop = Swiss("7k/8/8/8/8/8/8/B6K w - - 0 1");
        Assert.That(bishop.LegalMoves(), Does.Contain(Move("a1", "b2")));
        Assert.That(bishop.LegalMoves(), Does.Not.Contain(Move("a1", "c3")));
        Assert.That(bishop.LegalMoves(), Does.Not.Contain(Move("a1", "d4")));
    }

    [Test]
    public void A_hole_blocks_check_along_a_rook_line()
    {
        const string fen = "k7/8/8/8/r6K/8/8/8 w - - 0 1";
        var standard = FenSerializer.Load(fen);
        var swiss = standard.WithMode(GameMode.QuesoSuizo);
        Assert.That(CheckDetector.IsInCheck(standard, PlayerColor.White), Is.True);
        Assert.That(CheckDetector.IsInCheck(swiss, PlayerColor.White), Is.False);
        Assert.That(AttackDetector.IsAttacked(swiss, Square("d4"), PlayerColor.Black), Is.False);
    }

    [Test]
    public void Cannot_put_pieces_inside_holes()
    {
        var initial = FenSerializer.Load("7k/8/8/8/8/2N5/8/K7 w - - 0 1");
        Assert.Throws<ArgumentException>(() => initial.WithMode(GameMode.QuesoSuizo));
        var swiss = Swiss();
        Assert.Throws<InvalidOperationException>(() =>
            swiss.Board.WithPiece(Square("c3"), new Piece(PieceType.Knight, PlayerColor.White)));
    }

    [Test]
    public void Undo_redo_and_memento_preserve_the_swiss_board()
    {
        var game = Swiss();
        Play(game, "e2e4", "e7e6", "g1h3");
        var saved = game.CreateMemento();
        var finalFen = FenSerializer.Write(game.Board);
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Mode, Is.EqualTo(GameMode.QuesoSuizo));
        Assert.That(game.Board.IsBlocked(Square("c3")), Is.True);
        Assert.That(game.Redo(), Is.True);
        Assert.That(FenSerializer.Write(game.Board), Is.EqualTo(finalFen));

        var restored = Create();
        restored.Restore(saved);
        Assert.That(restored.Mode, Is.EqualTo(GameMode.QuesoSuizo));
        Assert.That(restored.Board.IsBlocked(Square("f6")), Is.True);
        Assert.That(restored.History.Count, Is.EqualTo(3));
    }

    [Test]
    public void Swiss_pgn_export_and_replay_preserve_mode_and_move_legality()
    {
        var game = Swiss();
        Play(game, "e2e4", "e7e6", "g1h3");
        var pgn = SanNotation.ToPgn(game);
        Assert.That(pgn, Does.Contain("[Variant \"Queso Suizo\"]"));
        var moves = SanNotation.FromPgn(pgn, out var initial, GameMode.QuesoSuizo);
        Assert.That(initial.Mode, Is.EqualTo(GameMode.QuesoSuizo));
        Assert.That(moves, Is.EqualTo(game.History));
        Assert.Throws<FormatException>(() => SanNotation.FromPgn(
            pgn.Replace("Queso Suizo", "Standard"), out _, GameMode.QuesoSuizo));
    }
}
