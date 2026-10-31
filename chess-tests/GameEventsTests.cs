using Chess.Core.Events;
using Chess.Core.Model;
using Chess.Core.Ports;
using Chess.Core.Services;
using NUnit.Framework;
using static Chess.Tests.GameTestSupport;

namespace Chess.Tests;

public class GameEventsTests
{
    private sealed class Recorder : IEventPublisher
    {
        public readonly List<object> Items = [];
        public void Publish<T>(T domainEvent) => Items.Add(domainEvent!);
    }

    private static ChessGame WithEvents(Recorder events, string fen = FenSerializer.InitialPosition)
    {
        var ports = Create();
        return new ChessGame(events, ports.Repository, ports.Clock, FenSerializer.Load(fen));
    }

    [Test]
    public void Legal_move_publishes_updated_position_and_illegal_move_publishes_nothing()
    {
        var events = new Recorder();
        var game = WithEvents(events);
        var move = Move("e2", "e4");
        Assert.That(game.TryMove(move), Is.True);
        Assert.That(events.Items, Is.EqualTo(new object[] { new PieceMoved(move, new Piece(PieceType.Pawn, PlayerColor.White)) }));
        Assert.That(game.Board.At(Square("e4"))?.Type, Is.EqualTo(PieceType.Pawn));
        events.Items.Clear();
        Assert.That(game.TryMove(Move("e4", "e5")), Is.False);
        Assert.That(game.Undo(), Is.True);
        Assert.That(events.Items, Is.Empty);
    }

    [Test]
    public void Mate_publishes_move_check_and_end_in_order_and_rejects_further_moves()
    {
        var events = new Recorder();
        var game = WithEvents(events);
        Play(game, "f2f3", "e7e5", "g2g4");
        events.Items.Clear();
        var mate = Move("d8", "h4");
        Assert.That(game.TryMove(mate), Is.True);
        Assert.That(events.Items, Is.EqualTo(new object[] {
            new PieceMoved(mate, new Piece(PieceType.Queen, PlayerColor.Black)),
            new CheckRaised(PlayerColor.White), new GameEnded(GameStatus.Checkmate) }));
        events.Items.Clear();
        Assert.That(game.TryMove(Move("a2", "a3")), Is.False);
        Assert.That(events.Items, Is.Empty);
    }

    [Test]
    public void Promotion_publishes_move_then_check_then_promotion()
    {
        var events = new Recorder();
        var game = WithEvents(events, "7k/P7/8/8/8/8/8/7K w - - 0 1");
        var move = Move("a7", "a8", PieceType.Queen);
        Assert.That(game.TryMove(move), Is.True);
        Assert.That(events.Items, Is.EqualTo(new object[] {
            new PieceMoved(move, new Piece(PieceType.Pawn, PlayerColor.White)),
            new CheckRaised(PlayerColor.Black), new PromotionOccurred(Square("a8"), PieceType.Queen) }));
    }

    [Test]
    public void Fifty_move_draw_publishes_draw_and_end_and_accepts_no_more_moves()
    {
        var events = new Recorder();
        var game = WithEvents(events, "7k/8/8/8/8/8/8/R6K w - - 99 50");
        Assert.That(game.TryMove(Move("a1", "a2")), Is.True);
        Assert.That(events.Items.Skip(1), Is.EqualTo(new object[] { new GameEnded(GameStatus.DrawByFiftyMove), new FiftyMoveDraw() }));
        events.Items.Clear();
        Assert.That(game.TryMove(Move("h8", "g8")), Is.False);
        Assert.That(events.Items, Is.Empty);
    }

    [Test]
    public void Threefold_repetition_publishes_position_key_and_end()
    {
        var events = new Recorder();
        var game = WithEvents(events);
        Play(game, "g1f3", "g8f6", "f3g1", "f6g8", "g1f3", "g8f6", "f3g1");
        events.Items.Clear();
        Play(game, "f6g8");
        Assert.That(events.Items.Skip(1), Is.EqualTo(new object[] {
            new GameEnded(GameStatus.DrawByRepetition),
            new DrawByRepetition("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq -") }));
    }
}
