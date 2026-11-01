namespace Chess.Core.Model;

public sealed class GameMemento
{
    internal GameMemento(Board initialBoard, Board board, GameStatus status,
        IEnumerable<Move> history, IEnumerable<Board> positions, TimeControl timeControl,
        ClockSnapshot clockState, IEnumerable<ClockSnapshot> clockHistory,
        PlayerColor? winner = null, PlayerColor? drawOfferedBy = null)
    {
        InitialBoard = initialBoard;
        Board = board;
        Status = status;
        History = Array.AsReadOnly(history.ToArray());
        Positions = Array.AsReadOnly(positions.ToArray());
        TimeControl = timeControl;
        ClockState = clockState;
        ClockHistory = Array.AsReadOnly(clockHistory.ToArray());
        Winner = winner;
        DrawOfferedBy = drawOfferedBy;
    }

    public Board InitialBoard { get; }
    public Board Board { get; }
    public GameStatus Status { get; }
    public IReadOnlyList<Move> History { get; }
    public TimeControl TimeControl { get; }
    public ClockSnapshot ClockState { get; }
    public IReadOnlyList<ClockSnapshot> ClockHistory { get; }
    public PlayerColor? Winner { get; }
    public PlayerColor? DrawOfferedBy { get; }
    internal IReadOnlyList<Board> Positions { get; }
}
