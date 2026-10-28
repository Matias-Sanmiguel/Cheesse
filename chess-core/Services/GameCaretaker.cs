using Chess.Core.Model;

namespace Chess.Core.Services;

public sealed class GameCaretaker
{
    private readonly ChessGame _game;
    private readonly Stack<MoveCommand> _undo = new();
    private readonly Stack<MoveCommand> _redo = new();

    public GameCaretaker(ChessGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        _game = game;
        for (var ply = 0; ply < game.History.Count; ply++)
            _undo.Push(new MoveCommand(game, game.History[ply], ply));
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public bool Execute(Move move)
    {
        var command = new MoveCommand(_game, move);
        if (!command.Execute())
            return false;

        _undo.Push(command);
        _redo.Clear();
        return true;
    }

    public bool Undo()
    {
        if (!_undo.TryPeek(out var command) || !command.Undo())
            return false;

        _undo.Pop();
        _redo.Push(command);
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPeek(out var command) || !command.Execute())
            return false;

        _redo.Pop();
        _undo.Push(command);
        return true;
    }
}
