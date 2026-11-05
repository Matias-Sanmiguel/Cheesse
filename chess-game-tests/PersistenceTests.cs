using System.Text.Json;
using System.Text.Json.Nodes;
using Chess.Core.Model;
using Chess.Core.Ports;
using Chess.Core.Services;
using Cheesse.Game.Adapters.Persist;
using Godot;
using Cheesse.Game.Adapters.Appearance;
using FileAccess = Godot.FileAccess;

namespace Cheesse.Game.Tests;

public partial class PersistenceTests : Node
{
    private const string SavePath = "user://cheesse-save.txt";
    private const string RestartInitial = "r3k2r/3p4/8/4P3/8/8/8/R3K2R b KQkq - 37 42";
    private const string RestartSaved = "r3k2r/8/8/3pP3/8/8/8/R3K2R w KQkq d6 0 43";
    private int _checks;

    public override void _Ready()
    {
        try
        {
            var mode = OS.GetCmdlineUserArgs().SingleOrDefault() ?? "--run";
            var repository = new FileSaveRepository();
            if (mode == "--save")
            {
                var game = Create(repository, RestartInitial);
                Play(game, "d7d5");
                repository.Save(game);
                Check(FileAccess.FileExists(SavePath), "save exists in user://");
                Check(FenSerializer.Write(game.Board) == RestartSaved, "restart save position");
            }
            else
            {
                if (mode == "--load")
                {
                    // This instance and process were created after the saving process exited.
                    var game = Create(repository);
                    Play(game, "g1f3");
                    Check(repository.Load(game), "load from previous process");
                    Check(FenSerializer.Write(game.Board) == RestartSaved, "restart FEN and turn");
                    Check(game.History.Count == 1 && game.Status == GameStatus.Ongoing, "restart history and status");
                    Check(game.Undo(), "restart Undo");
                    Check(FenSerializer.Write(game.Board) == RestartInitial, "restart initial snapshot");
                    Play(game, "d7d5", "e5d6");
                }
                else if (mode != "--run")
                {
                    throw new InvalidOperationException("Use --run, --save or --load.");
                }

                RunCases(repository);
                RemoveSave();
            }

            GD.Print($"PASS: persistence {mode}, {_checks} checks.");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void RunCases(FileSaveRepository repository)
    {
        Legacy_themed_save_keeps_appearance_without_v2_metadata();
        Previous_numeric_save_values_keep_their_original_meaning();
        Fondue_mode_and_melted_king_survive_save_load(repository);
        SwissCheese_roundtrip_preserves_holes_and_history(repository);
        Claimed_draw_results_roundtrip(repository);
        Invalid_positions_in_edited_saves_are_rejected(repository);
        Clock_and_outcome_survive_save_and_restart(repository);
        Text_import_and_export_preserve_results(repository);
        Loading_does_not_publish_historical_events(repository);
        MissingSaveKeepsOpenGame(repository);
        RoundTrip(repository, FenSerializer.InitialPosition);
        RoundTrip(repository, FenSerializer.InitialPosition, "e2e4", "c7c5", "g1f3");
        RoundTrip(repository, RestartInitial, "d7d5");
        RoundTrip(repository, "r3k2r/8/8/3pP3/8/8/8/R3K2R w KQkq d6 27 42", "e5d6");
        RoundTrip(repository, "r3k2r/8/8/8/3Pp3/8/8/R3K2R b KQkq d3 27 42", "e4d3");
        RoundTrip(repository, "r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 12 23", "e1g1", "e8c8");
        RoundTrip(repository, "7k/P7/8/8/8/8/8/7K w - - 8 11", "a7a8q");
        RoundTrip(repository, "7k/P7/8/8/8/8/8/7K w - - 8 11", "a7a8n");
        RoundTrip(repository, "4k2r/6P1/8/8/8/8/8/4K3 w - - 0 1", "g7h8q");
        RoundTrip(repository, FenSerializer.InitialPosition, "f2f3", "e7e5", "g2g4", "d8h4");
        RoundTrip(repository, "7k/8/8/8/8/8/8/R6K w - - 99 50", "a1a2");
        RoundTrip(repository, "7k/8/8/8/8/8/8/7K w - - 0 1");
        RoundTrip(repository, "7k/5K2/6Q1/8/8/8/8/8 b - - 0 1");
        RoundTrip(repository, "r6k/8/8/8/8/8/8/K7 w - - 0 1");
        RoundTrip(repository, FenSerializer.InitialPosition,
            "g1f3", "g8f6", "f3g1", "f6g8", "g1f3", "g8f6", "f3g1", "f6g8");
        RepetitionContinuesAfterLoad(repository);
        PicadaRoundTrip(repository);
        InvalidSavesKeepOpenGame(repository);
        OverwriteUsesMostRecentSave(repository);
        SaveFailureKeepsPreviousFile(repository);
        Check(!DirAccess.GetFilesAt("user://").Any(name =>
            name.StartsWith("cheesse-save.txt.") && name.EndsWith(".tmp")), "temporary files cleaned");
    }

    private void Legacy_themed_save_keeps_appearance_without_v2_metadata()
    {
        var oldSave = new
        {
            Version = 1,
            Fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
            Pgn = "1. e4 *",
            AgainstRandom = true,
            Theme = new VisualTheme("01_TablaDePicada", "01_Brie_vs_Salame", "03_Emmental_vs_Pepperoni")
        };
        using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write))
            file.StoreString(JsonSerializer.Serialize(oldSave));
        var repository = new FileSaveRepository();
        var game = Create(repository);
        Check(repository.Load(game), "Nico's v1 themed save loads without v2 clock metadata");
        Check(FenSerializer.Write(game.Board) == oldSave.Fen && game.History.Count == 1,
            "legacy themed save preserves the actual position and history");
        Check(repository.Theme == oldSave.Theme, "legacy save preserves independent visual teams");
        Check(game.TimeControl == TimeControl.Unlimited && repository.AgainstStockfish,
            "legacy AI save defaults to unlimited time and migrated opponent");
        Check(game.Undo(), "legacy themed save retains Undo after upgrade");
    }

