using Chess.Core.Events;
using Chess.Core.Ports;
using Chess.Core.Rules;
using Chess.Core.Services;

namespace Chess.Core.Model;

public sealed class ChessGame
{
    private readonly List<Move> _history = [];
    private readonly List<(Board Board, GameStatus Status)> _snapshots = [];

    public ChessGame(IEventPublisher events, IGameRepository repository, IClock clock)
        : this(events, repository, clock, FenSerializer.Load(FenSerializer.InitialPosition))
    {
    }

    public ChessGame(IEventPublisher events, IGameRepository repository, IClock clock, Board initialBoard)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(initialBoard);

        Events = events;
        Repository = repository;
        Clock = clock;
        InitialBoard = initialBoard;
        Board = initialBoard;
        History = _history.AsReadOnly();
        Status = GameRules.Evaluate(this);
    }

    public IEventPublisher Events { get; }

    public IGameRepository Repository { get; }

    public IClock Clock { get; }

    public Board InitialBoard { get; }

    public Board Board { get; private set; }

    public PlayerColor Turn => Board.Turn;

    public GameStatus Status { get; private set; }

    public IReadOnlyList<Move> History { get; }

    internal IEnumerable<Board> Positions => _snapshots.Select(snapshot => snapshot.Board).Append(Board);

    public bool TryMove(Move move)
    {
        if (IsFinished || !MoveGenerator.Legal(Board, Turn).Contains(move))
            return false;

        var moving = Board.At(move.From)!.Value;
        _snapshots.Add((Board, Status));
        Board = Board.WithMove(move);
        _history.Add(move);
        Status = GameRules.Evaluate(this);

        Events.Publish(new PieceMoved(move, moving));

        if (Status is GameStatus.Check or GameStatus.Checkmate)
            Events.Publish(new CheckRaised(Turn));

        if (IsFinished)
            Events.Publish(new GameEnded(Status));

        if (Status == GameStatus.DrawByRepetition)
            Events.Publish(new DrawByRepetition(GameRules.PositionKey(Board, Turn)));

        if (Status == GameStatus.DrawByFiftyMove)
            Events.Publish(new FiftyMoveDraw());

        if (move.Promotion is PieceType promoted)
            Events.Publish(new PromotionOccurred(move.To, promoted));

        return true;
    }

    public bool Undo()
    {
        if (_history.Count == 0)
            return false;

        var previous = _snapshots[^1];
        Board = previous.Board;
        Status = previous.Status;
        _snapshots.RemoveAt(_snapshots.Count - 1);
        _history.RemoveAt(_history.Count - 1);
        return true;
    }

    public IReadOnlyList<Move> LegalMoves() =>
        IsFinished ? [] : MoveGenerator.Legal(Board, Turn);

    private bool IsFinished => Status is not (GameStatus.Ongoing or GameStatus.Check);
}
