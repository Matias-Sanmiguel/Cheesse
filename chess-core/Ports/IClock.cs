namespace Chess.Core.Ports;

public interface IClock
{
    double ElapsedSeconds { get; }
}
