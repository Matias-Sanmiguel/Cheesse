using Chess.Core.Model;

namespace Chess.Core.Ports;

public interface IAIPlayer
{
    Move ChooseMove(ChessGame game);
}
