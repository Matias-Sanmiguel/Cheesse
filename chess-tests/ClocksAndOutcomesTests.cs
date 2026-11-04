using Chess.Core.Events;
using Chess.Core.Model;
using Chess.Core.Ports;
using Chess.Core.Services;
using NUnit.Framework;
using static Chess.Tests.GameTestSupport;

namespace Chess.Tests;

public class ClocksAndOutcomesTests
{
    private sealed class FakeClock : IClock { public double ElapsedSeconds { get; set; } }
    private sealed class Recorder : IEventPublisher
    {
        public List<object> Items { get; } = [];
        public void Publish<T>(T domainEvent) => Items.Add(domainEvent!);
    }
    private static (ChessGame Game, FakeClock Clock, Recorder Events) Timed(string fen = FenSerializer.InitialPosition)
    {
        var ports = Create();
        var clock = new FakeClock();
        var events = new Recorder();
        var game = new ChessGame(events, ports.Repository, clock, FenSerializer.Load(fen));
        game.ConfigureClock(new TimeControl(60, 2));
        return (game, clock, events);
    }

    [Test]
    public void Clock_charges_active_player_adds_increment_and_excludes_pause_time()
    {
        var (game, clock, _) = Timed();
        clock.ElapsedSeconds = 10;
        game.Tick();
        Assert.That(game.ClockState.WhiteSeconds, Is.EqualTo(50));
        Assert.That(game.TryMove(Move("e2", "e5")), Is.False);
        Assert.That(game.ClockState.WhiteSeconds, Is.EqualTo(50));
        Play(game, "e2e4");
        Assert.That(game.ClockState.WhiteSeconds, Is.EqualTo(52));
        clock.ElapsedSeconds = 15;
        game.PauseClock();
        clock.ElapsedSeconds = 100;
        game.ResumeClock();
        clock.ElapsedSeconds = 103;
        game.Tick();
        Assert.That(game.ClockState.BlackSeconds, Is.EqualTo(52));
    }

    [Test]
    public void Resuming_an_already_running_clock_preserves_elapsed_time()
    {
        var (game, clock, _) = Timed();
        clock.ElapsedSeconds = 10;
        game.ResumeClock();
        game.Tick();
        Assert.That(game.ClockState.WhiteSeconds, Is.EqualTo(50));
    }

    [Test]
    public void Repeated_live_frame_resume_calls_charge_each_interval_to_the_active_side()
    {
        var (game, clock, _) = Timed();
        for (var frame = 1; frame <= 100; frame++)
        {
            clock.ElapsedSeconds = frame / 10d;
            game.ResumeClock();
            game.Tick();
        }
        Assert.That(game.ClockState.WhiteSeconds, Is.EqualTo(50).Within(0.00001));
        Assert.That(game.ClockState.BlackSeconds, Is.EqualTo(60));
        Play(game, "e2e4");
        clock.ElapsedSeconds = 15;
        game.ResumeClock();
        game.Tick();
        Assert.That(game.ClockState.WhiteSeconds, Is.EqualTo(52).Within(0.00001));
        Assert.That(game.ClockState.BlackSeconds, Is.EqualTo(55));
    }

