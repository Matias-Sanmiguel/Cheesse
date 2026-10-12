using Chess.Core.Model;

namespace Chess.Core.Ports;

public interface IGameRepository
{
    void Save(ChessGame game);

    ChessGame Load(string id);
}
