using Chess.Core.Model;

namespace Chess.Core.Services;

public sealed class MoveCommand
{
    private readonly ChessGame _game;
    private readonly int _ply;

    public MoveCommand(ChessGame game, Move move)
        : this(game, move, game?.History.Count ?? throw new ArgumentNullException(nameof(game)))
    {
    }

    internal MoveCommand(ChessGame game, Move move, int ply)
    {
        _game = game;
        Move = move;
        _ply = ply;
    }

    public Move Move { get; }

    public bool Execute() => _game.History.Count == _ply && _game.TryMove(Move);

    public bool Undo() =>
        _game.History.Count == _ply + 1 && _game.History[^1] == Move && _game.Undo();
}
