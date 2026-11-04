using System.IO;
using System.Text.Json;
using Chess.Core.Model;
using Chess.Core.Ports;
using Chess.Core.Services;
using Godot;
using Cheesse.Game.Adapters.Appearance;
using FileAccess = Godot.FileAccess;

namespace Cheesse.Game.Adapters.Persist;

public sealed class FileSaveRepository : IGameRepository
{
    private const string SavePath = "user://cheesse-save.txt";
    private const int SaveVersion = 2;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Save validation must not notify observers of reconstructed historical moves.
    private sealed class ReplayEvents : IEventPublisher
    {
        public void Publish<T>(T domainEvent) { }
    }

    public bool HasSave => FileAccess.FileExists(SavePath);

    // UI mode belongs to the adapter; the chess domain remains independent.
    public VisualTheme Theme { get; set; } = VisualTheme.Classic;
    public bool AgainstStockfish { get; set; }
    public int EngineElo { get; set; } = 1800;
    public bool AgainstRandom { get => AgainstStockfish; set => AgainstStockfish = value; }

    public void Save(ChessGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        game.Tick();
        var data = new SaveData(SaveVersion, FenSerializer.Write(game.Board), SanNotation.ToPgn(game),
            AgainstStockfish, AgainstStockfish, EngineElo, game.TimeControl, game.ClockState,
            game.Status, game.Winner, game.DrawOfferedBy, game.ClockHistory.ToArray(), Theme, game.Mode);
        var content = JsonSerializer.Serialize(data, JsonOptions);
        var temporaryPath = SavePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            // Close a complete temporary file before replacing the previous save.
            using (var file = FileAccess.Open(temporaryPath, FileAccess.ModeFlags.Write))
            {
                if (file is null)
                    throw new IOException($"Cannot open save file: {FileAccess.GetOpenError()}.");

                file.StoreString(content);
                file.Flush();
                if (file.GetError() != Error.Ok)
                    throw new IOException($"Cannot write save file: {file.GetError()}.");
            }

            var error = DirAccess.RenameAbsolute(temporaryPath, SavePath);
            if (error != Error.Ok)
                throw new IOException($"Cannot replace save file: {error}.");
        }
        finally
        {
            if (FileAccess.FileExists(temporaryPath))
                DirAccess.RemoveAbsolute(temporaryPath);
        }
    }

    public bool Load(ChessGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!FileAccess.FileExists(SavePath))
            return false;

        try
        {
            string content;
            using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read))
            {
                if (file is null || file.GetLength() > 4 * 1024 * 1024)
                    return false;
                content = file.GetAsText();
                if (file.GetError() is not (Error.Ok or Error.FileEof))
                    return false;
            }

            var data = JsonSerializer.Deserialize<SaveData>(content);
            if (data is null || data.Version is not (1 or SaveVersion) || data.Fen is null || data.Pgn is null)
                return false;

            var history = SanNotation.FromPgn(data.Pgn, out var initialBoard, out var result);
            if (data.Mode is GameMode mode && mode != initialBoard.Mode) return false;
            GameTextFiles.ValidatePosition(initialBoard);
            var restored = new ChessGame(new ReplayEvents(), game.Repository, game.Clock, initialBoard);
            foreach (var move in history)
            {
                if (!restored.TryMove(move))
                    return false;
            }

            // FEN and replayed history must describe the very same position.
            var expectedFen = FenSerializer.Write(FenSerializer.Load(data.Fen));
            if (FenSerializer.Write(restored.Board) != expectedFen)
                return false;

            if (data.Version == SaveVersion)
            {
                if (data.TimeControl is null || data.Clock is null || data.ClockHistory is null ||
                    data.Status is null || data.EngineElo is not (0 or 1400 or 1800 or 2200))
                    return false;
                restored.RestoreClock(data.TimeControl.Value, data.Clock.Value);
                restored.RestoreClockHistory(data.ClockHistory);
                restored.RestoreOutcome(data.Status.Value, data.Winner, data.DrawOfferedBy);
                // The PGN and explicit save outcome must agree before replacing the live game.
                if (result != (restored.Winner == PlayerColor.White ? "1-0" :
                    restored.Winner == PlayerColor.Black ? "0-1" : restored.State.AcceptsMoves ? "*" : "1/2-1/2")) return false;
            }
            else
            {
                if (result == "1/2-1/2" && restored.CanClaimDraw)
                    restored.ClaimDraw(restored.Turn);
                else
                    restored.SetImportedResult(result);
            }
            game.RestoreFrom(restored);
            AgainstStockfish = data.AgainstStockfish ?? data.AgainstRandom;
            EngineElo = data.Version == 1 ? 1800 : data.EngineElo;
            Theme = VisualThemeCatalog.Normalize(data.Theme);
            return true;
        }
        catch (Exception error) when (error is JsonException or FormatException or
            ArgumentException or InvalidOperationException or IOException)
        {
            // Parsing and replay occur off to the side; the open game is untouched.
            return false;
        }
    }

    private sealed record SaveData(int Version, string? Fen, string? Pgn, bool AgainstRandom = false,
        bool? AgainstStockfish = null, int EngineElo = 1800, TimeControl? TimeControl = null,
        ClockSnapshot? Clock = null, GameStatus? Status = null, PlayerColor? Winner = null,
        PlayerColor? DrawOfferedBy = null, ClockSnapshot[]? ClockHistory = null, VisualTheme? Theme = null, GameMode? Mode = null);
}
