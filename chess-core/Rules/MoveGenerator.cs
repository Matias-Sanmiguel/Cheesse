using Chess.Core.Model;

namespace Chess.Core.Rules;

public static class MoveGenerator
{
    private static readonly IReadOnlyDictionary<PieceType, IPieceMoveRule> Rules =
        new Dictionary<PieceType, IPieceMoveRule>
        {
            [PieceType.Pawn] = new PawnMoveRule(),
            [PieceType.Knight] = new KnightMoveRule(),
            [PieceType.Bishop] = new BishopMoveRule(),
            [PieceType.Rook] = new RookMoveRule(),
            [PieceType.Queen] = new QueenMoveRule(),
            [PieceType.King] = new KingMoveRule(),
        };

    public static IReadOnlyList<Move> Generate(Board board, PlayerColor side)
    {
        ArgumentNullException.ThrowIfNull(board);
        var moves = new List<Move>();
        for (var index = 0; index < 64; index++)
        {
            var from = Square.FromIndex(index);
            if (board.At(from) is Piece piece && piece.Color == side &&
                Rules.TryGetValue(piece.Type, out var rule))
                rule.AddMoves(board, from, side, moves);
        }
        return moves;
    }

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
