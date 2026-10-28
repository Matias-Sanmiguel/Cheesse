using Chess.Core.Model;
using Cheesse.Game.Adapters.Ui;
using Godot;

namespace Cheesse.Game.Adapters.Input;

public partial class InputAdapter : Node
{
    private ChessGame? _game;
    private BoardView? _board;
    private Action<Move>? _moveRequested;
    private Func<bool>? _canMove;
    private Square? _selected;

    public override void _Ready() => SetProcessUnhandledInput(false);

    public void Attach(ChessGame game, BoardView board, Action<Move> moveRequested, Func<bool> canMove)
    {
        _game = game;
        _board = board;
        _moveRequested = moveRequested;
        _canMove = canMove;
        _selected = null;
        board.SelectSquare(null);
        SetProcessUnhandledInput(true);
    }

    public void ClearSelection()
    {
        _selected = null;
        _board?.SelectSquare(null);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse ||
            _game is null || _board is null || !_board.Visible || _canMove?.Invoke() != true)
            return;

        var local = _board.GetGlobalTransformWithCanvas().AffineInverse() * mouse.Position;
        if (!_board.TryGetSquare(local, out var square))
            return;

        if (_selected is Square from)
        {
            var candidates = _game.LegalMoves().Where(move => move.From == from && move.To == square).ToArray();
            if (candidates.Length > 0)
            {
                // Until a promotion picker exists, promote to a queen.
                var move = candidates.FirstOrDefault(candidate => candidate.Promotion == PieceType.Queen,
                    candidates[0]);
                _selected = null;
                _board.SelectSquare(null);
                _moveRequested?.Invoke(move);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        _selected = _game.Board.At(square) is Piece piece && piece.Color == _game.Turn ? square : null;
        _board.SelectSquare(_selected);
        GetViewport().SetInputAsHandled();
    }
}
