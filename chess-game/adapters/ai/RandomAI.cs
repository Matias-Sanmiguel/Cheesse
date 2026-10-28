using Chess.Core.Model;
using Chess.Core.Ports;

namespace Cheesse.Game.Adapters.Ai;

public sealed class RandomAI : IAIPlayer
{
    public Move ChooseMove(ChessGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var moves = game.LegalMoves();
        if (moves.Count == 0)
            throw new InvalidOperationException("No hay movimientos legales disponibles.");
        return moves[System.Random.Shared.Next(moves.Count)];
    }
}
