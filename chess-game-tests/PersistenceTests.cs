using System.Text.Json;
using System.Text.Json.Nodes;
using Chess.Core.Model;
using Chess.Core.Ports;
using Chess.Core.Services;
using Cheesse.Game.Adapters.Persist;
using Godot;
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
        InvalidSavesKeepOpenGame(repository);
        OverwriteUsesMostRecentSave(repository);
        SaveFailureKeepsPreviousFile(repository);
        Check(!DirAccess.GetFilesAt("user://").Any(name =>
            name.StartsWith("cheesse-save.txt.") && name.EndsWith(".tmp")), "temporary files cleaned");
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

    private void RepetitionContinuesAfterLoad(FileSaveRepository repository)
    {
        var source = Create(repository);
        Play(source, "g1f3", "g8f6", "f3g1", "f6g8");
        repository.Save(source);
        var target = Create(repository);
        Check(repository.Load(target), "load before repetition");
        Play(target, "g1f3", "g8f6", "f3g1", "f6g8");
        Check(target.Status == GameStatus.DrawByRepetition, "repetition counts survive Load");
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
            ("Version", (JsonNode)JsonValue.Create(2)!),
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
        public void Publish<T>(T domainEvent) { }
    }

    private sealed class Clock : IClock
    {
        public double ElapsedSeconds => 0;
    }
}
