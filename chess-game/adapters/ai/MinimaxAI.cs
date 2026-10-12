using Chess.Core.Model;
using Chess.Core.Ports;

namespace Cheesse.Game.Adapters.Ai;

public sealed class MinimaxAI : IAIPlayer
{
    public Move ChooseMove(ChessGame game) =>
        throw new System.NotImplementedException("IA fuera del núcleo.");
}
