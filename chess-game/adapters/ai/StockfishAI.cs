using System.Diagnostics;
using System.Globalization;
using Chess.Core.Model;
using Chess.Core.Ports;
using Chess.Core.Services;

namespace Cheesse.Game.Adapters.Ai;

public sealed class StockfishAI : IAIPlayer
{
    private readonly string _executablePath;
    private readonly int _elo;
    private readonly int _moveTimeMilliseconds;
    private const int ProtocolTimeoutMilliseconds = 5000;

    public StockfishAI(string executablePath, int elo = 1800, int moveTimeMilliseconds = 500)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (elo < 0) throw new ArgumentOutOfRangeException(nameof(elo));
        if (moveTimeMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(moveTimeMilliseconds));
        _executablePath = executablePath;
        _elo = elo;
        _moveTimeMilliseconds = moveTimeMilliseconds;
    }

    public Move ChooseMove(ChessGame game) => ChooseMoveAsync(game).GetAwaiter().GetResult();

    public async Task<Move> ChooseMoveAsync(ChessGame game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();
        if (game.Board.Mode != GameMode.Standard)
            throw new NotSupportedException("Stockfish estándar no admite este modo; usá dos jugadores o RandomAI.");
        // Capture before yielding: the UI may continue, undo, or replace its game while we search.
        var legalMoves = game.LegalMoves().ToArray();
        if (legalMoves.Length == 0)
            throw new InvalidOperationException("No hay movimientos legales disponibles.");
        var position = "position fen " + FenSerializer.Write(game.InitialBoard);
        var history = game.History.ToArray();
        if (history.Length > 0)
            position += " moves " + string.Join(" ", history.Select(ToUci));

        return await Task.Run(() => SearchAsync(position, legalMoves, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Move> SearchAsync(string position, Move[] legalMoves, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(_executablePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(ProtocolTimeoutMilliseconds);
        var token = deadline.Token;
        process.Start();
        var drainErrors = DrainErrorsAsync(process.StandardError);
        try
        {
            await SendAsync("uci").ConfigureAwait(false);
            var minElo = 1;
            var maxElo = int.MaxValue;
            while (true)
            {
                var line = await ReadAsync().ConfigureAwait(false);
                if (line == "uciok") break;
                if (!line.StartsWith("option name UCI_Elo type spin ", StringComparison.Ordinal)) continue;
                var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (var index = 0; index < fields.Length - 1; index++)
                {
                    if (!int.TryParse(fields[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) continue;
                    if (fields[index] == "min") minElo = value;
                    if (fields[index] == "max") maxElo = value;
                }
            }

            deadline.CancelAfter(ProtocolTimeoutMilliseconds);
            await SendAsync("setoption name Threads value 1").ConfigureAwait(false);
            await SendAsync("setoption name Hash value 16").ConfigureAwait(false);
            await SendAsync("setoption name UCI_LimitStrength value " + (_elo == 0 ? "false" : "true")).ConfigureAwait(false);
            if (_elo != 0)
            {
                if (minElo > maxElo) throw new InvalidOperationException("Stockfish anunció un rango UCI_Elo inválido.");
                await SendAsync("setoption name UCI_Elo value " + Math.Clamp(_elo, minElo, maxElo).ToString(CultureInfo.InvariantCulture))
                    .ConfigureAwait(false);
            }
            await SendAsync("ucinewgame").ConfigureAwait(false);
            await SendAsync("isready").ConfigureAwait(false);
            while (await ReadAsync().ConfigureAwait(false) != "readyok") { }

            deadline.CancelAfter(TimeSpan.FromMilliseconds((long)_moveTimeMilliseconds + ProtocolTimeoutMilliseconds));
            await SendAsync(position).ConfigureAwait(false);
            await SendAsync("go movetime " + _moveTimeMilliseconds.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            while (true)
            {
                var line = await ReadAsync().ConfigureAwait(false);
                if (!line.StartsWith("bestmove ", StringComparison.Ordinal)) continue;
                var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length > 1)
                    foreach (var move in legalMoves)
                        if (ToUci(move) == fields[1]) return move;
                throw new InvalidOperationException("Stockfish devolvió un movimiento ilegal: " + line);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Stockfish no respondió dentro del tiempo permitido.");
        }
        finally
        {
            // Every request owns its process, including failed or cancelled handshakes.
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { } // The process may exit between checking and killing it.
            }
            await process.WaitForExitAsync().ConfigureAwait(false);
            await drainErrors.ConfigureAwait(false);
        }

        async Task SendAsync(string command)
        {
            await process.StandardInput.WriteLineAsync(command.AsMemory(), token).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(token).ConfigureAwait(false);
        }

        async Task<string> ReadAsync() =>
            await process.StandardOutput.ReadLineAsync(token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Stockfish cerró su salida antes de completar la búsqueda.");
    }

    private static async Task DrainErrorsAsync(StreamReader reader)
    {
        // Drain in fixed-size chunks: a noisy engine must not block or accumulate an unbounded log.
        var buffer = new char[1024];
        while (await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false) > 0) { }
    }

    private static string ToUci(Move move) => move.From.ToString() + move.To + (move.Promotion switch
    {
        null => "",
        PieceType.Queen => "q",
        PieceType.Rook => "r",
        PieceType.Bishop => "b",
        PieceType.Knight => "n",
        _ => throw new InvalidOperationException("Promoción inválida."),
    });
}
