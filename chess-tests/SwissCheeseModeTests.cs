using Chess.Core.Model;
using Chess.Core.Rules;
using Chess.Core.Services;
using NUnit.Framework;
using static Chess.Tests.GameTestSupport;

namespace Chess.Tests;

[TestFixture]
public class SwissCheeseModeTests
{
    // Explicit legacy pattern for low-level geometry tests; the actual game
    // constructor always generates a fresh, validated layout.
    private static ChessGame Swiss(string fen = FenSerializer.InitialPosition)
    {
        var ports = Create();
        return new ChessGame(ports.Events, ports.Repository, ports.Clock,
            FenSerializer.Load(fen).WithSwissHoles(SwissCheeseRules.LegacyHoles));
    }

    [Test]
    public void New_matches_get_random_nonrepeating_balanced_holes_with_playable_openings()
    {
        string? previous = null;
        var seen = new HashSet<string>();
        for (int attempt = 0; attempt < 30; attempt++)
        {
            var game = Create(GameMode.QuesoSuizo);
            var board = game.Board;
            var holes = board.SwissHoles;
            var pattern = SwissCheeseRules.Format(holes);
            Assert.That(holes, Has.Count.EqualTo(6));
            Assert.That(holes.Distinct().Count(), Is.EqualTo(6));
            Assert.That(holes.All(hole => hole.Rank is >= 2 and <= 5 &&
                board.At(hole) is null && board.IsBlocked(hole)), Is.True);
            Assert.That(holes.All(hole =>
                holes.Contains(new Square(7 - hole.File, 7 - hole.Rank))), Is.True,
                "The board must remain rotationally symmetric for fairness.");
            Assert.That(holes.Select(hole => hole.File).Distinct().Count(), Is.LessThanOrEqualTo(4),
                "At least four pawn files must remain fully open.");
            Assert.That(holes.Count(hole => hole.Rank == 2), Is.LessThanOrEqualTo(2));
            Assert.That(holes.Count(hole => hole.Rank == 5), Is.LessThanOrEqualTo(2));
            Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));

            foreach (var side in new[] { PlayerColor.White, PlayerColor.Black })
            {
                var from = board.WithState(side, 0, 1);
                var legal = MoveGenerator.Legal(from, side);
                Assert.That(legal.Count, Is.GreaterThanOrEqualTo(14), side.ToString());
                Assert.That(legal.Where(move => from.At(move.From)?.Type == PieceType.Pawn)
                    .Select(move => move.From.File).Distinct().Count(),
                    Is.GreaterThanOrEqualTo(6), "Each side needs several usable pawn openings.");
                int home = side == PlayerColor.White ? 0 : 7;
                Assert.That(legal.Any(move => move.From == new Square(1, home)), Is.True);
                Assert.That(legal.Any(move => move.From == new Square(6, home)), Is.True);
            }

