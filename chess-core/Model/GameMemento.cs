namespace Chess.Core.Model;

public sealed class GameMemento
{
    internal GameMemento(Board initialBoard, Board board, GameStatus status,
        IEnumerable<Move> history, IEnumerable<Board> positions)
    {
        InitialBoard = initialBoard;
        Board = board;
        Status = status;
        History = Array.AsReadOnly(history.ToArray());
        Positions = Array.AsReadOnly(positions.ToArray());
    }

    public Board InitialBoard { get; }
    public Board Board { get; }
    public GameStatus Status { get; }
    public IReadOnlyList<Move> History { get; }
    internal IReadOnlyList<Board> Positions { get; }
}
