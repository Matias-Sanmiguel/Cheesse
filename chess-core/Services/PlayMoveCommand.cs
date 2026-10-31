using Chess.Core.Model;

namespace Chess.Core.Services;

public sealed class PlayMoveCommand : ICommand
{
    private readonly ChessGame _game;
    private readonly GameMemento _before;
    private Board? _after;

    public PlayMoveCommand(ChessGame game, Move move)
        : this(game, move, game?.CreateMemento() ?? throw new ArgumentNullException(nameof(game))) { }

    internal PlayMoveCommand(ChessGame game, Move move, GameMemento before, Board? after = null)
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
        _after = _game.Board;
        return true;
    }

    public bool Undo() => _game.Caretaker.Undo(this);

    internal bool UndoCore()
    {
        if (_after is null || !ReferenceEquals(_game.InitialBoard, _before.InitialBoard) ||
            !_game.History.SequenceEqual(_before.History.Append(Move)))
            return false;
        _game.RestorePosition(_before);
        _after = null;
        return true;
    }
}
