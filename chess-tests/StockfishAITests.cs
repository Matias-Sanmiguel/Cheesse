using System.Diagnostics;
using System.Text.Json;
using Cheesse.Game.Adapters.Ai;
using Chess.Core.Model;
using Chess.Core.Services;
using NUnit.Framework;
using static Chess.Tests.GameTestSupport;

namespace Chess.Tests;

public class StockfishAITests
{
    [TestCase(1400, 1500)]
    [TestCase(5000, 3000)]
    [Platform(Exclude = "Win")]
    public async Task Handshake_configures_strength_and_sends_initial_fen_with_complete_history(int elo, int expectedElo)
    {
        using var engine = new FakeEngine("e2e4");
        var game = Create();
        Play(game, "g1f3", "g8f6", "f3g1", "f6g8");
        var history = game.History.ToArray();

        var result = await new StockfishAI(engine.Path, elo, 25).ChooseMoveAsync(game);

        Assert.That(result, Is.EqualTo(Move("e2", "e4")));
        Assert.That(engine.Commands, Is.EqualTo(new[]
        {
            "uci", "setoption name Threads value 1", "setoption name Hash value 16",
            "setoption name UCI_LimitStrength value true", $"setoption name UCI_Elo value {expectedElo}",
            "ucinewgame", "isready",
            $"position fen {FenSerializer.InitialPosition} moves g1f3 g8f6 f3g1 f6g8",
            "go movetime 25",
        }));
        Assert.That(game.History, Is.EqualTo(history));
        Assert.That(engine.IsRunning, Is.False);
    }

    [Test]
    [Platform(Exclude = "Win")]
    public void Synchronous_bridge_preserves_promotion_history_and_drains_noisy_stderr()
    {
        const string fen = "7k/P7/8/8/8/8/8/7K w - - 0 1";
        using var engine = new FakeEngine("h8h7", "noisy");
        var game = Create(fen);
        Assert.That(game.TryMove(Move("a7", "a8", PieceType.Queen)), Is.True);

        Assert.That(new StockfishAI(engine.Path).ChooseMove(game), Is.EqualTo(Move("h8", "h7")));
        Assert.That(engine.Commands, Does.Contain($"position fen {fen} moves a7a8q"));
        Assert.That(engine.IsRunning, Is.False);
    }

    [Test]
    [Platform(Exclude = "Win")]
    public async Task Full_strength_disables_elo_limit_and_reads_promotion()
    {
        using var engine = new FakeEngine("a7a8n");
        var game = Create("7k/P7/8/8/8/8/8/7K w - - 0 1");

        Assert.That(await new StockfishAI(engine.Path, 0).ChooseMoveAsync(game),
            Is.EqualTo(Move("a7", "a8", PieceType.Knight)));
        Assert.That(engine.Commands, Does.Contain("setoption name UCI_LimitStrength value false"));
        Assert.That(engine.Commands.Any(command => command.StartsWith("setoption name UCI_Elo")), Is.False);
    }

