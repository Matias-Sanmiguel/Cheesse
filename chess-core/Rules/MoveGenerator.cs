using Chess.Core.Model;

namespace Chess.Core.Rules;

public static class MoveGenerator
{
    private static readonly (int File, int Rank)[] KnightOffsets =
    [
        (-2, -1), (-2, 1), (-1, -2), (-1, 2),
        (1, -2), (1, 2), (2, -1), (2, 1),
    ];

    private static readonly (int File, int Rank)[] DiagonalDirections =
    [
        (-1, -1), (-1, 1), (1, -1), (1, 1),
    ];

    private static readonly (int File, int Rank)[] StraightDirections =
    [
        (-1, 0), (1, 0), (0, -1), (0, 1),
    ];

    private static readonly (int File, int Rank)[] KingOffsets =
    [
        (-1, -1), (-1, 0), (-1, 1), (0, -1),
        (0, 1), (1, -1), (1, 0), (1, 1),
    ];

    public static IReadOnlyList<Move> Generate(Board board, PlayerColor side)
    {
        var moves = new List<Move>();

        for (var index = 0; index < 64; index++)
        {
            var from = Square.FromIndex(index);
            if (board.At(from) is not Piece piece || piece.Color != side)
                continue;

            switch (piece.Type)
            {
                case PieceType.Pawn:
                    AddPawnMoves(board, from, side, moves);
                    break;
                case PieceType.Knight:
                    AddStepMoves(board, from, side, KnightOffsets, moves);
                    break;
                case PieceType.Bishop:
                    AddSlidingMoves(board, from, side, DiagonalDirections, moves);
                    break;
                case PieceType.Rook:
                    AddSlidingMoves(board, from, side, StraightDirections, moves);
                    break;
                case PieceType.Queen:
                    AddSlidingMoves(board, from, side, DiagonalDirections, moves);
                    AddSlidingMoves(board, from, side, StraightDirections, moves);
                    break;
                case PieceType.King:
                    AddStepMoves(board, from, side, KingOffsets, moves);
                    break;
            }
        }

        return moves;
    }

    private static void AddPawnMoves(Board board, Square from, PlayerColor side, List<Move> moves)
    {
        var direction = side == PlayerColor.White ? 1 : -1;
        var startingRank = side == PlayerColor.White ? 1 : 6;
        var nextRank = from.Rank + direction;
        if (!IsOnBoard(from.File, nextRank))
            return;

        var forward = new Square(from.File, nextRank);
        if (board.At(forward) is null)
        {
            moves.Add(new Move(from, forward));

            if (from.Rank == startingRank)
            {
                var doubleForward = new Square(from.File, from.Rank + 2 * direction);
                if (board.At(doubleForward) is null)
                    moves.Add(new Move(from, doubleForward));
            }
        }

        for (var fileOffset = -1; fileOffset <= 1; fileOffset += 2)
        {
            var file = from.File + fileOffset;
            if (!IsOnBoard(file, nextRank))
                continue;

            var target = new Square(file, nextRank);
            if (board.At(target) is Piece occupant && occupant.Color != side)
                moves.Add(new Move(from, target));
        }
    }

    private static void AddStepMoves(
        Board board, Square from, PlayerColor side,
        (int File, int Rank)[] offsets, List<Move> moves)
    {
        foreach (var (fileOffset, rankOffset) in offsets)
        {
            var file = from.File + fileOffset;
            var rank = from.Rank + rankOffset;
            if (!IsOnBoard(file, rank))
                continue;

            var target = new Square(file, rank);
            if (board.At(target) is not Piece occupant || occupant.Color != side)
                moves.Add(new Move(from, target));
        }
    }

    private static void AddSlidingMoves(
        Board board, Square from, PlayerColor side,
        (int File, int Rank)[] directions, List<Move> moves)
    {
        foreach (var (fileDirection, rankDirection) in directions)
        {
            var file = from.File + fileDirection;
            var rank = from.Rank + rankDirection;

            while (IsOnBoard(file, rank))
            {
                var target = new Square(file, rank);
                var occupant = board.At(target);

                if (occupant is null || occupant.Value.Color != side)
                    moves.Add(new Move(from, target));

                if (occupant is not null)
                    break;

                file += fileDirection;
                rank += rankDirection;
            }
        }
    }

    private static bool IsOnBoard(int file, int rank) =>
        file is >= 0 and < 8 && rank is >= 0 and < 8;

    
    public static IReadOnlyList<Move> Legal(
        Board board,
        PlayerColor side)
    {
        ArgumentNullException.ThrowIfNull(board);

        // F2 genera los movimientos pseudo-legales.
        var candidates = Generate(board, side);

        var legalMoves = new List<Move>();

        foreach (var move in candidates)
        {
            // El rey rival nunca puede ser capturado.
            var target = board.At(move.To);

            if (target is Piece { Type: PieceType.King })
                continue;

            // Simulamos el movimiento sin modificar
            // el tablero original.
            var nextBoard = board.WithMove(move);

            // Descartamos movimientos que dejan
            // a nuestro propio rey en jaque.
            if (CheckDetector.IsInCheck(nextBoard, side))
                continue;

            legalMoves.Add(move);
        }

        return legalMoves;
    }

}
