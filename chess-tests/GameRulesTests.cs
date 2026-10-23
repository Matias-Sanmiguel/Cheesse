
using Chess.Core.Model;
using Chess.Core.Services;
using NUnit.Framework;
using static Chess.Tests.GameTestSupport;

namespace Chess.Tests;

public class GameRulesTests
{
    [Test]
    public void Check_when_king_is_attacked_but_can_escape()
    {
        // Rey blanco en e1.
        // Torre negra en e8: da jaque.
        // Rey negro en a8.
        // El rey blanco puede escapar.

        var board = Board.Empty()
            .WithPiece(
                new Square(4, 0),
                new Piece(PieceType.King, PlayerColor.White))
            .WithPiece(
                new Square(4, 7),
                new Piece(PieceType.Rook, PlayerColor.Black))
            .WithPiece(
                new Square(0, 7),
                new Piece(PieceType.King, PlayerColor.Black));

        var result = GameRules.Evaluate(
            board,
            PlayerColor.White);

        Assert.That(result, Is.EqualTo(GameStatus.Check));
    }

    [Test]
    public void Ongoing_when_king_is_safe_and_has_legal_moves()
    {
        // Dos reyes separados y una torre blanca.
        // El blanco tiene movimientos legales y no esta en jaque.

        var board = Board.Empty()
            .WithPiece(
                new Square(4, 0),
                new Piece(PieceType.King, PlayerColor.White))
            .WithPiece(
                new Square(0, 0),
                new Piece(PieceType.Rook, PlayerColor.White))
            .WithPiece(
                new Square(4, 7),
                new Piece(PieceType.King, PlayerColor.Black));

        var result = GameRules.Evaluate(
            board,
            PlayerColor.White);

        Assert.That(result, Is.EqualTo(GameStatus.Ongoing));
    }

    [TestCase("7k/8/8/8/8/8/8/7K w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/2B4K w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/2N4K w - - 0 1")]
    [TestCase("5b1k/8/8/8/8/8/8/2B4K w - - 0 1")]
    [TestCase("5b1k/8/8/8/8/4B3/8/2B4K w - - 0 1")]
    public void Insufficient_material_closes_the_game(string fen)
    {
        var game = Create(fen);

        Assert.That(game.Status, Is.EqualTo(GameStatus.DrawByInsufficientMaterial));
        Assert.That(GameRules.Evaluate(game.Board, game.Turn), Is.EqualTo(game.Status));
        Assert.That(game.LegalMoves(), Is.Empty);
    }

    [TestCase("7k/8/8/8/8/8/8/1NN4K w - - 0 1")]
    [TestCase("5n1k/8/8/8/8/8/8/2N4K w - - 0 1")]
    [TestCase("4b2k/8/8/8/8/8/8/2B4K w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/1NB4K w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/P7/7K w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/R6K w - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/Q6K w - - 0 1")]
    public void Material_that_can_participate_in_mate_is_not_automatically_drawn(string fen)
    {
        Assert.That(Create(fen).Status, Is.Not.EqualTo(GameStatus.DrawByInsufficientMaterial));
    }

    [Test]
    public void Hundred_halfmoves_close_draw_and_undo_restores_the_previous_state()
    {
        var game = Create("7k/8/8/8/8/8/8/R6K w - - 99 50");
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Play(game, "a1a2");

        Assert.That(game.Board.HalfmoveClock, Is.EqualTo(100));
        Assert.That(game.Status, Is.EqualTo(GameStatus.DrawByFiftyMove));
        Assert.That(game.TryMove(Move("h8", "g8")), Is.False);
        Assert.That(game.LegalMoves(), Is.Empty);
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Board.HalfmoveClock, Is.EqualTo(99));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    [TestCase("7k/8/8/8/8/8/P7/7K w - - 99 50", "a2a3")]
    [TestCase("r6k/8/8/8/8/8/8/R6K w - - 99 50", "a1a8")]
    [TestCase("4k3/8/8/3pP3/8/8/8/4K3 w - d6 99 50", "e5d6")]
    public void Pawn_moves_and_captures_reset_halfmove_counter(string fen, string move)
    {
        var game = Create(fen);
        Play(game, move);

        Assert.That(game.Board.HalfmoveClock, Is.Zero);
        Assert.That(game.Status, Is.Not.EqualTo(GameStatus.DrawByFiftyMove));
    }

    [Test]
    public void Mate_takes_precedence_over_fifty_move_draw()
    {
        var game = Create("7k/8/5KQ1/8/8/8/8/8 w - - 99 50");
        Play(game, "g6g7");

        Assert.That(game.Board.HalfmoveClock, Is.EqualTo(100));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Checkmate));
    }

    [Test]
    public void Initial_position_counts_toward_threefold_and_counters_do_not_distinguish_positions()
    {
        var game = Create();
        Play(game, "g1f3", "g8f6", "f3g1", "f6g8");
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Play(game, "g1f3", "g8f6", "f3g1", "f6g8");

        Assert.That(game.Board.HalfmoveClock, Is.EqualTo(8));
        Assert.That(game.Status, Is.EqualTo(GameStatus.DrawByRepetition));
        Assert.That(game.TryMove(Move("e2", "e4")), Is.False);

        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Play(game, "f6h5");
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    [Test]
    public void Lost_castling_rights_distinguish_an_otherwise_identical_position()
    {
        var game = Create("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        for (var cycle = 0; cycle < 2; cycle++)
            Play(game, "e1f1", "e8f8", "f1e1", "f8e8");

        Assert.That(game.Board.Castling, Is.EqualTo(CastlingRights.None));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    [TestCase("4k1n1/8/8/3pP3/8/8/8/4K1N1 w - d6 0 1", GameStatus.Ongoing)]
    [TestCase("4k1n1/8/8/3p4/8/8/8/4K1N1 w - d6 0 1", GameStatus.DrawByRepetition)]
    [TestCase("k3r1n1/8/8/3pP3/8/8/8/4K1N1 w - d6 0 1", GameStatus.DrawByRepetition)]
    public void Only_a_legal_en_passant_capture_distinguishes_repetition_positions(
        string fen, GameStatus expected)
    {
        var game = Create(fen);
        for (var cycle = 0; cycle < 2; cycle++)
            Play(game, "g1f3", "g8f6", "f3g1", "f6g8");

        Assert.That(game.Status, Is.EqualTo(expected));
    }
}
