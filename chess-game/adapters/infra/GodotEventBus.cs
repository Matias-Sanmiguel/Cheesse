using Chess.Core.Ports;
using Godot;

namespace Cheesse.Game.Adapters.Infra;

public partial class GodotEventBus : Node, IEventPublisher
{
    [Signal]
    public delegate void PieceMovedEventHandler();

    [Signal]
    public delegate void CheckRaisedEventHandler();

    [Signal]
    public delegate void GameEndedEventHandler();

    [Signal]
    public delegate void PromotionOccurredEventHandler();

    public void Publish<T>(T domainEvent)
    {
        switch (domainEvent)
        {
            case Chess.Core.Events.PieceMoved:
                EmitSignal(SignalName.PieceMoved);
                break;
            case Chess.Core.Events.CheckRaised:
                EmitSignal(SignalName.CheckRaised);
                break;
            case Chess.Core.Events.GameEnded:
                EmitSignal(SignalName.GameEnded);
                break;
            case Chess.Core.Events.PromotionOccurred:
                EmitSignal(SignalName.PromotionOccurred);
                break;
        }
    }
}
