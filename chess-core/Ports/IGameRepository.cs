using Chess.Core.Model;

namespace Chess.Core.Ports;

public interface IGameRepository
{
    void Save(ChessGame game);

    /// <summary>Restores the open game; returns false without changing it when loading fails.</summary>
    bool Load(ChessGame game);
}
