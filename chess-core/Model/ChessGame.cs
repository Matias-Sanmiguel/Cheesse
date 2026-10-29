using Chess.Core.Events;
using Chess.Core.Ports;
using Chess.Core.Rules;
using Chess.Core.Services;

namespace Chess.Core.Model;

public sealed class ChessGame
{
    private readonly List<Move> _history = [];
    private readonly Stack<MoveCommand> _previousStates = [];
    private readonly Stack<MoveCommand> _undoneMoves = [];
    private readonly IReadOnlyList<Move> _readOnlyHistory;

    public ChessGame(IEventPublisher events, IGameRepository repository, IClock clock)
        : this(events, repository, clock, CreateInitialBoard(), PlayerColor.White)
    {
    }

    public ChessGame(
        IEventPublisher events,
        IGameRepository repository,
        IClock clock,
        Board board,
        PlayerColor turn)
    {
        Events = events ?? throw new ArgumentNullException(nameof(events));
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Board = board ?? throw new ArgumentNullException(nameof(board));
        Turn = turn;
        Status = GameRules.Evaluate(Board, Turn);
        _readOnlyHistory = _history.AsReadOnly();
    }

    public IEventPublisher Events { get; }

    public IGameRepository Repository { get; }

    public IClock Clock { get; }

    public Board Board { get; private set; }

    public PlayerColor Turn { get; private set; } = PlayerColor.White;

    public GameStatus Status { get; private set; } = GameStatus.Ongoing;

    public IReadOnlyList<Move> History => _readOnlyHistory;

    public bool TryMove(Move move)
    {
        if (!LegalMoves().Contains(move))
            return false;

        var command = new MoveCommand(move, CaptureState());
        _undoneMoves.Clear();
        command.Execute(this);
        return true;
    }

    private void ApplyMove(MoveCommand command)
    {
        var move = command.Move;

        var piece = Board.At(move.From)!.Value;
        var nextBoard = Board.WithMove(move);
        var nextTurn = Turn == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
        var nextStatus = GameRules.Evaluate(nextBoard, nextTurn);

        // Guardamos el tablero inmutable completo, incluidos enroque y al paso.
        _previousStates.Push(command);
        _history.Add(move);
        Board = nextBoard;
        Turn = nextTurn;
        Status = nextStatus;

        // Los observadores reciben los eventos con la partida ya actualizada.
        Events.Publish(new PieceMoved(move, piece));

        if (piece.Type == PieceType.Pawn && move.Promotion is PieceType promotedTo)
            Events.Publish(new PromotionOccurred(move.To, promotedTo));

        if (Status is GameStatus.Check or GameStatus.Checkmate)
            Events.Publish(new CheckRaised(Turn));

        if (IsFinished)
            Events.Publish(new GameEnded(Status));

    }

    public bool Undo()
    {
        if (!_previousStates.TryPop(out var command))
            return false;

        command.Undo(this);
        _undoneMoves.Push(command);
        return true;
    }

    public bool Redo()
    {
        if (!_undoneMoves.TryPop(out var command))
            return false;

        command.Execute(this);
        return true;
    }

    public IReadOnlyList<Move> LegalMoves() =>
        IsFinished ? Array.Empty<Move>() : MoveGenerator.Legal(Board, Turn);

    private bool IsFinished => !GameState.For(Status).CanMove;

    private static Board CreateInitialBoard()
    {
        var board = Board.Empty().WithCastling(CastlingRights.All);
        PieceType[] backRank =
        [
            PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
            PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook,
        ];

        for (var file = 0; file < 8; file++)
        {
            board = board
                .WithPiece(new Square(file, 0), new Piece(backRank[file], PlayerColor.White))
                .WithPiece(new Square(file, 1), new Piece(PieceType.Pawn, PlayerColor.White))
                .WithPiece(new Square(file, 6), new Piece(PieceType.Pawn, PlayerColor.Black))
                .WithPiece(new Square(file, 7), new Piece(backRank[file], PlayerColor.Black));
        }

        return board;
    }

    private GameMemento CaptureState() => new(Board, Turn, Status);

    private readonly record struct GameMemento(Board Board, PlayerColor Turn, GameStatus Status);
    private sealed record MoveCommand(Move Move, GameMemento Before)
    {
        public void Execute(ChessGame game) => game.ApplyMove(this);

        public void Undo(ChessGame game)
        {
            game.Board = Before.Board;
            game.Turn = Before.Turn;
            game.Status = Before.Status;
            game._history.RemoveAt(game._history.Count - 1);
        }
    }
}