    private void Previous_numeric_save_values_keep_their_original_meaning()
    {
        // Fixtures use the numeric values written by the previous two branches.
        const string stalemateFen = "7k/5K2/6Q1/8/8/8/8/8 b - - 0 1";
        var oldV2 = new
        {
            Version = 2, Fen = stalemateFen,
            Pgn = "[SetUp \"1\"] [FEN \"" + stalemateFen + "\"] [Result \"1/2-1/2\"] 1/2-1/2",
            EngineElo = 1800, Status = 3,
            TimeControl = new { InitialSeconds = 0, IncrementSeconds = 0 },
            Clock = new { WhiteSeconds = 0, BlackSeconds = 0, Running = false },
            ClockHistory = new[] { new { WhiteSeconds = 0, BlackSeconds = 0, Running = false } }
        };
        using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write))
            file.StoreString(JsonSerializer.Serialize(oldV2));
        var repository = new FileSaveRepository();
        var target = Create(repository);
        Check(repository.Load(target) && target.Status == GameStatus.Stalemate,
            "main's v2 numeric outcome still loads as stalemate after adding Picada");

        var oldPicada = new
        {
            Version = 1, Mode = 1,
            Fen = "7k/8/8/8/4K3/8/8/8 b - - 1 1",
            Pgn = "[Variant \"Rey de la Picada\"] [SetUp \"1\"] [FEN \"7k/8/8/8/8/4K3/8/8 w - - 0 1\"] [Result \"1-0\"] 1. Ke4 1-0"
        };
        using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write))
            file.StoreString(JsonSerializer.Serialize(oldPicada));
        Check(repository.Load(target) && target.Mode == GameMode.ReyDeLaPicada &&
            target.Status == GameStatus.PicadaVictory && target.Winner == PlayerColor.White,
            "Nico's v1 numeric mode still loads as a Picada win, not Fondue");
    }

    private void SwissCheese_roundtrip_preserves_holes_and_history(FileSaveRepository repository)
    {
        repository.AgainstStockfish = false;
        var source = new ChessGame(new Events(), repository, new Clock(), GameMode.QuesoSuizo);
        source.ConfigureClock(new TimeControl(300, 3));
        // Choose actual Swiss-legal moves; the holes vary between matches.
        for (int ply = 0; ply < 3; ply++)
        {
            var next = source.LegalMoves().First();
            Check(source.TryMove(next), "random Swiss opening remains playable");
        }
        var originalHoles = Chess.Core.Rules.SwissCheeseRules.Format(source.Board.SwissHoles);
        var expected = Snapshot(source);
        repository.Save(source);

        var target = Create(repository);
        Check(repository.Load(target), "Swiss game save loads");
        Check(target.Mode == GameMode.QuesoSuizo &&
              Chess.Core.Rules.SwissCheeseRules.Format(target.Board.SwissHoles) == originalHoles,
            "Swiss exact random hole layout and variant survive save/load");
        Check(Snapshot(target) == expected && target.History.Count == 3,
            "Swiss PGN moves and board are restored exactly");
        Check(target.TimeControl == new TimeControl(300, 3), "Swiss clock is preserved");
        Check(target.Undo() && target.Mode == GameMode.QuesoSuizo &&
              Chess.Core.Rules.SwissCheeseRules.Format(target.Board.SwissHoles) == originalHoles,
            "Swiss exact random holes remain intact after undo");
        Check(target.Redo() && Snapshot(target) == expected, "Swiss redo restores original history");
    }

    private void Fondue_mode_and_melted_king_survive_save_load(FileSaveRepository repository)
    {
        var ports = Create(repository);
        foreach (var fen in new[]
        {
            "7k/8/5n2/3pB3/8/8/8/K2R4 w - - 0 1",
            "7k/6p1/5n2/8/8/8/8/K5R1 w - - 0 1",
        })
        {
            var initial = FenSerializer.Load(fen).WithMode(GameMode.Fondue);
            var game = new ChessGame(ports.Events, repository, ports.Clock, initial);
            var move = fen.Contains("K2R4") ? "d1d5" : "g1g7";
            Play(game, move);
            repository.Save(game);
            var restored = Create(repository);
            Check(repository.Load(restored), "Fondue save loads: " + fen);
            Check(restored.Board.Mode == GameMode.Fondue, "Fondue mode restored");
            Check(FenSerializer.Write(restored.Board) == FenSerializer.Write(game.Board), "Fondue chain restored");
            Check(restored.Status == game.Status && restored.Winner == game.Winner, "Fondue outcome restored");
            Check(restored.Undo() && FenSerializer.Write(restored.Board) == fen, "Fondue undo after load");
            Check(restored.Redo() && restored.Status == game.Status, "Fondue redo after load");
        }
    }

    private void Claimed_draw_results_roundtrip(FileSaveRepository repository)
    {
        foreach (var announced in new[] { false, true })
        {
            var source = Create(repository, "7k/8/8/8/8/8/8/R6K w - - 99 50");
            var move = new Move(new Square(0, 0), new Square(0, 1));
            if (!announced) Check(source.TryMove(move), "fifty-move threshold reached");
            Check(source.ClaimDraw(source.Turn, announced ? move : null), "draw claimed now or by announced move");
            repository.Save(source);
            var target = Create(repository);
            Check(repository.Load(target), "claimed draw save loads");
            Check(target.Status == GameStatus.DrawByFiftyMove && target.Winner is null && target.History.Count == source.History.Count, "claimed result and unplayed announced move preserved");
        }
    }

    private void Invalid_positions_in_edited_saves_are_rejected(FileSaveRepository repository)
    {
        var source = Create(repository);
        repository.Save(source);
        string valid;
        using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read)) valid = file.GetAsText();
        var corrupt = JsonNode.Parse(valid)!;
        const string fen = "4k3/8/8/8/8/8/8/K3K3 w - - 0 1";
        corrupt["Fen"] = fen;
        corrupt["Pgn"] = "[SetUp \"1\"] [FEN \"" + fen + "\"] [Result \"*\"] *";
        using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write)) file.StoreString(corrupt.ToJsonString());
        Check(!repository.Load(source), "edited save with duplicate kings is rejected");
    }

    private void Clock_and_outcome_survive_save_and_restart(FileSaveRepository repository)
    {
        var clock = new MutableClock();
        repository.Theme = new VisualTheme("01_TablaDePicada", "01_Brie_vs_Salame", "03_Emmental_vs_Pepperoni");
        var source = new ChessGame(new Events(), repository, clock);
        source.ConfigureClock(new TimeControl(300, 3));
        clock.Seconds = 10;
        Play(source, "e2e4");
        clock.Seconds = 15;
        source.Tick();
        source.PauseClock();
        repository.AgainstStockfish = true;
        repository.EngineElo = 2200;
        repository.Save(source);
        var target = new ChessGame(new Events(), repository, new MutableClock { Seconds = 10000 });
        repository.Theme = VisualTheme.Classic;
        Check(repository.Load(target), "timed game loads");
        Check(repository.Theme == new VisualTheme("01_TablaDePicada", "01_Brie_vs_Salame", "03_Emmental_vs_Pepperoni"),
            "visual theme survives alongside real clocks and Stockfish settings");
        Check(target.TimeControl == new TimeControl(300, 3), "time control restored");
        Check(Math.Abs(target.ClockState.WhiteSeconds - 293) < 0.01 && Math.Abs(target.ClockState.BlackSeconds - 295) < 0.01, "remaining clocks restored without offline time");
        Check(repository.AgainstStockfish && repository.EngineElo == 2200, "Stockfish settings restored");
        Check(target.ClockHistory.Count == target.History.Count + 1, "clock history restored");
        Check(target.Resign(PlayerColor.Black), "black can resign");
        repository.Save(target);
        Check(repository.Load(source), "finished timed game loads");
        Check(source.Status == GameStatus.Resigned && source.Winner == PlayerColor.White, "resignation and winner restored");
        Check(!source.ClockState.Running, "finished clock stays paused");
        repository.AgainstStockfish = false;
        repository.EngineElo = 1800;
        repository.Theme = VisualTheme.Classic;
    }

    private void Text_import_and_export_preserve_results(FileSaveRepository repository)
    {
        var ports = Create(repository);
        var imported = GameTextFiles.Import("[Result \"1-0\"]\n\n1. e4 e5 1-0", ".pgn", ports.Events, repository, ports.Clock);
        Check(imported.Status == GameStatus.Resigned && imported.Winner == PlayerColor.White, "PGN resignation imported");
        Check(GameTextFiles.Export(imported, ".pgn").Contains("1-0"), "PGN result exported");
        var fen = GameTextFiles.Export(imported, ".fen");
        var position = GameTextFiles.Import(fen, ".fen", ports.Events, repository, ports.Clock);
        Check(position.History.Count == 0 && FenSerializer.Write(position.Board) == fen.Trim(), "FEN position roundtrips");
        var invalid = false;
        try { GameTextFiles.Import("8/8/8/8/8/8/8/8 w - - 0 1", ".fen", ports.Events, repository, ports.Clock); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException) { invalid = true; }
        Check(invalid, "invalid FEN kings rejected");
    }

    private sealed class MutableClock : IClock
    {
        public double Seconds { get; set; }
        public double ElapsedSeconds => Seconds;
    }

    private void Loading_does_not_publish_historical_events(FileSaveRepository repository)
    {
        var source = Create(repository);
        Play(source, "e2e4", "e7e5");
        repository.Save(source);
        var events = new Events();
        var target = new ChessGame(events, repository, new Clock());
        Check(repository.Load(target), "event test loads valid save");
        Check(events.Count == 0, "loading emits no historical domain events");
        string saved;
        using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read)) saved = file.GetAsText();
        var data = JsonNode.Parse(saved)!;
        data["Fen"] = FenSerializer.InitialPosition;
        using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write)) file.StoreString(data.ToJsonString());
        Check(!repository.Load(target), "inconsistent save rejected after replay");
        Check(events.Count == 0, "rejected load emits no historical domain events");
        Check(target.History.Count == 2, "failed load preserves open history");
    }

    private void RoundTrip(FileSaveRepository repository, string fen, params string[] moves)
    {
        var source = Create(repository, fen);
        Play(source, moves);
        var expected = Snapshot(source);
        var sourceBoard = source.Board;
        repository.Save(source);
        Check(ReferenceEquals(source.Board, sourceBoard), "Save does not change game");

        using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read))
        {
            Check(file is not null, "saved file can be read");
            var data = JsonNode.Parse(file!.GetAsText())!;
            Check(data["Fen"]!.GetValue<string>() == FenSerializer.Write(source.Board), "file contains current FEN");
            Check(data["Pgn"]!.GetValue<string>() == SanNotation.ToPgn(source), "file contains PGN history");
        }

        var target = Create(repository);
        Play(target, "e2e4");
        var events = target.Events;
        var clock = target.Clock;
        var historyView = target.History;
        Check(repository.Load(target), "Load succeeds");
        Check(Snapshot(target) == expected, "position, turn, status and full history round trip");
        Check(ReferenceEquals(target.Events, events) && ReferenceEquals(target.Clock, clock) &&
            ReferenceEquals(target.Repository, repository), "open game ports preserved");
        Check(ReferenceEquals(target.History, historyView), "open history view preserved");
        Check(target.LegalMoves().SequenceEqual(source.LegalMoves()), "legal moves preserved");
        for (var ply = 0; ply < source.History.Count; ply++)
            Check(target.Undo(), "loaded move can be undone");
        Check(!target.Undo(), "Undo stops at initial position");
        Check(FenSerializer.Write(target.Board) == fen, "all initial FEN fields restored by Undo");
        Check(source.Board == sourceBoard, "loaded Undo does not affect source");
    }

    private void MissingSaveKeepsOpenGame(FileSaveRepository repository)
    {
        RemoveSave();
        var game = Create(repository);
        Play(game, "e2e4");
        var before = Snapshot(game);
        var board = game.Board;
        Check(!repository.Load(game), "missing save returns false");
        Check(Snapshot(game) == before && ReferenceEquals(game.Board, board), "missing save keeps open game");
        Check(game.Undo(), "missing save keeps Undo");
    }

    private void PicadaRoundTrip(FileSaveRepository repository)
    {
        const string fen = "7k/8/8/8/8/4K3/8/8 w - - 0 1";
        var source = new ChessGame(new Events(), repository, new Clock(),
            FenSerializer.Load(fen), GameMode.ReyDeLaPicada);
        Play(source, "e3e4");
        Check(source.Status == GameStatus.PicadaVictory, "Picada ends when king reaches center");
        repository.Save(source);

        var target = Create(repository);
        Check(repository.Load(target), "Picada save reloads");
        Check(target.Mode == GameMode.ReyDeLaPicada &&
              target.Status == GameStatus.PicadaVictory &&
              FenSerializer.Write(target.Board) == FenSerializer.Write(source.Board),
            "saved mode, win status and board survive loading");
        Check(target.Undo() && target.Status == GameStatus.Ongoing,
            "undo works on a loaded Picada win");
        Check(target.Redo() && target.Status == GameStatus.PicadaVictory,
            "redo recovers loaded Picada win");
    }

    private void RepetitionContinuesAfterLoad(FileSaveRepository repository)
    {
        var source = Create(repository);
        Play(source, "g1f3", "g8f6", "f3g1", "f6g8");
        repository.Save(source);
        var target = Create(repository);
        Check(repository.Load(target), "load before repetition");
        Play(target, "g1f3", "g8f6", "f3g1", "f6g8");
        Check(target.CanClaimDraw && target.ClaimDraw(target.Turn), "repetition counts survive Load");
        Check(target.Status == GameStatus.DrawByRepetition, "repetition claim closes loaded game");
        Check(target.Undo() && target.Status == GameStatus.Ongoing, "Undo reopens loaded repetition");
    }

    private void InvalidSavesKeepOpenGame(FileSaveRepository repository)
    {
        var game = Create(repository);
        Play(game, "d2d4", "g8f6");
        var before = Snapshot(game);
        var board = game.Board;
        repository.Save(game);
        string valid;
        using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read))
            valid = file.GetAsText();

        var invalidFiles = new List<string> { "", "{", "null", "{}", "[]" };
        foreach (var (field, value) in new[]
        {
            ("Version", (JsonNode)JsonValue.Create(99)!),
            ("Fen", (JsonNode)JsonValue.Create(FenSerializer.InitialPosition)!),
            ("Fen", (JsonNode)JsonValue.Create("invalid FEN")!),
            ("Pgn", (JsonNode)JsonValue.Create("1. e5 *")!),
            ("Pgn", (JsonNode)JsonValue.Create("[SetUp \"1\"] [FEN \"8/8/8/8/8/8/8/8 w - - 0 1\"] 1. e4 *")!),
        })
        {
            var data = JsonNode.Parse(valid)!;
            data[field] = value;
            invalidFiles.Add(data.ToJsonString());
        }

        foreach (var content in invalidFiles)
        {
            using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write))
                file.StoreString(content);
            Check(!repository.Load(game), "invalid save returns false");
            Check(Snapshot(game) == before && ReferenceEquals(game.Board, board), "invalid save keeps open game");
        }
        Check(game.Undo(), "invalid saves keep Undo");
        Check(game.History.Count == 1, "invalid saves keep snapshot history");
    }

    private void OverwriteUsesMostRecentSave(FileSaveRepository repository)
    {
        var source = Create(repository);
        Play(source, "e2e4");
        repository.Save(source);
        Play(source, "e7e5");
        repository.Save(source);
        var target = Create(repository);
        Check(repository.Load(target), "overwritten save loads");
        Check(Snapshot(target) == Snapshot(source), "latest save wins");
    }

    private void SaveFailureKeepsPreviousFile(FileSaveRepository repository)
    {
        var game = Create(repository);
        Play(game, "e2e4");
        repository.Save(game);
        string saved;
        using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read))
            saved = file.GetAsText();

        // Holding the destination open without delete sharing prevents replacement on Windows.
        if (System.OperatingSystem.IsWindows())
        {
            using var locked = new System.IO.FileStream(ProjectSettings.GlobalizePath(SavePath),
                System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
            Play(game, "e7e5");
            var failed = false;
            try { repository.Save(game); }
            catch (System.IO.IOException) { failed = true; }
            Check(failed, "write error is reported to caller");
        }

        using var fileAfter = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        Check(fileAfter.GetAsText() == saved, "failed replacement keeps previous save");
    }

    private static ChessGame Create(FileSaveRepository repository, string fen = FenSerializer.InitialPosition) =>
        new(new Events(), repository, new Clock(), FenSerializer.Load(fen));

    private static void Play(ChessGame game, params string[] moves)
    {
        foreach (var move in moves)
        {
            PieceType? promotion = move.Length == 5 ? move[4] switch
            {
                'q' => PieceType.Queen,
                'r' => PieceType.Rook,
                'b' => PieceType.Bishop,
                'n' => PieceType.Knight,
                _ => throw new InvalidOperationException("Invalid test promotion."),
            } : null;
            if (!game.TryMove(new Move(new Square(move[0] - 'a', move[1] - '1'),
                    new Square(move[2] - 'a', move[3] - '1'), promotion)))
                throw new InvalidOperationException("Invalid test move: " + move);
        }
    }

    private static string Snapshot(ChessGame game) =>
        FenSerializer.Write(game.Board) + "|" + game.Status + "|" + SanNotation.ToPgn(game);

    private static void RemoveSave()
    {
        if (FileAccess.FileExists(SavePath) && DirAccess.RemoveAbsolute(SavePath) != Error.Ok)
            throw new InvalidOperationException("Cannot remove isolated test save.");
    }

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        _checks++;
    }

    private sealed class Events : IEventPublisher
    {
        public int Count { get; private set; }
        public void Publish<T>(T domainEvent) => Count++;
    }

    private sealed class Clock : IClock
    {
        public double ElapsedSeconds => 0;
    }
}
