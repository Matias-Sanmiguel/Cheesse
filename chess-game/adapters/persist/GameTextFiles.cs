using System.IO;
using System.Text;
using Chess.Core.Model;
using Chess.Core.Ports;
using Chess.Core.Rules;
using Chess.Core.Services;

namespace Cheesse.Game.Adapters.Persist;

public static class GameTextFiles
{
    private const int MaximumFileBytes = 1024 * 1024;

    public static ChessGame Import(string text, string extension, IEventPublisher events,
        IGameRepository repository, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Encoding.UTF8.GetByteCount(text) > MaximumFileBytes)
            throw new FormatException("El archivo supera el límite de 1 MB.");
        Board board;
        IReadOnlyList<Move> moves = [];
        var result = "*";
        switch (extension.ToLowerInvariant())
        {
            case ".fen": board = FenSerializer.Load(text.Trim()); break;
            case ".pgn": moves = SanNotation.FromPgn(text, out board, out result); break;
            default: throw new FormatException("Elegí un archivo .fen o .pgn.");
        }
        ValidatePosition(board);
        var game = new ChessGame(events, repository, clock, board);
        foreach (var move in moves)
            if (!game.TryMove(move)) throw new FormatException("El PGN contiene una jugada ilegal.");
        if (extension.Equals(".pgn", StringComparison.OrdinalIgnoreCase))
            game.SetImportedResult(result);
        return game;
    }

    public static string Export(ChessGame game, string extension) => extension.ToLowerInvariant() switch
    {
        ".fen" => FenSerializer.Write(game.Board) + "\n",
        ".pgn" => SanNotation.ToPgn(game),
        _ => throw new FormatException("Elegí la extensión .fen o .pgn."),
    };

    public static string Read(string path)
    {
        if (new FileInfo(path).Length > MaximumFileBytes)
            throw new FormatException("El archivo supera el límite de 1 MB.");
        return File.ReadAllText(path);
    }

    public static void Write(string path, string text)
    {
        var fullPath = Path.GetFullPath(path);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            File.Move(temporary, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static void ValidatePosition(Board board)
    {
        var pieces = Enumerable.Range(0, 64).Select(index =>
            (Square: Square.FromIndex(index), Piece: board.At(Square.FromIndex(index)))).ToArray();
        foreach (var side in new[] { PlayerColor.White, PlayerColor.Black })
        {
            var own = pieces.Where(item => item.Piece?.Color == side).ToArray();
            if (own.Count(item => item.Piece?.Type == PieceType.King) != 1 || own.Length > 16 ||
                own.Count(item => item.Piece?.Type == PieceType.Pawn) > 8 ||
                own.Any(item => item.Piece?.Type == PieceType.Pawn && item.Square.Rank is 0 or 7))
                throw new FormatException("La posición tiene reyes o piezas inválidas.");
        }
        foreach (var side in new[] { PlayerColor.White, PlayerColor.Black })
        {
            var rank = side == PlayerColor.White ? 0 : 7;
            var shortRight = side == PlayerColor.White ? board.Castling.WhiteKingside : board.Castling.BlackKingside;
            var longRight = side == PlayerColor.White ? board.Castling.WhiteQueenside : board.Castling.BlackQueenside;
            if ((shortRight || longRight) && board.At(new Square(4, rank)) != new Piece(PieceType.King, side) ||
                shortRight && board.At(new Square(7, rank)) != new Piece(PieceType.Rook, side) ||
                longRight && board.At(new Square(0, rank)) != new Piece(PieceType.Rook, side))
                throw new FormatException("Los derechos de enroque no coinciden con las piezas.");
        }
        if (board.EnPassant is Square target)
        {
            var pawnSide = board.Turn == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
            var pawnRank = board.Turn == PlayerColor.White ? 4 : 3;
            var targetRank = board.Turn == PlayerColor.White ? 5 : 2;
            if (target.Rank != targetRank || board.At(target) is not null ||
                board.At(new Square(target.File, pawnRank)) != new Piece(PieceType.Pawn, pawnSide))
                throw new FormatException("La casilla al paso no coincide con un peón rival.");
        }
        var previousSide = board.Turn == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
        if (CheckDetector.IsInCheck(board, previousSide))
            throw new FormatException("El jugador que acaba de mover no puede quedar en jaque.");
    }
}
