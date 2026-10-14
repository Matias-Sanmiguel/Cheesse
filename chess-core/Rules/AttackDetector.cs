using Chess.Core.Model;

namespace Chess.Core.Rules;

public static class AttackDetector
{
    public static bool IsAttacked(Board board, Square square, PlayerColor by)
    {
        for (var index = 0; index < 64; index++)
        {
            var from = Square.FromIndex(index);
            if (board.At(from) is not Piece piece || piece.Color != by || from == square)
                continue;

            var fileDistance = Math.Abs(square.File - from.File);
            var rankDistance = Math.Abs(square.Rank - from.Rank);

            var attacks = piece.Type switch
            {
                PieceType.Pawn => fileDistance == 1 &&
                    square.Rank - from.Rank == (by == PlayerColor.White ? 1 : -1),
                PieceType.Knight => (fileDistance == 1 && rankDistance == 2) ||
                    (fileDistance == 2 && rankDistance == 1),
                PieceType.Bishop => fileDistance == rankDistance &&
                    HasClearPath(board, from, square),
                PieceType.Rook => (fileDistance == 0 || rankDistance == 0) &&
                    HasClearPath(board, from, square),
                PieceType.Queen => (fileDistance == rankDistance ||
                    fileDistance == 0 || rankDistance == 0) &&
                    HasClearPath(board, from, square),
                PieceType.King => fileDistance <= 1 && rankDistance <= 1,
                _ => false,
            };

            if (attacks)
                return true;
        }

        return false;
    }

    private static bool HasClearPath(Board board, Square from, Square to)
    {
        var fileStep = Math.Sign(to.File - from.File);
        var rankStep = Math.Sign(to.Rank - from.Rank);
        var file = from.File + fileStep;
        var rank = from.Rank + rankStep;

        // The first occupied target is attacked, even when it holds a friendly piece.
        while (file != to.File || rank != to.Rank)
        {
            if (board.At(new Square(file, rank)) is not null)
                return false;

            file += fileStep;
            rank += rankStep;
        }

        return true;
    }
}
