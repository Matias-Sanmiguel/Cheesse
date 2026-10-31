using Chess.Core.Model;
using Chess.Core.Services;
using NUnit.Framework;
using static Chess.Tests.GameTestSupport;

namespace Chess.Tests;

public class MementoTests
{
    [Test]
    public void Restore_reuses_immutable_snapshot_and_rebuilds_shared_undo_without_replaying_events()
    {
        var game = Create("r3k2r/3p4/8/4P3/8/8/8/R3K2R b KQkq - 37 42");
        Play(game, "d7d5");
        var snapshot = game.CreateMemento();
        var caretaker = new GameCaretaker(game);
        Play(game, "e5d6", "e8g8");
        Assert.That(snapshot.History, Has.Count.EqualTo(1));
        Assert.Throws<NotSupportedException>(() => ((IList<Move>)snapshot.History).Clear());
        game.Restore(snapshot);
        Assert.That(game.Board, Is.SameAs(snapshot.Board));
        Assert.That(game.Turn, Is.EqualTo(PlayerColor.White));
        Assert.That(game.Board.EnPassant, Is.EqualTo(Square("d6")));
        Assert.That(caretaker.CanRedo, Is.False);
        Assert.That(caretaker.Undo(), Is.True);
        Assert.That(game.Redo(), Is.True);
        Assert.That(game.Board.EnPassant, Is.EqualTo(Square("d6")));
        Play(game, "e5d6");
        Assert.That(caretaker.Undo(), Is.True);
        Assert.That(caretaker.Undo(), Is.True);
        Assert.That(caretaker.Undo(), Is.False);
    }

    [TestCase("7k/8/5KQ1/8/8/8/8/8 b - - 0 1", false)]
    [TestCase("7k/6Q1/5K2/8/8/8/8/8 b - - 0 1", false)]
    [TestCase("7k/8/8/8/8/8/8/7K w - - 0 1", false)]
    [TestCase("7k/8/8/8/8/8/8/R6K w - - 100 50", false)]
    [TestCase(FenSerializer.InitialPosition, true)]
    [TestCase("4r2k/8/8/8/8/8/8/4K3 w - - 0 1", true)]
    public void State_accepts_moves_only_in_active_positions(string fen, bool acceptsMoves)
    {
        var game = Create(fen);
        Assert.That(game.State.AcceptsMoves, Is.EqualTo(acceptsMoves));
        Assert.That(game.LegalMoves().Count > 0, Is.EqualTo(acceptsMoves));
    }
}
