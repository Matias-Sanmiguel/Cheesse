using Chess.Core.Model;
using Chess.Core.Services;
using NUnit.Framework;
using static Chess.Tests.GameTestSupport;

namespace Chess.Tests;

public class GameCaretakerTests
{
    [Test]
    public void Direct_commands_and_facade_share_one_undo_redo_history()
    {
        var game = Create();
        Assert.That(game.TryMove(Move("e2", "e4")), Is.True);
        ICommand command = new PlayMoveCommand(game, Move("e7", "e5"));
        Assert.That(command.Execute(), Is.True);
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.History, Is.Empty);
        Assert.That(game.Redo(), Is.True);
        Assert.That(game.Redo(), Is.True);
        Assert.That(command.Undo(), Is.True);
        Assert.That(game.History, Has.Count.EqualTo(1));
    }

    [Test]
    public void Empty_history_disables_undo_and_redo()
    {
        var caretaker = new GameCaretaker(Create());
        Assert.That(caretaker.CanUndo, Is.False);
        Assert.That(caretaker.CanRedo, Is.False);
        Assert.That(caretaker.Undo(), Is.False);
        Assert.That(caretaker.Redo(), Is.False);
    }

    [Test]
    public void Commands_execute_once_in_order_and_undo_their_own_move()
    {
        var game = Create();
        var first = new PlayMoveCommand(game, Move("e2", "e4"));
        var other = new PlayMoveCommand(game, Move("d2", "d4"));

        Assert.That(first.Execute(), Is.True);
        Assert.That(first.Execute(), Is.False);
        Assert.That(other.Execute(), Is.False);
        Assert.That(other.Undo(), Is.False);
        Assert.That(first.Undo(), Is.True);
        Assert.That(first.Undo(), Is.False);
        Assert.That(first.Execute(), Is.True);
        Assert.That(game.History, Is.EqualTo(new[] { Move("e2", "e4") }));
    }

    [TestCase(FenSerializer.InitialPosition, "e2", "e4", null)]
    [TestCase("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 12 23", "e1", "g1", null)]
    [TestCase("r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 12 23", "e8", "c8", null)]
    [TestCase("4k3/8/8/3pP3/8/8/8/4K3 w - d6 27 42", "e5", "d6", null)]
    [TestCase("4k3/8/8/8/3Pp3/8/8/4K3 b - d3 27 42", "e4", "d3", null)]
    [TestCase("r3k3/8/8/8/8/8/8/R3K3 w q - 39 5", "a1", "a8", null)]
    [TestCase("7k/P7/8/8/8/8/8/7K w - - 8 11", "a7", "a8", PieceType.Queen)]
    [TestCase("7k/P7/8/8/8/8/8/7K w - - 8 11", "a7", "a8", PieceType.Knight)]
    [TestCase("7k/8/8/8/8/8/8/R6K w - - 99 50", "a1", "a2", null)]
    public void Undo_redo_restore_exact_position_and_status(
        string fen, string from, string to, PieceType? promotion)
    {
        var game = Create(fen);
        var caretaker = new GameCaretaker(game);
        var initialStatus = game.Status;
        var move = Move(from, to, promotion);
        Assert.That(caretaker.Execute(move), Is.True);
        var movedFen = FenSerializer.Write(game.Board);
        var movedStatus = game.Status;
        Assert.That(caretaker.CanUndo, Is.True);
        Assert.That(caretaker.CanRedo, Is.False);

        Assert.That(caretaker.Undo(), Is.True);
        Assert.That(FenSerializer.Write(game.Board), Is.EqualTo(fen));
        Assert.That(game.Status, Is.EqualTo(initialStatus));
        Assert.That(game.History, Is.Empty);
        Assert.That(caretaker.CanUndo, Is.False);
        Assert.That(caretaker.CanRedo, Is.True);

        Assert.That(caretaker.Redo(), Is.True);
        Assert.That(FenSerializer.Write(game.Board), Is.EqualTo(movedFen));
        Assert.That(game.Status, Is.EqualTo(movedStatus));
        Assert.That(game.History, Is.EqualTo(new[] { move }));
        Assert.That(caretaker.CanUndo, Is.True);
        Assert.That(caretaker.CanRedo, Is.False);
    }

    [Test]
    public void Loaded_history_is_available_to_undo_and_redo()
    {
        var game = Create();
        Play(game, "e2e4", "e7e5", "g1f3");
        var expected = FenSerializer.Write(game.Board);
        var caretaker = new GameCaretaker(game);

        for (var ply = 0; ply < 3; ply++) Assert.That(caretaker.Undo(), Is.True);
        Assert.That(FenSerializer.Write(game.Board), Is.EqualTo(FenSerializer.InitialPosition));
        for (var ply = 0; ply < 3; ply++) Assert.That(caretaker.Redo(), Is.True);
        Assert.That(FenSerializer.Write(game.Board), Is.EqualTo(expected));
        Assert.That(caretaker.Redo(), Is.False);
    }

    [Test]
    public void Successful_new_branch_discards_redo_but_invalid_moves_preserve_it()
    {
        var game = Create();
        var caretaker = new GameCaretaker(game);
        Assert.That(caretaker.Execute(Move("e2", "e4")), Is.True);
        Assert.That(caretaker.Undo(), Is.True);
        Assert.That(caretaker.Execute(Move("e2", "e5")), Is.False);
        Assert.That(caretaker.CanRedo, Is.True);
        Assert.That(caretaker.Execute(Move("d2", "d4")), Is.True);
        Assert.That(caretaker.CanRedo, Is.False);
        Assert.That(caretaker.Redo(), Is.False);
        Assert.That(game.Board.At(Square("d4"))?.Type, Is.EqualTo(PieceType.Pawn));
        Assert.That(game.Board.At(Square("e2"))?.Type, Is.EqualTo(PieceType.Pawn));
    }

    [Test]
    public void Undo_redo_reopens_and_recloses_checkmate()
    {
        var game = Create();
        Play(game, "f2f3", "e7e5", "g2g4", "d8h4");
        var caretaker = new GameCaretaker(game);
        Assert.That(caretaker.Undo(), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(caretaker.Redo(), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Checkmate));
    }

    [Test]
    public void Undo_redo_preserves_threefold_repetition()
    {
        var game = Create();
        for (var cycle = 0; cycle < 2; cycle++)
            Play(game, "g1f3", "g8f6", "f3g1", "f6g8");
        var caretaker = new GameCaretaker(game);
        Assert.That(caretaker.Undo(), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(caretaker.Redo(), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.DrawByRepetition));
    }
}
