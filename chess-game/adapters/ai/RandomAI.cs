using Chess.Core.Model;
using Chess.Core.Ports;

namespace Cheesse.Game.Adapters.Ai;

public sealed class RandomAI : IAIPlayer
{
    private readonly Random _random;

    public RandomAI() => _random = Random.Shared;
    public RandomAI(int seed) => _random = new Random(seed);

    public Move ChooseMove(ChessGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var moves = game.LegalMoves();
        if (moves.Count == 0)
            throw new InvalidOperationException("No hay movimientos legales disponibles.");
        return moves[_random.Next(moves.Count)];
    }
}