            if (previous is not null)
                Assert.That(pattern, Is.Not.EqualTo(previous),
                    "Consecutive new matches cannot reuse the same hole pattern.");
            previous = pattern;
            seen.Add(pattern);
        }
        Assert.That(seen.Count, Is.GreaterThan(2), "New Swiss matches must visibly vary.");
    }

    [Test]
    public void Layout_is_part_of_board_state_and_never_rerolls_on_moves_or_undo()
    {
        var game = Create(GameMode.QuesoSuizo);
        string startingPattern = SwissCheeseRules.Format(game.Board.SwissHoles);
        var move = game.LegalMoves().First();
        Assert.That(game.TryMove(move), Is.True);
        Assert.That(SwissCheeseRules.Format(game.Board.SwissHoles), Is.EqualTo(startingPattern));
        Assert.That(game.Undo(), Is.True);
        Assert.That(SwissCheeseRules.Format(game.Board.SwissHoles), Is.EqualTo(startingPattern));
        Assert.That(game.Redo(), Is.True);
        Assert.That(SwissCheeseRules.Format(game.Board.SwissHoles), Is.EqualTo(startingPattern));
        Assert.That(game.Board.WithMode(GameMode.QuesoSuizo).SwissHoles, Is.EqualTo(game.Board.SwissHoles));
        Assert.That(game.Board.WithMode(GameMode.Standard).SwissHoles, Is.Empty);
    }

    [Test]
    public void Classic_has_no_holes_and_fixed_test_layout_uses_both_colors()
    {
        var game = Swiss();
        Assert.That(game.Board.SwissHoles,
            Is.EquivalentTo(SwissCheeseRules.LegacyHoles));
        foreach (var hole in SwissCheeseRules.LegacyHoles)
        {
            Assert.That(game.Board.IsBlocked(hole), Is.True, hole.ToString());
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
        Assert.That(white, Does.Not.Contain(Move("c2", "c4")));
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
        Assert.That(black, Does.Not.Contain(Move("e7", "e5")));
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
        var swiss = standard.WithSwissHoles(SwissCheeseRules.LegacyHoles);
        Assert.That(CheckDetector.IsInCheck(standard, PlayerColor.White), Is.True);
        Assert.That(CheckDetector.IsInCheck(swiss, PlayerColor.White), Is.False);
        Assert.That(AttackDetector.IsAttacked(swiss, Square("d4"), PlayerColor.Black), Is.False);
    }

    [Test]
    public void King_moves_and_en_passant_cannot_use_hole_destinations()
    {
        var kings = FenSerializer.Load("7k/8/8/8/8/4K3/8/8 w - - 0 1")
            .WithSwissHoles(SwissCheeseRules.LegacyHoles);
        var kingMoves = MoveGenerator.Legal(kings, PlayerColor.White);
        Assert.That(kingMoves, Does.Not.Contain(Move("e3", "d4")));
        Assert.That(kingMoves, Does.Contain(Move("e3", "e4")));

        var pawnPosition = FenSerializer.Load("7k/8/8/1Pp5/8/8/8/K7 w - c6 0 1");
        Assert.That(SpecialMoves.EnPassant(pawnPosition, PlayerColor.White),
            Does.Contain(Move("b5", "c6")));
        var swiss = pawnPosition.WithSwissHoles(SwissCheeseRules.LegacyHoles);
        Assert.That(SpecialMoves.EnPassant(swiss, PlayerColor.White),
            Does.Not.Contain(Move("b5", "c6")));
    }

    [Test]
    public void Invalid_or_occupied_hole_patterns_are_rejected()
    {
        var initial = FenSerializer.Load("7k/8/8/8/8/2N5/8/K7 w - - 0 1");
        Assert.Throws<ArgumentException>(() =>
            initial.WithSwissHoles(SwissCheeseRules.LegacyHoles));
        var swiss = Swiss();
        Assert.Throws<InvalidOperationException>(() =>
            swiss.Board.WithPiece(Square("c3"), new Piece(PieceType.Knight, PlayerColor.White)));
        Assert.Throws<ArgumentException>(() =>
            FenSerializer.Load(FenSerializer.InitialPosition).WithSwissHoles(
                new[] { Square("a1"), Square("h8"), Square("c3"), Square("f6"), Square("d4"), Square("e5") }));
        Assert.Throws<ArgumentException>(() =>
            FenSerializer.Load(FenSerializer.InitialPosition).WithSwissHoles(
                Enumerable.Repeat(Square("c3"), 6)));
    }

    [Test]
    public void Undo_redo_and_memento_preserve_the_exact_layout()
    {
        var game = Swiss();
        Play(game, "e2e4", "e7e6", "g1h3");
        var saved = game.CreateMemento();
        var originalHoles = SwissCheeseRules.Format(game.Board.SwissHoles);
        var finalFen = FenSerializer.Write(game.Board);
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Mode, Is.EqualTo(GameMode.QuesoSuizo));
        Assert.That(SwissCheeseRules.Format(game.Board.SwissHoles), Is.EqualTo(originalHoles));
        Assert.That(game.Redo(), Is.True);
        Assert.That(FenSerializer.Write(game.Board), Is.EqualTo(finalFen));

        var restored = Create();
        restored.Restore(saved);
        Assert.That(restored.Mode, Is.EqualTo(GameMode.QuesoSuizo));
        Assert.That(SwissCheeseRules.Format(restored.Board.SwissHoles), Is.EqualTo(originalHoles));
        Assert.That(restored.History.Count, Is.EqualTo(3));
    }

    [Test]
    public void Swiss_pgn_export_and_replay_preserve_randomized_hole_configuration()
    {
        var game = Create(GameMode.QuesoSuizo);
        var initialPattern = SwissCheeseRules.Format(game.InitialBoard.SwissHoles);
        var move = game.LegalMoves().First();
        Assert.That(game.TryMove(move), Is.True);
        var pgn = SanNotation.ToPgn(game);
        Assert.That(pgn, Does.Contain("[Variant \"Queso Suizo\"]"));
        Assert.That(pgn, Does.Contain("[SwissHoles \"" + initialPattern + "\"]"));
        var moves = SanNotation.FromPgn(pgn, out var initial, GameMode.QuesoSuizo);
        Assert.That(SwissCheeseRules.Format(initial.SwissHoles), Is.EqualTo(initialPattern));
        Assert.That(moves, Is.EqualTo(game.History));
        Assert.Throws<FormatException>(() => SanNotation.FromPgn(
            pgn.Replace("Queso Suizo", "Standard"), out _, GameMode.QuesoSuizo));
        Assert.Throws<FormatException>(() => SanNotation.FromPgn(
            pgn.Replace(initialPattern, "a1,b2,c3,d4,e5,f6"), out _));
    }

    [Test]
    public void Old_swiss_pgn_without_layout_tag_uses_its_legacy_six_holes()
    {
        var source = Swiss();
        Play(source, "e2e4");
        var pgn = SanNotation.ToPgn(source);
        var legacy = string.Join("\n", pgn.Split('\n')
            .Where(line => !line.StartsWith("[SwissHoles ", StringComparison.Ordinal)));
        var moves = SanNotation.FromPgn(legacy, out var restored, GameMode.QuesoSuizo);
        Assert.That(moves, Is.EqualTo(source.History));
        Assert.That(restored.SwissHoles, Is.EquivalentTo(SwissCheeseRules.LegacyHoles));
    }
}
