
using Chess.Core.Model;
using Chess.Core.Rules;
using Chess.Core.Services;
using Cheesse.Game.Adapters.Ui;
using Godot;

namespace Cheesse.Game.Adapters.Input;

public partial class InputAdapter : Node
{
    public event Action<string>? MoveRejected;
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

            if (_game.Mode == GameMode.Fondue &&
                _game.Board.At(square) is Piece friend && friend.Color == _game.Turn)
            {
                // Fondue: change the selected piece directly while exploring
                // which moves are legal in an indirect melting check.
                SelectOwnPiece(square);
            }
            else
            {
                if (_game.Mode == GameMode.Fondue)
                    MoveRejected?.Invoke(ExplainFondueRejection(from, square));
                ClearSelection();
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        SelectOwnPiece(square);
        GetViewport().SetInputAsHandled();
    }

    private void SelectOwnPiece(Square square)
    {
        if (_game is null || _board is null) return;
        _selected = _game.Board.At(square) is Piece piece && piece.Color == _game.Turn ? square : null;
        var moves = _game.LegalMoves()
            .Where(move => move.From == _selected).ToArray();
        _board.SetSelection(_selected, moves.Select(move => move.To));
        if (_game.Mode == GameMode.Fondue && _selected is not null && moves.Length == 0 &&
            _game.Status == GameStatus.Check)
        {
            var threats = GameRules.KingMeltingThreats(_game.Board, _game.Turn);
            MoveRejected?.Invoke(threats.Count > 0
                ? "JAQUE FONDUE: tenés que evitar la captura en " +
                  string.Join(", ", threats.Select(move => move.To.ToString()).Distinct()) + "."
                : "Tu rey está en jaque: esa pieza no tiene movimientos que lo salven.");
        }
    }

    private string ExplainFondueRejection(Square from, Square to)
    {
        if (_game is null) return "Movimiento no permitido.";
        // Legal moves are authoritative. Inspect pseudo-legal candidates only
        // to explain why a geometrically valid capture was filtered out.
        var candidates = MoveGenerator.Generate(_game.Board, _game.Turn)
            .Where(move => move.From == from && move.To == to).ToArray();
        if (candidates.Length == 0)
            return "Esa pieza no puede moverse a esa casilla.";

        var next = _game.Board.WithMove(candidates[0]);
        bool ownKingSurvives = Enumerable.Range(0, 64).Any(index =>
            next.At(Square.FromIndex(index)) == new Piece(PieceType.King, _game.Turn));
        if (!ownKingSurvives)
            return "Captura ilegal en Fondue: la cadena derretiría tu propio rey.";

        var threats = GameRules.KingMeltingThreats(next, _game.Turn);
        if (threats.Count > 0)
            return "Esa jugada dejaría a tu rey expuesto: el rival podría capturar en " +
                string.Join(", ", threats.Select(move => move.To.ToString()).Distinct()) +
                " y derretirlo.";

        if (CheckDetector.IsInCheck(next, _game.Turn))
            return "Esa jugada dejaría a tu rey en jaque.";

        return "Movimiento no permitido.";
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
