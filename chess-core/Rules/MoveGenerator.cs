using Chess.Core.Model;

namespace Chess.Core.Rules;

public static class MoveGenerator
{
    public static IReadOnlyList<Move> Generate(Board board, PlayerColor side) =>
        throw new NotImplementedException("CORE-F2");

    
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
