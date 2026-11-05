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

    // Fondue 2.0: ordinary pawn captures stay ordinary. Only capturing a
    // knight, bishop, rook or queen starts melting. Neither adjacent pawns
    // nor more distant pieces propagate the effect.
    internal static bool IsMeltingCapture(Board board, Move move) =>
        CapturedSquare(board, move) is Square captured &&
        board.At(captured) is Piece { Type: not PieceType.Pawn };

    // A hot tile only burns a piece that FINISHES on it. Knights and sliding
    // pieces can pass/jump over it. Castling can move two pieces: the rook's
    // destination must be checked as well.
    internal static Square? HotLanding(Board board, Move move)
    {
        if (board.Mode != GameMode.Fondue || board.HotSquare is not Square hot)
            return null;
        if (move.To == hot) return hot;

        if (board.At(move.From) is not Piece { Type: PieceType.King } king ||
            move.From.File != 4 || move.To.Rank != move.From.Rank ||
            (move.To.File != 2 && move.To.File != 6))
            return null;

        int homeRank = king.Color == PlayerColor.White ? 0 : 7;
        if (move.From.Rank != homeRank) return null;

        bool shortSide = move.To.File == 6;
        var rookFrom = new Square(shortSide ? 7 : 0, homeRank);
        var rookTo = new Square(shortSide ? 5 : 3, homeRank);
        return rookTo == hot &&
            board.At(rookFrom) is Piece { Type: PieceType.Rook } rook &&
            rook.Color == king.Color ? hot : null;
    }

    private static IReadOnlyList<Square> MeltSquares(Board moved, Square destination)
    {
        var affected = new List<Square> { destination }; // victim + attacker
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            int file = destination.File + dx, rank = destination.Rank + dy;
            if (file is < 0 or >= 8 || rank is < 0 or >= 8) continue;
            var nearby = new Square(file, rank);
            if (moved.At(nearby) is Piece { Type: not PieceType.Pawn })
                affected.Add(nearby);
        }
        return affected;
    }

    // This is the single source of truth for the preview. Simulate without
    // melting first, so the preview and the actual area are always identical.
    internal static IReadOnlyList<Square> Preview(Board board, Move move)
    {
        if (board.Mode != GameMode.Fondue || !IsMeltingCapture(board, move))
            return [];
        var quiet = board.WithMode(GameMode.Standard).WithMove(move);
        return MeltSquares(quiet, move.To);
    }

    internal static Board Melt(Board moved, Square destination)
    {
        var board = moved;
        foreach (var square in MeltSquares(moved, destination))
            board = board.WithPiece(square, null);

        // Adjacent home rooks can also melt, so recompute castle rights after
        // the effect, without touching the movement rules of the other modes.
        var rights = board.Castling;
        foreach (var side in new[] { PlayerColor.White, PlayerColor.Black })
        {
            int rank = side == PlayerColor.White ? 0 : 7;
            bool king = board.At(new Square(4, rank)) == new Piece(PieceType.King, side);
            bool shortRook = king && board.At(new Square(7, rank)) == new Piece(PieceType.Rook, side);
            bool longRook = king && board.At(new Square(0, rank)) == new Piece(PieceType.Rook, side);
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
                !IsMeltingCapture(geometry, move)) continue;
            var after = board.WithMove(move);
            if (HasKing(after, opponent) && !HasKing(after, side))
                captures.Add(move);
        }
        return captures;
    }
}
