using Chess.Core.Events;
using Chess.Core.Ports;
using Chess.Core.Rules;
using Chess.Core.Services;

namespace Chess.Core.Model;

public sealed class ChessGame
{
    private readonly List<Move> _history = [];
    private readonly List<Board> _positions = [];
    private readonly List<ClockSnapshot> _clockHistory = [];
    private ClockSnapshot _clockState;
    private double _clockAnchor;
    private PlayerColor? _winner;

    public ChessGame(IEventPublisher events, IGameRepository repository, IClock clock)
        : this(events, repository, clock, FenSerializer.Load(FenSerializer.InitialPosition)) { }

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
        ClockHistory = _clockHistory.AsReadOnly();
        _positions.Add(initialBoard);
        _clockHistory.Add(default);
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
    public PlayerColor? Winner => Status == GameStatus.Checkmate ? Opponent(Turn) : _winner;
    public PlayerColor? DrawOfferedBy { get; private set; }
    public TimeControl TimeControl { get; private set; }
    public ClockSnapshot ClockState => _clockState;
    public IReadOnlyList<ClockSnapshot> ClockHistory { get; }
    public IReadOnlyList<Move> History { get; }
    public GameCaretaker Caretaker { get; }
    public IGameState State => GameState.For(Status);
    internal IEnumerable<Board> Positions => _positions;

    public void ConfigureClock(TimeControl control)
    {
        ValidateControl(control);
        TimeControl = control;
        _clockState = new ClockSnapshot(control.InitialSeconds, control.InitialSeconds,
            control.InitialSeconds > 0 && State.AcceptsMoves);
        _clockAnchor = Clock.ElapsedSeconds;
        for (var ply = 0; ply < _clockHistory.Count; ply++) _clockHistory[ply] = _clockState;
        Caretaker.Reset(CreateMemento());
    }

    public void Tick()
    {
        var now = Clock.ElapsedSeconds;
        if (_clockState.Running && State.AcceptsMoves)
        {
            var elapsed = Math.Max(0, now - _clockAnchor);
            _clockState = Turn == PlayerColor.White
                ? _clockState with { WhiteSeconds = Math.Max(0, _clockState.WhiteSeconds - elapsed) }
                : _clockState with { BlackSeconds = Math.Max(0, _clockState.BlackSeconds - elapsed) };
            _clockAnchor = now;
            if ((Turn == PlayerColor.White ? _clockState.WhiteSeconds : _clockState.BlackSeconds) <= 0)
            {
                var winner = Opponent(Turn);
                var canMate = GameRules.CanPossiblyMate(Board, winner);
                End(canMate ? GameStatus.Timeout : GameStatus.DrawByTimeout, canMate ? winner : null);
            }
            UpdateClockHistory();
        }
        else _clockAnchor = now;
    }

    public void PauseClock()
    {
        Tick();
        _clockState = _clockState with { Running = false };
        UpdateClockHistory();
    }

    public void ResumeClock()
    {
        if (_clockState.Running)
        {
            Tick();
            return;
        }
        _clockAnchor = Clock.ElapsedSeconds;
        _clockState = _clockState with { Running = TimeControl.InitialSeconds > 0 && State.AcceptsMoves };
        UpdateClockHistory();
        Tick();
    }

    public void RestoreClock(TimeControl control, ClockSnapshot snapshot)
    {
        ValidateControl(control);
        ValidateSnapshot(control, snapshot);
        TimeControl = control;
        _clockState = snapshot with { Running = snapshot.Running && State.AcceptsMoves };
        _clockAnchor = Clock.ElapsedSeconds;
        UpdateClockHistory();
    }

    public void RestoreClockHistory(IReadOnlyList<ClockSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        if (snapshots.Count != _history.Count + 1)
            throw new ArgumentException("Clock history must contain one snapshot per position.", nameof(snapshots));
        foreach (var snapshot in snapshots) ValidateSnapshot(TimeControl, snapshot);
        var restored = snapshots.ToArray();
        _clockHistory.Clear();
        _clockHistory.AddRange(restored);
        _clockHistory[^1] = _clockState;
        Caretaker.Reset(CreateMemento());
    }

    private static void ValidateControl(TimeControl control)
    {
        if (control.InitialSeconds < 0 || control.IncrementSeconds < 0 ||
            control.InitialSeconds == 0 && control.IncrementSeconds != 0)
            throw new ArgumentOutOfRangeException(nameof(control));
    }

    private static void ValidateSnapshot(TimeControl control, ClockSnapshot snapshot)
    {
        // Bound corrupt saves while allowing any supported control and number of increments.
        if (!double.IsFinite(snapshot.WhiteSeconds) || !double.IsFinite(snapshot.BlackSeconds) ||
            snapshot.WhiteSeconds < 0 || snapshot.BlackSeconds < 0 ||
            snapshot.WhiteSeconds > (double)int.MaxValue * (int.MaxValue + 1d) ||
            snapshot.BlackSeconds > (double)int.MaxValue * (int.MaxValue + 1d) ||
            control.InitialSeconds == 0 && (snapshot.WhiteSeconds != 0 || snapshot.BlackSeconds != 0 || snapshot.Running))
            throw new ArgumentOutOfRangeException(nameof(snapshot));
    }

