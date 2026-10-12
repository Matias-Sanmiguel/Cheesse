using Chess.Core.Model;

namespace Chess.Core.Ports;

public interface IRemoteSession
{
    void SendCommand(Move move);
}
