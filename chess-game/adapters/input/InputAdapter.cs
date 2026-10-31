
using Chess.Core.Model;
using Cheesse.Game.Adapters.Ui;
using Godot;

namespace Cheesse.Game.Adapters.Input;

public partial class InputAdapter : Node
{
    private ChessGame? _game;
    private BoardView? _board;
    private PromotionDialog? _promotionDialog;
    private Action<Move>? _moveRequested;
    private Func<bool>? _canMove;
    private Square? _selected;
    private Move[]? _pendingPromotions;

    public override void _Ready() =>
        SetProcessUnhandledInput(false);

    public void Attach(ChessGame game, BoardView board, PromotionDialog promotionDialog, Action<Move> moveRequested, Func<bool> canMove)
    {
        if (_promotionDialog is not null)
        {
            _promotionDialog.PieceChosen -= OnPieceChosen;
            _promotionDialog.Cancelled -= OnCancelled;
        }

        _game = game;
        _board = board;
        _promotionDialog = promotionDialog;
        _moveRequested = moveRequested;
        _canMove = canMove;

        promotionDialog.PieceChosen += OnPieceChosen;
        promotionDialog.Cancelled += OnCancelled;

        ClearSelection();
        SetProcessUnhandledInput(true);
    }

    public void ClearSelection()
    {
        _selected = null;
        _pendingPromotions = null;
        _promotionDialog?.Hide();
        _board?.ClearSelection();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (_promotionDialog?.Visible == true)
            return;

        if (input is not InputEventMouseButton
            {
                Pressed: true,
                ButtonIndex: MouseButton.Left
            } mouse || _game is null || _board is null || !_board.Visible || _canMove?.Invoke() != true)
        {
            return;
        }

        var local = _board.GetGlobalTransformWithCanvas()
            .AffineInverse() * mouse.Position;

        if (!_board.TryGetSquare(local, out var square))
        {
            ClearSelection();
            return;
        }

        if (_selected is Square from)
        {
            var candidates = _game.LegalMoves().Where(move => move.From == from && move.To == square).ToArray();

            if (candidates.Length > 0)
            {
                if (candidates.Length > 1 && candidates.Any(move => move.Promotion == PieceType.Queen))
                {
                    _pendingPromotions = candidates;
                    _promotionDialog?.OpenDialog();
                }
                else
                {
                    _moveRequested?.Invoke(candidates[0]);
                }

                _selected = null;
                _board.ClearSelection();
                GetViewport().SetInputAsHandled();
                return;
            }

            ClearSelection();
            GetViewport().SetInputAsHandled();
            return;
        }

        _selected = _game.Board.At(square) is Piece piece &&piece.Color == _game.Turn ? square : null;

        _board.SetSelection(_selected, _game.LegalMoves()
            .Where(move => move.From == _selected).Select(move => move.To));
        GetViewport().SetInputAsHandled();
    }

    private void OnPieceChosen(int pieceType)
    {
        if (_pendingPromotions is null)
            return;

        var chosenPiece = (PieceType)pieceType;

        foreach (var move in _pendingPromotions)
        {
            if (move.Promotion != chosenPiece)
                continue;

            _pendingPromotions = null;

            if (_canMove?.Invoke() == true)
                _moveRequested?.Invoke(move);

            return;
        }

        _pendingPromotions = null;
    }

    private void OnCancelled()
    {
        _pendingPromotions = null;
    }

    public override void _ExitTree()
    {
        if (_promotionDialog is null)
            return;

        _promotionDialog.PieceChosen -= OnPieceChosen;
        _promotionDialog.Cancelled -= OnCancelled;
    }
}
