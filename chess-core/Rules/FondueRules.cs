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
        !BurnsBeforeDestination(board, move) &&
        CapturedSquare(board, move) is Square captured &&
        board.At(captured) is Piece { Type: not PieceType.Pawn };

    // The heated tile is not an obstacle. Every piece except a knight burns
    // as soon as its path touches the tile; a knight only burns on landing.
    // This includes a pawn's two-square advance and the rook in castling.
    internal static Square? HotLanding(Board board, Move move)
    {
        if (board.Mode != GameMode.Fondue || board.HotSquare is not Square hot ||
            board.At(move.From) is not Piece mover)
            return null;
        if (move.To == hot) return hot;

        int df = move.To.File - move.From.File;
        int dr = move.To.Rank - move.From.Rank;
        int files = Math.Abs(df), ranks = Math.Abs(dr);
        bool castling = mover.Type == PieceType.King && files == 2 && dr == 0 &&
            move.From.File == 4 && move.From.Rank == (mover.Color == PlayerColor.White ? 0 : 7);

        bool followsRay = mover.Type switch
        {
            PieceType.Rook => (files == 0) != (ranks == 0),
            PieceType.Bishop => files == ranks && files > 0,
            PieceType.Queen => (files == 0) != (ranks == 0) || files == ranks && files > 0,
            PieceType.Pawn => files == 0 && ranks == 2,
            PieceType.King => castling,
            _ => false
        };
        if (followsRay)
        {
            int steps = Math.Max(files, ranks);
            int stepFile = Math.Sign(df), stepRank = Math.Sign(dr);
            for (int i = 1; i < steps; i++)
                if (new Square(move.From.File + stepFile * i,
                    move.From.Rank + stepRank * i) == hot)
                    return hot;
        }

        // King and rook move independently during castling. The king's route
        // has priority above; also check every intermediate rook square.
        if (castling)
        {
            int rank = move.From.Rank;
            bool kingside = df > 0;
            int rookFile = kingside ? 7 : 0;
            int rookEnd = kingside ? 5 : 3;
            if (board.At(new Square(rookFile, rank)) is Piece { Type: PieceType.Rook } rook &&
                rook.Color == mover.Color)
                for (int file = rookFile + Math.Sign(rookEnd - rookFile);
                     file != rookEnd + Math.Sign(rookEnd - rookFile);
                     file += Math.Sign(rookEnd - rookFile))
                    if (hot == new Square(file, rank))
                        return hot;
        }
        return null;
    }

    internal static bool HotBurnsMover(Board board, Move move)
    {
        if (HotLanding(board, move) is not Square hot) return false;
        // If only the rook's castling path touches heat, the king survives.
        if (board.At(move.From) is Piece { Type: PieceType.King } &&
            move.From.File == 4 && Math.Abs(move.To.File - move.From.File) == 2 &&
            move.From.Rank == move.To.Rank)
            return hot == move.To ||
                hot == new Square(move.From.File + Math.Sign(move.To.File - move.From.File),
                    move.From.Rank);
        return true;
    }

    // A capture beyond the heated square never happens: the attacker melts
    // on the way, so the defending piece stays where it was.
    internal static bool BurnsBeforeDestination(Board board, Move move) =>
        HotLanding(board, move) is Square hot && hot != move.To &&
        HotBurnsMover(board, move);

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
                !IsMeltingCapture(board, move)) continue;
            var after = board.WithMove(move);
            if (HasKing(after, opponent) && !HasKing(after, side))
                captures.Add(move);
        }
        return captures;
    }
}
