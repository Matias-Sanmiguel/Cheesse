using Chess.Core.Model;

namespace Chess.Core.Rules;

internal static class FondueRules
{
    internal static bool HasKing(Board board, PlayerColor side) =>
        Enumerable.Range(0, 64).Any(index =>
            board.At(Square.FromIndex(index)) == new Piece(PieceType.King, side));

    internal static Square? CapturedSquare(Board board, Move move)
    {
        if (board.At(move.To) is not null) return move.To;
        if (board.At(move.From) is Piece { Type: PieceType.Pawn } pawn &&
            board.EnPassant == move.To && move.From.File != move.To.File)
        {
            var square = new Square(move.To.File, move.From.Rank);
            if (board.At(square) is Piece { Type: PieceType.Pawn } captured && captured.Color != pawn.Color)
                return square;
        }
        return null;
    }

    internal static Board Melt(Board moved, Square destination, Square captured)
    {
        var pending = new Queue<Square>();
        var visited = new HashSet<Square>();
        pending.Enqueue(destination);
        pending.Enqueue(captured);
        var board = moved;
        while (pending.TryDequeue(out var square))
        {
            if (!visited.Add(square)) continue;
            board = board.WithPiece(square, null);
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            {
                var file = square.File + dx;
                var rank = square.Rank + dy;
                if (file is < 0 or >= 8 || rank is < 0 or >= 8) continue;
                var neighbor = new Square(file, rank);
                if (board.At(neighbor) is not null) pending.Enqueue(neighbor);
            }
        }

        var rights = board.Castling;
        foreach (var side in new[] { PlayerColor.White, PlayerColor.Black })
        {
            var rank = side == PlayerColor.White ? 0 : 7;
            var king = board.At(new Square(4, rank)) == new Piece(PieceType.King, side);
            var shortRook = king && board.At(new Square(7, rank)) == new Piece(PieceType.Rook, side);
            var longRook = king && board.At(new Square(0, rank)) == new Piece(PieceType.Rook, side);
            rights = side == PlayerColor.White
                ? rights with { WhiteKingside = rights.WhiteKingside && shortRook, WhiteQueenside = rights.WhiteQueenside && longRook }
                : rights with { BlackKingside = rights.BlackKingside && shortRook, BlackQueenside = rights.BlackQueenside && longRook };
        }
        return board.WithCastling(rights);
    }

    internal static bool IsThreatened(Board board, PlayerColor side) =>
        !HasKing(board, side) || KingMeltingCaptures(board, side).Count > 0;

    // Expose exactly the opposing capture destinations responsible for a
    // Fondue check. The UI uses this same authoritative rule for its hints;
    // normal chess moves and the engine's legality remain unchanged.
    internal static IReadOnlyList<Move> KingMeltingCaptures(Board board, PlayerColor side)
    {
        var opponent = side == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
        if (!HasKing(board, side) || !HasKing(board, opponent))
            return [];

        var captures = new List<Move>();
        // Generate geometry using standard mode to avoid recursively testing castling/check.
        // En passant is available only to the side whose turn the position records.
        var geometry = board.WithMode(GameMode.Standard);
        if (board.Turn != opponent) geometry = geometry.WithEnPassant(null);
        foreach (var move in MoveGenerator.Generate(geometry, opponent))
        {
            if (board.At(move.To) is Piece { Type: PieceType.King } ||
                CapturedSquare(geometry, move) is null) continue;
            var after = board.WithMove(move);
            if (HasKing(after, opponent) && !HasKing(after, side))
                captures.Add(move);
        }
        return captures;
    }
}
