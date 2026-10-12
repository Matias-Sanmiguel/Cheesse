using Chess.Core.Model;
using Chess.Core.Ports;

namespace Cheesse.Game.Adapters.Persist;

public sealed class FileSaveRepository : IGameRepository
{
    public void Save(ChessGame game) =>
        throw new System.NotImplementedException("Persistencia fuera del núcleo.");

    public ChessGame Load(string id) =>
        throw new System.NotImplementedException("Persistencia fuera del núcleo.");
}
