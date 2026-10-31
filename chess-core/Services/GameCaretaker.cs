using Chess.Core.Model;

namespace Chess.Core.Services;

public sealed class GameCaretaker
{
    private readonly ChessGame _game;
    private readonly Stack<PlayMoveCommand> _undo;
    private readonly Stack<PlayMoveCommand> _redo;

    public GameCaretaker(ChessGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        _game = game;
        // All adapters for a game share its one undo/redo history.
        _undo = game.Caretaker?._undo ?? new();
        _redo = game.Caretaker?._redo ?? new();
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public bool Execute(Move move) => Execute(new PlayMoveCommand(_game, move));

    internal bool Execute(PlayMoveCommand command)
    {
        if (!command.ExecuteCore()) return false;
        _undo.Push(command);
        _redo.Clear();
        return true;
    }

    public bool Undo()
    {
        if (!_undo.TryPeek(out var command) || !command.UndoCore()) return false;
        _undo.Pop();
        _redo.Push(command);
        return true;
    }

    internal bool Undo(PlayMoveCommand expected) =>
        _undo.TryPeek(out var command) && ReferenceEquals(command, expected) && Undo();

    public bool Redo()
    {
        if (!_redo.TryPeek(out var command) || !command.ExecuteCore()) return false;
        _redo.Pop();
        _undo.Push(command);
        return true;
    }

    internal void Reset(GameMemento memento)
    {
        _undo.Clear();
        _redo.Clear();
        for (var ply = 0; ply < memento.History.Count; ply++)
        {
            var board = memento.Positions[ply];
            var positions = memento.Positions.Take(ply + 1).ToArray();
            var before = new GameMemento(memento.InitialBoard, board,
                GameRules.Evaluate(board, board.Turn, positions), memento.History.Take(ply), positions);
            _undo.Push(new PlayMoveCommand(_game, memento.History[ply], before, memento.Positions[ply + 1]));
        }
    }
}
