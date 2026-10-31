using Chess.Core.Events;
using Chess.Core.Ports;
using Chess.Core.Rules;
using Chess.Core.Services;

namespace Chess.Core.Model;

public sealed class ChessGame
{
    private readonly List<Move> _history = [];
    private readonly List<Board> _positions = [];

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
        _positions.Add(initialBoard);
        Status = GameRules.Evaluate(this);
        Caretaker = new GameCaretaker(this);
    }

    public IEventPublisher Events { get; }

    public IGameRepository Repository { get; }

    public IClock Clock { get; }

    public Board InitialBoard { get; private set; }

    public Board Board { get; private set; }

    public PlayerColor Turn => Board.Turn;

    public GameStatus Status { get; private set; }

    public IReadOnlyList<Move> History { get; }

    public GameCaretaker Caretaker { get; }

    public IGameState State => GameState.For(Status);

    internal IEnumerable<Board> Positions => _positions;

    public bool TryMove(Move move) => Caretaker.Execute(move);

    internal bool ApplyMove(Move move)
    {
        if (!State.AcceptsMoves || !MoveGenerator.Legal(Board, Turn).Contains(move))
            return false;

        var piece = Board.At(move.From)!.Value;
        Board = Board.WithMove(move);
        _positions.Add(Board);
        _history.Add(move);
        Status = GameRules.Evaluate(this);
        Events.Publish(new PieceMoved(move, piece));
        if (Status is GameStatus.Check or GameStatus.Checkmate)
            Events.Publish(new CheckRaised(Turn));
        if (!State.AcceptsMoves)
            Events.Publish(new GameEnded(Status));
        if (Status == GameStatus.DrawByRepetition)
            Events.Publish(new DrawByRepetition(GameRules.PositionKey(Board, Turn)));
        if (Status == GameStatus.DrawByFiftyMove)
            Events.Publish(new FiftyMoveDraw());
        if (move.Promotion is PieceType promotedTo)
            Events.Publish(new PromotionOccurred(move.To, promotedTo));
        return true;
    }

    /// <summary>Copies an already reconstructed game without replacing this instance or its ports.</summary>
    public void RestoreFrom(ChessGame source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (ReferenceEquals(this, source))
            return;

        Restore(source.CreateMemento());
    }

    public GameMemento CreateMemento() => new(InitialBoard, Board, Status, History, _positions);

    public void Restore(GameMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);
        RestorePosition(memento);
        Caretaker.Reset(memento);
    }

    internal void RestorePosition(GameMemento memento)
    {
        InitialBoard = memento.InitialBoard;
        Board = memento.Board;
        Status = memento.Status;
        _history.Clear();
        _history.AddRange(memento.History);
        _positions.Clear();
        _positions.AddRange(memento.Positions);
    }

    public bool Undo() => Caretaker.Undo();
    public bool Redo() => Caretaker.Redo();

    public IReadOnlyList<Move> LegalMoves() =>
        State.AcceptsMoves ? MoveGenerator.Legal(Board, Turn) : [];
}
