using Chess.Core.Model;
using Chess.Core.Rules;

namespace Chess.Core.Services;

public static class GameRules
{
    public static GameStatus Evaluate(ChessGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return Evaluate(game.Board, game.Turn, game.Positions);
    }

    public static GameStatus Evaluate(Board board, PlayerColor side) =>
        Evaluate(board, side, null);

    internal static GameStatus Evaluate(Board board, PlayerColor side, IEnumerable<Board>? positions)
    {
        ArgumentNullException.ThrowIfNull(board);
        var inCheck = CheckDetector.IsInCheck(board, side);
        var legalMoves = MoveGenerator.Legal(board, side);

        // Mate and stalemate take precedence over move-count draws.
        if (legalMoves.Count == 0)
            return inCheck ? GameStatus.Checkmate : GameStatus.Stalemate;

        if (HasInsufficientMaterial(board))
            return GameStatus.DrawByInsufficientMaterial;

        // CORE-F5 closes these draws without a separate claim interaction.
        if (board.HalfmoveClock >= 100)
            return GameStatus.DrawByFiftyMove;

        if (positions is not null)
        {
            var currentKey = PositionKey(board, side);
            if (positions.Count(position => PositionKey(position, position.Turn) == currentKey) >= 3)
                return GameStatus.DrawByRepetition;
        }

        return inCheck ? GameStatus.Check : GameStatus.Ongoing;
    }

    internal static string PositionKey(Board board, PlayerColor side)
    {
        var fields = FenSerializer.Write(board, side).Split(' ');
        // A nominal FEN target distinguishes positions only if a legal capture exists.
        var canCaptureEnPassant = SpecialMoves.EnPassant(board, side)
            .Any(move => !CheckDetector.IsInCheck(board.WithMove(move), side));
        if (!canCaptureEnPassant)
            fields[3] = "-";
        return string.Join(" ", fields.Take(4));
    }

    private static bool HasInsufficientMaterial(Board board)
    {
        var minors = new List<(PieceType Type, int SquareColor)>();
        var whiteKings = 0;
        var blackKings = 0;
        for (var index = 0; index < 64; index++)
        {
            var square = Square.FromIndex(index);
            if (board.At(square) is not Piece piece)
                continue;

            switch (piece.Type)
            {
                case PieceType.King:
                    if (piece.Color == PlayerColor.White) whiteKings++;
                    else blackKings++;
                    break;
                case PieceType.Bishop:
                case PieceType.Knight:
                    minors.Add((piece.Type, (square.File + square.Rank) % 2));
                    break;
                default:
                    return false;
            }
        }

        if (whiteKings != 1 || blackKings != 1)
            return false;

        // Bare kings, a single minor, or bishops confined to one square color.
        // Two knights and opposite-colored bishops can participate in a mate.
        return minors.Count <= 1 ||
            (minors.All(piece => piece.Type == PieceType.Bishop) &&
             minors.All(piece => piece.SquareColor == minors[0].SquareColor));
    }
}