    [Test]
    public void Resuming_an_already_running_expired_clock_ends_once_and_cannot_restart_it()
    {
        var (game, clock, events) = Timed();
        clock.ElapsedSeconds = 61;
        game.ResumeClock();
        Assert.That(game.ClockState.WhiteSeconds, Is.Zero);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Timeout));
        Assert.That(game.Winner, Is.EqualTo(PlayerColor.Black));
        Assert.That(game.ClockState.Running, Is.False);
        clock.ElapsedSeconds = 100;
        game.ResumeClock();
        game.Tick();
        Assert.That(game.ClockState.Running, Is.False);
        Assert.That(events.Items, Is.EqualTo(new object[] { new GameEnded(GameStatus.Timeout) }));
    }

    [Test]
    public void Undo_redo_and_restore_preserve_exact_clocks_without_offline_charge()
    {
        var (game, clock, _) = Timed();
        clock.ElapsedSeconds = 10;
        Play(game, "e2e4");
        clock.ElapsedSeconds = 14;
        Play(game, "e7e5");
        var snapshot = game.CreateMemento();
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.ClockState, Is.EqualTo(new ClockSnapshot(52, 56, true)));
        Assert.That(game.Redo(), Is.True);
        Assert.That(game.ClockState, Is.EqualTo(new ClockSnapshot(52, 58, true)));
        clock.ElapsedSeconds = 1000;
        game.Restore(snapshot);
        Assert.That(game.ClockState, Is.EqualTo(new ClockSnapshot(52, 58, true)));
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.ClockState, Is.EqualTo(new ClockSnapshot(52, 56, true)));
    }

    [TestCase(FenSerializer.InitialPosition, GameStatus.Timeout, PlayerColor.Black)]
    [TestCase("7k/8/8/8/8/8/8/R6K w - - 0 1", GameStatus.DrawByTimeout, null)]
    [TestCase("7k/8/8/8/8/8/8/R5NK b - - 0 1", GameStatus.Timeout, PlayerColor.White)]
    public void Expiry_ends_with_winner_only_when_opponent_can_mate(string fen, GameStatus status, PlayerColor? winner)
    {
        var (game, clock, events) = Timed(fen);
        clock.ElapsedSeconds = 61;
        game.Tick();
        Assert.That(game.Status, Is.EqualTo(status));
        Assert.That(game.Winner, Is.EqualTo(winner));
        Assert.That(game.ClockState.Running, Is.False);
        Assert.That(events.Items, Is.EqualTo(new object[] { new GameEnded(status) }));
        game.Tick();
        Assert.That(events.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public void Clock_restore_rejects_nonfinite_negative_and_unbounded_values()
    {
        var (game, _, _) = Timed();
        Assert.Throws<ArgumentOutOfRangeException>(() => game.ConfigureClock(new TimeControl(-1, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => game.RestoreClock(new TimeControl(60, 0), new ClockSnapshot(double.NaN, 1, true)));
        Assert.Throws<ArgumentOutOfRangeException>(() => game.RestoreClock(new TimeControl(60, 0), new ClockSnapshot(-1, 1, true)));
        Assert.Throws<ArgumentOutOfRangeException>(() => game.RestoreClock(new TimeControl(60, 0), new ClockSnapshot(double.MaxValue, 1, true)));
    }

    [Test]
    public void Draw_offer_can_follow_own_move_and_opponent_move_rejects_it()
    {
        var game = Create();
        Assert.That(game.OfferDraw(PlayerColor.White), Is.True);
        Assert.That(game.AcceptDraw(PlayerColor.White), Is.False);
        Play(game, "e2e4");
        Assert.That(game.DrawOfferedBy, Is.EqualTo(PlayerColor.White));
        Play(game, "e7e5");
        Assert.That(game.DrawOfferedBy, Is.Null);
        Assert.That(game.OfferDraw(PlayerColor.Black), Is.True);
        Assert.That(game.DeclineDraw(PlayerColor.White), Is.True);
        Assert.That(game.DrawOfferedBy, Is.Null);
    }

    [Test]
    public void Agreement_and_resignation_publish_only_end_and_undo_reopens_without_winner()
    {
        var (game, _, events) = Timed();
        Play(game, "e2e4");
        events.Items.Clear();
        Assert.That(game.Resign(PlayerColor.White), Is.True);
        Assert.That(game.Winner, Is.EqualTo(PlayerColor.Black));
        Assert.That(events.Items, Is.EqualTo(new object[] { new GameEnded(GameStatus.Resigned) }));
        Assert.That(game.Resign(PlayerColor.Black), Is.False);
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Winner, Is.Null);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.OfferDraw(PlayerColor.White), Is.True);
        Assert.That(game.AcceptDraw(PlayerColor.Black), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.DrawByAgreement));
        Assert.That(game.Winner, Is.Null);
    }

    [Test]
    public void Resignation_is_draw_when_opponent_cannot_possibly_mate()
    {
        var game = Create("7k/8/8/8/8/8/8/R6K w - - 0 1");
        Assert.That(game.Resign(PlayerColor.White), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.DrawByResignation));
        Assert.That(game.Winner, Is.Null);
        Assert.That(SanNotation.ToPgn(game), Does.Contain("[Result \"1/2-1/2\"]"));
    }

    [Test]
    public void Saved_clock_history_is_validated_atomically_and_can_restore_its_own_readonly_view()
    {
        var (game, clock, _) = Timed();
        clock.ElapsedSeconds = 5;
        Play(game, "e2e4");
        var expected = game.ClockHistory.ToArray();
        game.RestoreClockHistory(game.ClockHistory);
        Assert.That(game.ClockHistory, Is.EqualTo(expected));
        Assert.Throws<ArgumentOutOfRangeException>(() => game.RestoreClockHistory(new[] { new ClockSnapshot(double.NaN, 60, true), game.ClockState }));
        Assert.That(game.ClockHistory, Is.EqualTo(expected));
    }

    [Test]
    public void Saved_outcomes_reject_false_winners_impossible_timeouts_and_unearned_claims()
    {
        var (game, _, _) = Timed();
        Assert.Throws<ArgumentException>(() => game.RestoreOutcome(GameStatus.Timeout, PlayerColor.Black));
        Assert.Throws<ArgumentException>(() => game.RestoreOutcome(GameStatus.DrawByRepetition, null));
        Assert.Throws<ArgumentException>(() => game.RestoreOutcome(GameStatus.Ongoing, PlayerColor.White));
        Assert.Throws<ArgumentException>(() => game.RestoreOutcome(GameStatus.DrawByResignation, null));
        game.RestoreOutcome(GameStatus.Resigned, PlayerColor.Black);
        Assert.That(game.Winner, Is.EqualTo(PlayerColor.Black));
        Assert.That(game.ClockState.Running, Is.False);
    }

    [TestCase("r6k/8/8/8/8/8/8/7K w - - 0 1", "1-0")]
    [TestCase("7k/8/8/8/8/8/8/R6K w - - 0 1", "0-1")]
    public void Pgn_decisive_result_rejects_a_winner_that_cannot_mate(string fen, string result)
    {
        var moves = SanNotation.FromPgn($"[SetUp \"1\"]\n[FEN \"{fen}\"]\n[Result \"{result}\"]\n\n{result}", out var initial, out var importedResult);
        var ports = Create();
        var game = new ChessGame(ports.Events, ports.Repository, ports.Clock, initial);
        Assert.That(moves, Is.Empty);
        Assert.Throws<FormatException>(() => game.SetImportedResult(importedResult));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.Winner, Is.Null);
    }

    [TestCase("r6k/8/8/8/8/8/8/7K w - - 0 1", PlayerColor.White)]
    [TestCase("7k/8/8/8/8/8/8/R6K w - - 0 1", PlayerColor.Black)]
    public void Saved_resignation_rejects_impossible_winner_and_accepts_draw(string fen, PlayerColor impossibleWinner)
    {
        var game = Create(fen);
        Assert.Throws<ArgumentException>(() => game.RestoreOutcome(GameStatus.Resigned, impossibleWinner));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.Winner, Is.Null);
        game.RestoreOutcome(GameStatus.DrawByResignation, null);
        Assert.That(game.Status, Is.EqualTo(GameStatus.DrawByResignation));
        Assert.That(game.Winner, Is.Null);
    }

    [TestCase("1-0", PlayerColor.White)]
    [TestCase("0-1", PlayerColor.Black)]
    [TestCase("1/2-1/2", null)]
    public void Pgn_roundtrips_external_results(string result, PlayerColor? winner)
    {
        var history = SanNotation.FromPgn($"[Result \"{result}\"]\n\n1. e4 {result}", out var board, out var parsedResult);
        var ports = Create();
        var game = new ChessGame(ports.Events, ports.Repository, ports.Clock, board);
        foreach (var move in history) Assert.That(game.TryMove(move), Is.True);
        game.SetImportedResult(parsedResult);
        Assert.That(game.Winner, Is.EqualTo(winner));
        Assert.That(game.State.AcceptsMoves, Is.False);
        Assert.That(SanNotation.ToPgn(game), Does.Contain($"[Result \"{result}\"]"));
        Assert.That(SanNotation.ToPgn(game).TrimEnd(), Does.EndWith(result));
    }
}
