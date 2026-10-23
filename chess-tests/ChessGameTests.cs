using Chess.Core.Model;
using Chess.Core.Services;
using NUnit.Framework;
using static Chess.Tests.GameTestSupport;

namespace Chess.Tests;

public class ChessGameTests
{
    [Test]
    public void Default_constructor_starts_a_standard_game()
    {
        var dependencies = Create();
        var game = new ChessGame(dependencies.Events, dependencies.Repository, dependencies.Clock);

        Assert.That(FenSerializer.Write(game.Board), Is.EqualTo(FenSerializer.InitialPosition));
        Assert.That(game.LegalMoves(), Has.Count.EqualTo(20));
        Assert.That(game.History, Is.Empty);
        Assert.That(game.Undo(), Is.False);
    }

    [Test]
    public void Illegal_or_wrong_color_move_does_not_change_game()
    {
        var game = Create();
        var board = game.Board;

        Assert.That(game.TryMove(Move("e2", "e5")), Is.False);
        Assert.That(game.TryMove(Move("e7", "e5")), Is.False);
        Assert.That(game.Board, Is.SameAs(board));
        Assert.That(game.History, Is.Empty);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.Undo(), Is.False);
    }

    [TestCase("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 12 23", "e1", "g1")]
    [TestCase("r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 12 23", "e8", "c8")]
    [TestCase("4k3/8/8/3pP3/8/8/8/4K3 w - d6 27 42", "e5", "d6")]
    [TestCase("4k3/8/8/8/3Pp3/8/8/4K3 b - d3 27 42", "e4", "d3")]
    [TestCase("r3k3/8/8/8/8/8/8/R3K3 w q - 39 5", "a1", "a8")]
    [TestCase("7k/8/8/8/8/8/8/R6K w - - 99 50", "a1", "a2")]
    public void Undo_restores_exact_board_turn_counters_rights_target_status_and_history(
        string fen, string from, string to)
    {
        var game = Create(fen);
        var initialBoard = game.Board;
        var initialStatus = game.Status;
        var move = Move(from, to);
        Assert.That(game.TryMove(move), Is.True);
        Assert.That(game.History, Is.EqualTo(new[] { move }));
        Assert.That(game.Undo(), Is.True);

        Assert.That(game.Board, Is.SameAs(initialBoard));
        Assert.That(FenSerializer.Write(game.Board), Is.EqualTo(fen));
        Assert.That(game.Status, Is.EqualTo(initialStatus));
        Assert.That(game.History, Is.Empty);
        Assert.That(game.Undo(), Is.False);
        Assert.That(game.TryMove(move), Is.True);
    }

    [TestCase(PieceType.Queen)]
    [TestCase(PieceType.Rook)]
    [TestCase(PieceType.Bishop)]
    [TestCase(PieceType.Knight)]
    public void Undo_promotion_restores_pawn_and_can_choose_another_promotion(PieceType promotion)
    {
        const string fen = "7k/P7/8/8/8/8/8/7K w - - 8 11";
        var game = Create(fen);
        Assert.That(game.TryMove(Move("a7", "a8", promotion)), Is.True);
        Assert.That(game.Undo(), Is.True);
        Assert.That(FenSerializer.Write(game.Board), Is.EqualTo(fen));
        Assert.That(game.TryMove(Move("a7", "a8", PieceType.Queen)), Is.True);
        Assert.That(game.Board.At(Square("a8"))?.Type, Is.EqualTo(PieceType.Queen));
    }

    [Test]
    public void Undo_mate_reopens_game_and_can_take_another_branch()
    {
        var game = Create();
        Play(game, "f2f3", "e7e5", "g2g4", "d8h4");
        Assert.That(game.Status, Is.EqualTo(GameStatus.Checkmate));
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Play(game, "b8c6");

        Assert.That(game.History, Has.Count.EqualTo(4));
        Assert.That(SanNotation.ToPgn(game), Does.Not.Contain("Qh4"));
        while (game.Undo()) { }
        Assert.That(FenSerializer.Write(game.Board), Is.EqualTo(FenSerializer.InitialPosition));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    [Test]
    public void Undo_discards_repetition_from_abandoned_branch()
    {
        var game = Create();
        for (var cycle = 0; cycle < 2; cycle++)
            Play(game, "g1f3", "g8f6", "f3g1", "f6g8");
        Assert.That(game.Status, Is.EqualTo(GameStatus.DrawByRepetition));

        for (var ply = 0; ply < 4; ply++)
            Assert.That(game.Undo(), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Play(game, "g1f3", "g8f6", "f3g1");
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Play(game, "f6g8");
        Assert.That(game.Status, Is.EqualTo(GameStatus.DrawByRepetition));
    }

    [Test]
    public void Public_history_cannot_be_mutated_by_callers()
    {
        var game = Create();
        Play(game, "e2e4");

        Assert.Throws<NotSupportedException>(() => ((IList<Move>)game.History).Clear());
        Assert.That(game.History, Has.Count.EqualTo(1));
    }
}
