using Chess.Core.Model;

namespace Chess.Core.Services;

public sealed class PlayMoveCommand : ICommand
{
    private readonly ChessGame _game;
    private readonly GameMemento _before;
    private GameMemento? _after;

    public PlayMoveCommand(ChessGame game, Move move)
        : this(game, move, game?.CreateMemento() ?? throw new ArgumentNullException(nameof(game))) { }

    internal PlayMoveCommand(ChessGame game, Move move, GameMemento before, GameMemento? after = null)
    {
        _game = game;
        Move = move;
        _before = before;
        _after = after;
    }

    public Move Move { get; }

    public bool Execute() => _game.Caretaker.Execute(this);

    internal bool ExecuteCore()
    {
        if (!ReferenceEquals(_game.InitialBoard, _before.InitialBoard) || !_game.History.SequenceEqual(_before.History) ||
            !_game.ApplyMove(Move))
            return false;
        if (_after is not null) _game.RestorePosition(_after);
        else _after = _game.CreateMemento();
        return true;
    }

    public bool Undo() => _game.Caretaker.Undo(this);

    internal bool UndoCore()
    {
        if (_after is null || !ReferenceEquals(_game.InitialBoard, _before.InitialBoard) ||
            !_game.History.SequenceEqual(_before.History.Append(Move)))
            return false;
        _after = _game.CreateMemento();
        _game.RestorePosition(_before);
        return true;
    }
}
