namespace Chess.Core.Model;

public sealed class GameMemento
{
    internal GameMemento(Board initialBoard, Board board, GameStatus status,
        IEnumerable<Move> history, IEnumerable<Board> positions, GameMode mode)
    {
        InitialBoard = initialBoard;
        Board = board;
        Status = status;
        Mode = mode;
        History = Array.AsReadOnly(history.ToArray());
        Positions = Array.AsReadOnly(positions.ToArray());
    }

    public Board InitialBoard { get; }
    public Board Board { get; }
    public GameStatus Status { get; }
    public GameMode Mode { get; }
    public IReadOnlyList<Move> History { get; }
    internal IReadOnlyList<Board> Positions { get; }
}
