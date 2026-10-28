using System.IO;
using System.Text.Json;
using Chess.Core.Model;
using Chess.Core.Ports;
using Chess.Core.Services;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Cheesse.Game.Adapters.Persist;

public sealed class FileSaveRepository : IGameRepository
{
    private const string SavePath = "user://cheesse-save.txt";
    private const int SaveVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public void Save(ChessGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var data = new SaveData(SaveVersion, FenSerializer.Write(game.Board), SanNotation.ToPgn(game));
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
                if (file is null)
                    return false;
                content = file.GetAsText();
                if (file.GetError() is not (Error.Ok or Error.FileEof))
                    return false;
            }

            var data = JsonSerializer.Deserialize<SaveData>(content);
            if (data is null || data.Version != SaveVersion || data.Fen is null || data.Pgn is null)
                return false;

            var history = SanNotation.FromPgn(data.Pgn, out var initialBoard);
            var restored = new ChessGame(game.Events, game.Repository, game.Clock, initialBoard);
            foreach (var move in history)
            {
                if (!restored.TryMove(move))
                    return false;
            }

            // FEN and replayed history must describe the very same position.
            var expectedFen = FenSerializer.Write(FenSerializer.Load(data.Fen));
            if (FenSerializer.Write(restored.Board) != expectedFen)
                return false;

            game.RestoreFrom(restored);
            return true;
        }
        catch (Exception error) when (error is JsonException or FormatException or
            ArgumentException or InvalidOperationException or IOException)
        {
            // Parsing and replay occur off to the side; the open game is untouched.
            return false;
        }
    }

    private sealed record SaveData(int Version, string? Fen, string? Pgn);
}