    private void UpdateClockHistory() => _clockHistory[^1] = _clockState;

    public bool TryMove(Move move) => Caretaker.Execute(move);

    internal bool ApplyMove(Move move)
    {
        Tick();
        if (!State.AcceptsMoves || !MoveGenerator.Legal(Board, Turn).Contains(move)) return false;
        var mover = Turn;
        var piece = Board.At(move.From)!.Value;
        if (DrawOfferedBy == Opponent(mover)) DrawOfferedBy = null;
        if (TimeControl.InitialSeconds > 0)
            _clockState = mover == PlayerColor.White
                ? _clockState with { WhiteSeconds = _clockState.WhiteSeconds + TimeControl.IncrementSeconds }
                : _clockState with { BlackSeconds = _clockState.BlackSeconds + TimeControl.IncrementSeconds };
        Board = Board.WithMove(move);
        _positions.Add(Board);
        _history.Add(move);
        Status = GameRules.Evaluate(this);
        _winner = null;
        if (!State.AcceptsMoves)
        {
            _clockState = _clockState with { Running = false };
            DrawOfferedBy = null;
        }
        _clockHistory.Add(_clockState);
        Events.Publish(new PieceMoved(move, piece));
        if (Status is GameStatus.Check or GameStatus.Checkmate) Events.Publish(new CheckRaised(Turn));
        if (!State.AcceptsMoves) Events.Publish(new GameEnded(Status));
        PublishDrawEvent();
        if (move.Promotion is PieceType promotedTo) Events.Publish(new PromotionOccurred(move.To, promotedTo));
        return true;
    }

    public bool Resign(PlayerColor side)
    {
        Tick();
        if (!State.AcceptsMoves || !Enum.IsDefined(side)) return false;
        var opponent = Opponent(side);
        var canMate = GameRules.CanPossiblyMate(Board, opponent);
        End(canMate ? GameStatus.Resigned : GameStatus.DrawByResignation, canMate ? opponent : null);
        return true;
    }

    public bool OfferDraw(PlayerColor side)
    {
        Tick();
        if (!State.AcceptsMoves || !Enum.IsDefined(side) || DrawOfferedBy is not null) return false;
        DrawOfferedBy = side;
        return true;
    }

    public bool AcceptDraw(PlayerColor side)
    {
        Tick();
        if (!State.AcceptsMoves || !Enum.IsDefined(side) || DrawOfferedBy != Opponent(side)) return false;
        End(GameStatus.DrawByAgreement);
        return true;
    }

    public bool DeclineDraw(PlayerColor side)
    {
        Tick();
        if (!State.AcceptsMoves || !Enum.IsDefined(side) || DrawOfferedBy != Opponent(side)) return false;
        DrawOfferedBy = null;
        return true;
    }

    public bool CanClaimDraw => State.AcceptsMoves && GameRules.ClaimStatus(Board, _positions) is not null;

    public IReadOnlyList<Move> ClaimableDrawMoves() => State.AcceptsMoves
        ? LegalMoves().Where(move => ClaimStatusAfter(move) is not null).ToArray()
        : [];

    private GameStatus? ClaimStatusAfter(Move move)
    {
        var board = Board.WithMove(move);
        return GameRules.ClaimStatus(board, _positions.Append(board));
    }

    public bool ClaimDraw(PlayerColor side, Move? announcedMove = null)
    {
        Tick();
        if (!State.AcceptsMoves || side != Turn) return false;
        var board = Board;
        IEnumerable<Board> positions = _positions;
        if (announcedMove is Move move)
        {
            if (!MoveGenerator.Legal(board, Turn).Contains(move)) return false;
            board = board.WithMove(move);
            positions = positions.Append(board);
        }
        if (GameRules.ClaimStatus(board, positions) is not GameStatus status) return false;
        End(status);
        return true;
    }

    private void End(GameStatus status, PlayerColor? winner = null)
    {
        Status = status;
        _winner = winner;
        DrawOfferedBy = null;
        _clockState = _clockState with { Running = false };
        UpdateClockHistory();
        Events.Publish(new GameEnded(status));
        PublishDrawEvent();
    }

    private void PublishDrawEvent()
    {
        if (Status is GameStatus.DrawByRepetition or GameStatus.DrawByFivefoldRepetition)
            Events.Publish(new DrawByRepetition(GameRules.PositionKey(Board, Turn)));
        if (Status is GameStatus.DrawByFiftyMove or GameStatus.DrawBySeventyFiveMove)
            Events.Publish(new FiftyMoveDraw());
    }

