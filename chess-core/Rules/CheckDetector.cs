using Chess.Core.Model;

namespace Chess.Core.Rules;

public static class CheckDetector
{
    public static bool IsInCheck(Board board, PlayerColor side)
    {
        ArgumentNullException.ThrowIfNull(board);

        // Buscar el rey del jugador indicado.
        var kingSquare = FindKing(board, side);

        // Determinar el color del oponente.
        var opponent = side == PlayerColor.White
            ? PlayerColor.Black
            : PlayerColor.White;

        // Consultar las casillas atacadas por el rival.
        return AttackDetector.IsAttacked(
            board,
            kingSquare,
            opponent);
    }

    private static Square FindKing(Board board, PlayerColor side)
    {
        for (var index = 0; index < 64; index++)
        {
            var square = Square.FromIndex(index);
            var piece = board.At(square);

            if (piece is Piece p &&
                p.Type == PieceType.King &&
                p.Color == side)
            {
                return square;
            }
        }

        throw new InvalidOperationException(
            $"No se encontro el rey del jugador {side}.");
    }
}
