using Chess.Core.Events;
using Chess.Core.Model;
using Chess.Core.Ports;
using NUnit.Framework;

namespace Chess.Tests;

public class ChessGameTests
{
    [Test]
    public void Redo_restores_undone_moves_and_new_move_clears_redo()
    {
        var game = CreateGame(new RecordingEvents());
        var first = new Move(new Square(4, 1), new Square(4, 3));
        var second = new Move(new Square(4, 6), new Square(4, 4));
        Assert.That(game.TryMove(first), Is.True);
        Assert.That(game.TryMove(second), Is.True);
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Redo(), Is.True);
        Assert.That(game.History, Is.EqualTo(new[] { first, second }));
        Assert.That(game.Turn, Is.EqualTo(PlayerColor.White));
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.TryMove(new Move(new Square(3, 6), new Square(3, 4))), Is.True);
        Assert.That(game.Redo(), Is.False);
    }

    [Test]
    public void Multiple_redos_replay_in_order_and_publish_move_events()
    {
        var events = new RecordingEvents();
        var game = CreateGame(events);
        var first = new Move(new Square(4, 1), new Square(4, 3));
        var second = new Move(new Square(4, 6), new Square(4, 4));
        Assert.That(game.TryMove(first), Is.True);
        Assert.That(game.TryMove(second), Is.True);
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Undo(), Is.True);
        events.Items.Clear();

        Assert.That(game.Redo(), Is.True);
        Assert.That(game.Redo(), Is.True);
        Assert.That(game.Redo(), Is.False);
        Assert.That(game.History, Is.EqualTo(new[] { first, second }));
        Assert.That(events.Items.OfType<PieceMoved>().Select(e => e.Move), Is.EqualTo(new[] { first, second }));
    }

    [Test]
    public void Initial_position_has_twenty_legal_moves()
    {
        var game = CreateGame(new RecordingEvents());

        Assert.That(game.LegalMoves(), Has.Count.EqualTo(20));
        Assert.That(game.Turn, Is.EqualTo(PlayerColor.White));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    [Test]
    public void Game_created_from_position_updates_status_and_publishes_check()
    {
        var events = new RecordingEvents();
        var board = Board.Empty()
            .WithPiece(new Square(4, 1), new Piece(PieceType.King, PlayerColor.White))
            .WithPiece(new Square(0, 0), new Piece(PieceType.Rook, PlayerColor.White))
            .WithPiece(new Square(7, 7), new Piece(PieceType.King, PlayerColor.Black));
        var game = new ChessGame(events, new UnusedRepository(), new FixedClock(), board, PlayerColor.White);
        var move = new Move(new Square(0, 0), new Square(7, 0));

        Assert.That(game.Board, Is.SameAs(board));
        Assert.That(game.Turn, Is.EqualTo(PlayerColor.White));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));

        Assert.That(game.TryMove(move), Is.True);

        Assert.That(game.Status, Is.EqualTo(GameStatus.Check));
        Assert.That(events.Items, Is.EqualTo(new object[]
        {
            new PieceMoved(move, new Piece(PieceType.Rook, PlayerColor.White)),
            new CheckRaised(PlayerColor.Black),
        }));
    }

    [Test]
    public void Legal_move_updates_board_turn_history_and_publishes_event()
    {
        var events = new RecordingEvents();
        var game = CreateGame(events);
        var before = game.Board;
        var move = new Move(new Square(4, 1), new Square(4, 3));
        var pawn = new Piece(PieceType.Pawn, PlayerColor.White);

        Assert.That(game.TryMove(move), Is.True);
        Assert.That(game.Board.At(move.From), Is.Null);
        Assert.That(game.Board.At(move.To), Is.EqualTo(pawn));
        Assert.That(before.At(move.From), Is.EqualTo(pawn));
        Assert.That(before.At(move.To), Is.Null);
        Assert.That(game.Turn, Is.EqualTo(PlayerColor.Black));
        Assert.That(game.History, Is.EqualTo(new[] { move }));
        Assert.That(events.Items, Is.EqualTo(new object[] { new PieceMoved(move, pawn) }));
    }

    [Test]
    public void Illegal_move_changes_nothing_and_publishes_nothing()
    {
        var events = new RecordingEvents();
        var game = CreateGame(events);
        var before = game.Board;

        Assert.That(game.TryMove(new Move(new Square(4, 1), new Square(4, 4))), Is.False);
        Assert.That(game.TryMove(new Move(new Square(4, 6), new Square(4, 4))), Is.False);
        Assert.That(game.Board, Is.SameAs(before));
        Assert.That(game.Turn, Is.EqualTo(PlayerColor.White));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.History, Is.Empty);
        Assert.That(events.Items, Is.Empty);
    }

    [Test]
    public void Undo_restores_each_previous_position_and_does_not_publish_moves()
    {
        var events = new RecordingEvents();
        var game = CreateGame(events);
        var initial = game.Board;
        var first = new Move(new Square(4, 1), new Square(4, 3));
        Assert.That(game.TryMove(first), Is.True);
        var afterFirst = game.Board;
        Assert.That(game.TryMove(new Move(new Square(4, 6), new Square(4, 4))), Is.True);
        events.Items.Clear();

        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Board, Is.SameAs(afterFirst));
        Assert.That(game.Turn, Is.EqualTo(PlayerColor.Black));
        Assert.That(game.History, Is.EqualTo(new[] { first }));
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Board, Is.SameAs(initial));
        Assert.That(game.Turn, Is.EqualTo(PlayerColor.White));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.History, Is.Empty);
        Assert.That(game.Undo(), Is.False);
        Assert.That(events.Items, Is.Empty);
    }

    [Test]
    public void Fools_mate_publishes_check_and_end_and_can_be_undone()
    {
        var events = new RecordingEvents();
        var game = CreateGame(events);
        Assert.That(game.TryMove(new Move(new Square(5, 1), new Square(5, 2))), Is.True);
        Assert.That(game.TryMove(new Move(new Square(4, 6), new Square(4, 4))), Is.True);
        Assert.That(game.TryMove(new Move(new Square(6, 1), new Square(6, 3))), Is.True);
        var beforeMate = game.Board;
        events.Items.Clear();
        var mate = new Move(new Square(3, 7), new Square(7, 3));

        Assert.That(game.TryMove(mate), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Checkmate));
        Assert.That(events.Items, Is.EqualTo(new object[]
        {
            new PieceMoved(mate, new Piece(PieceType.Queen, PlayerColor.Black)),
            new CheckRaised(PlayerColor.White),
            new GameEnded(GameStatus.Checkmate),
        }));
        Assert.That(game.LegalMoves(), Is.Empty);
        events.Items.Clear();
        Assert.That(game.TryMove(new Move(new Square(0, 1), new Square(0, 2))), Is.False);
        Assert.That(events.Items, Is.Empty);
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Board, Is.SameAs(beforeMate));
        Assert.That(game.Turn, Is.EqualTo(PlayerColor.Black));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.History, Has.Count.EqualTo(3));
    }

    private static ChessGame CreateGame(RecordingEvents events) =>
        new(events, new UnusedRepository(), new FixedClock());

    private sealed class RecordingEvents : IEventPublisher
    {
        public List<object> Items { get; } = [];
        public void Publish<T>(T domainEvent) => Items.Add(domainEvent!);
    }

    private sealed class FixedClock : IClock
    {
        public double ElapsedSeconds => 0;
    }

    private sealed class UnusedRepository : IGameRepository
    {
        public void Save(ChessGame game) => throw new NotSupportedException();
        public ChessGame Load(string id) => throw new NotSupportedException();
    }
}
