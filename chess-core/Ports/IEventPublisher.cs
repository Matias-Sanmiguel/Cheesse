namespace Chess.Core.Ports;

public interface IEventPublisher
{
    void Publish<T>(T domainEvent);
}
