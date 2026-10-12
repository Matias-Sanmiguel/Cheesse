using Chess.Core.Ports;

namespace Chess.Core.Model;

public sealed class ChessGame
{
    private readonly List<Move> _history = [];

    public ChessGame(IEventPublisher events, IGameRepository repository, IClock clock)
    {
        Events = events;
        Repository = repository;
        Clock = clock;
        Board = Board.Empty();
    }

    public IEventPublisher Events { get; }

    public IGameRepository Repository { get; }

    public IClock Clock { get; }

    public Board Board { get; private set; }

    public PlayerColor Turn { get; private set; } = PlayerColor.White;

    public GameStatus Status { get; private set; } = GameStatus.Ongoing;

    public IReadOnlyList<Move> History => _history;

    public bool TryMove(Move move) => false;

    public bool Undo() => false;

    public IReadOnlyList<Move> LegalMoves() => [];
}