    [TestCase("e2e5")]
    [TestCase("nonsense")]
    [TestCase("0000")]
    [TestCase("(none)")]
    [TestCase("e2e4q")]
    [Platform(Exclude = "Win")]
    public void Illegal_or_malformed_bestmove_is_rejected_and_engine_is_reaped(string bestmove)
    {
        using var engine = new FakeEngine(bestmove);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new StockfishAI(engine.Path).ChooseMoveAsync(Create()));
        Assert.That(engine.IsRunning, Is.False);
    }

    [Test]
    [Platform(Exclude = "Win")]
    public async Task Caller_mutation_after_start_does_not_change_requested_position_or_move_validation()
    {
        using var engine = new FakeEngine("e2e4", "slow");
        var game = Create();
        var task = new StockfishAI(engine.Path).ChooseMoveAsync(game);
        Play(game, "d2d4");

        Assert.That(await task, Is.EqualTo(Move("e2", "e4")));
        Assert.That(engine.Commands, Does.Contain($"position fen {FenSerializer.InitialPosition}"));
        Assert.That(game.History, Is.EqualTo(new[] { Move("d2", "d4") }));
    }

    [TestCase("hang_uci")]
    [TestCase("hang_search")]
    [Platform(Exclude = "Win")]
    public async Task Cancellation_interrupts_protocol_reads_and_reaps_engine(string behavior)
    {
        using var engine = new FakeEngine("e2e4", behavior);
        using var cancellation = new CancellationTokenSource();
        var task = new StockfishAI(engine.Path).ChooseMoveAsync(Create(), cancellation.Token);
        await engine.WaitForCommand(behavior == "hang_search" ? "go movetime 500" : "uci");
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
        Assert.That(engine.IsRunning, Is.False);
    }

    [TestCase("hang_uci")]
    [TestCase("hang_search")]
    [Platform(Exclude = "Win")]
    public void Unresponsive_protocol_times_out_and_reaps_engine(string behavior)
    {
        using var engine = new FakeEngine("e2e4", behavior);
        Assert.ThrowsAsync<TimeoutException>(async () =>
            await new StockfishAI(engine.Path).ChooseMoveAsync(Create()));
        Assert.That(engine.IsRunning, Is.False);
    }

    [Test]
    [Platform(Exclude = "Win")]
    public void Unexpected_eof_reports_engine_failure_and_reaps_engine()
    {
        using var engine = new FakeEngine("e2e4", "eof");
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new StockfishAI(engine.Path).ChooseMoveAsync(Create()));
        Assert.That(engine.IsRunning, Is.False);
    }

    [Test]
    public void Invalid_arguments_and_finished_positions_fail_before_starting_an_engine()
    {
        Assert.Throws<ArgumentException>(() => new StockfishAI(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StockfishAI("missing", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StockfishAI("missing", 1800, 0));
        var ai = new StockfishAI("missing");
        Assert.ThrowsAsync<ArgumentNullException>(async () => await ai.ChooseMoveAsync(null!));
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ai.ChooseMoveAsync(Create("7k/8/5KQ1/8/8/8/8/8 b - - 0 1")));
    }

    [Test]
    public async Task Real_stockfish_returns_legal_moves_when_binary_is_configured()
    {
        var path = Environment.GetEnvironmentVariable("CHEESSE_STOCKFISH_PATH");
        if (string.IsNullOrWhiteSpace(path))
            Assert.Ignore("Set CHEESSE_STOCKFISH_PATH to run the native Stockfish integration check.");
        var game = Create();
        foreach (var elo in new[] { 1400, 1800, 2200, 0 })
        {
            var move = await new StockfishAI(path!, elo, 50).ChooseMoveAsync(game);
            Assert.That(game.LegalMoves(), Does.Contain(move));
            Assert.That(game.TryMove(move), Is.True);
        }
    }

    private sealed class FakeEngine : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cheesse-uci-" + Guid.NewGuid());
        private string LogPath => System.IO.Path.Combine(_directory, "commands");
        private string PidPath => System.IO.Path.Combine(_directory, "pid");
        public string Path => System.IO.Path.Combine(_directory, "fake-stockfish");
        public string[] Commands => File.ReadAllLines(LogPath);
        public bool IsRunning
        {
            get
            {
                if (!File.Exists(PidPath)) return false;
                try { using var process = Process.GetProcessById(int.Parse(File.ReadAllText(PidPath))); return !process.HasExited; }
                catch (ArgumentException) { return false; }
            }
        }

        public FakeEngine(string bestmove, string behavior = "normal")
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path, "#!/usr/bin/env python3\n" + $$"""
                import os, sys, time
                root = os.path.dirname(__file__)
                open(os.path.join(root, 'pid'), 'w').write(str(os.getpid()))
                behavior = {{JsonSerializer.Serialize(behavior)}}
                for raw in sys.stdin:
                    command = raw.strip()
                    with open(os.path.join(root, 'commands'), 'a') as log:
                        log.write(command + '\n')
                    if command == 'uci':
                        if behavior == 'hang_uci': time.sleep(60)
                        if behavior == 'eof': sys.exit(0)
                        print('option name UCI_Elo type spin default 1800 min 1500 max 3000', flush=True)
                        print('uciok', flush=True)
                    elif command == 'isready':
                        print('readyok', flush=True)
                    elif command.startswith('go '):
                        if behavior == 'hang_search': time.sleep(60)
                        if behavior == 'slow': time.sleep(0.1)
                        if behavior == 'noisy':
                            sys.stderr.write('x' * 200000)
                            sys.stderr.flush()
                        print('info depth 1 score cp 20', flush=True)
                        print('bestmove ' + {{JsonSerializer.Serialize(bestmove)}} + ' ponder e7e5', flush=True)
                """);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        public async Task WaitForCommand(string command)
        {
            for (var retry = 0; retry < 100; retry++)
            {
                if (File.Exists(LogPath) && Commands.Contains(command)) return;
                await Task.Delay(10);
            }
            Assert.Fail("fake engine did not receive " + command);
        }

        public void Dispose() => Directory.Delete(_directory, true);
    }
}