    public void SetImportedResult(string result)
    {
        var expected = Winner is PlayerColor winner ? winner == PlayerColor.White ? "1-0" : "0-1"
            : State.AcceptsMoves ? "*" : "1/2-1/2";
        if (result is not ("1-0" or "0-1" or "1/2-1/2" or "*")) throw new FormatException("Invalid PGN result.");
        if (!State.AcceptsMoves)
        {
            if (result != expected) throw new FormatException("PGN result conflicts with the final position.");
            return;
        }
        if (result == "*") return;
        if (result == "1/2-1/2") End(GameStatus.DrawByAgreement);
        else
        {
            var importedWinner = result == "1-0" ? PlayerColor.White : PlayerColor.Black;
            if (!GameRules.CanPossiblyMate(Board, importedWinner))
                throw new FormatException("PGN winner cannot possibly mate in the final position.");
            End(GameStatus.Resigned, importedWinner);
        }
    }

    public void RestoreOutcome(GameStatus status, PlayerColor? winner, PlayerColor? drawOfferedBy = null)
    {
        if (!Enum.IsDefined(status) || winner is PlayerColor color && !Enum.IsDefined(color) ||
            drawOfferedBy is PlayerColor offer && !Enum.IsDefined(offer))
            throw new ArgumentException("Invalid saved outcome.");
        var natural = GameRules.Evaluate(this);
        var manual = status is GameStatus.Resigned or GameStatus.Timeout or GameStatus.DrawByTimeout or GameStatus.DrawByAgreement or GameStatus.DrawByResignation;
        var claimed = status is GameStatus.DrawByFiftyMove or GameStatus.DrawByRepetition;
        if (status != natural && !(GameState.For(natural).AcceptsMoves && (manual || claimed &&
                (GameRules.ClaimStatus(Board, _positions) == status || ClaimableDrawMoves().Any(move => ClaimStatusAfter(move) == status)))))
            throw new ArgumentException("Saved outcome conflicts with the position.");
        var expectedWinner = status == GameStatus.Checkmate ? Opponent(Turn) : (PlayerColor?)null;
        if (status is GameStatus.Resigned or GameStatus.Timeout)
        {
            if (winner is null) throw new ArgumentException("Winning outcome requires a winner.");
            if (!GameRules.CanPossiblyMate(Board, winner.Value))
                throw new ArgumentException("Saved winner cannot possibly mate.");
            if (status == GameStatus.Timeout && winner != Opponent(Turn))
                throw new ArgumentException("Invalid timeout winner.");
        }
        else if (winner != expectedWinner) throw new ArgumentException("Saved winner conflicts with outcome.");
        if (status is GameStatus.Timeout or GameStatus.DrawByTimeout &&
            (TimeControl.InitialSeconds == 0 || (Turn == PlayerColor.White ? _clockState.WhiteSeconds : _clockState.BlackSeconds) != 0))
            throw new ArgumentException("Saved timeout requires an expired clock.");
        if (status == GameStatus.DrawByResignation && GameRules.CanPossiblyMate(Board, PlayerColor.White) &&
            GameRules.CanPossiblyMate(Board, PlayerColor.Black))
            throw new ArgumentException("Both sides can mate in the saved drawn resignation.");
        if (status == GameStatus.DrawByTimeout && GameRules.CanPossiblyMate(Board, Opponent(Turn)))
            throw new ArgumentException("Opponent can mate in the saved timeout position.");
        if (drawOfferedBy is not null && !GameState.For(status).AcceptsMoves)
            throw new ArgumentException("Ended game cannot contain a draw offer.");
        Status = status;
        _winner = winner;
        DrawOfferedBy = drawOfferedBy;
        if (!State.AcceptsMoves) _clockState = _clockState with { Running = false };
        UpdateClockHistory();
    }

    public void RestoreFrom(ChessGame source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!ReferenceEquals(this, source)) Restore(source.CreateMemento());
    }

    public GameMemento CreateMemento()
    {
        Tick();
        return new(InitialBoard, Board, Status, History, _positions, TimeControl, _clockState, _clockHistory, Winner, DrawOfferedBy);
    }

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
        _winner = memento.Winner;
        DrawOfferedBy = memento.DrawOfferedBy;
        TimeControl = memento.TimeControl;
        _clockState = memento.ClockState;
        _clockAnchor = Clock.ElapsedSeconds;
        _history.Clear();
        _history.AddRange(memento.History);
        _positions.Clear();
        _positions.AddRange(memento.Positions);
        _clockHistory.Clear();
        _clockHistory.AddRange(memento.ClockHistory);
    }

    private static PlayerColor Opponent(PlayerColor side) => side == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;

    public bool Undo() => Caretaker.Undo();
    public bool Redo() => Caretaker.Redo();
    public IReadOnlyList<Move> LegalMoves() => State.AcceptsMoves ? MoveGenerator.Legal(Board, Turn) : [];
}
