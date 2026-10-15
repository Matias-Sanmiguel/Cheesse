
using Chess.Core.Model;
using Chess.Core.Rules;

namespace Chess.Core.Services;

public static class GameRules
{
    // Conservamos la firma original para ChessGame.
    public static GameStatus Evaluate(ChessGame game)
    {
        ArgumentNullException.ThrowIfNull(game);

        return Evaluate(game.Board, game.Turn);
    }

    // Evaluacion de la posicion, independiente de Godot.
    public static GameStatus Evaluate(
        Board board,
        PlayerColor side)
    {
        ArgumentNullException.ThrowIfNull(board);

        bool inCheck = CheckDetector.IsInCheck(
            board,
            side);

        var legalMoves = MoveGenerator.Legal(
            board,
            side);

        // Sin movimientos legales: mate o ahogado.
        if (legalMoves.Count == 0)
        {
            return inCheck
                ? GameStatus.Checkmate
                : GameStatus.Stalemate;
        }

        // Con movimientos legales: jaque o juego normal.
        return inCheck
            ? GameStatus.Check
            : GameStatus.Ongoing;
    }
}
