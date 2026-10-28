
using Chess.Core.Model;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

public partial class PromotionDialog : CanvasLayer
{
	[Signal]
	public delegate void PieceChosenEventHandler(int pieceType);

	[Signal]
	public delegate void CancelledEventHandler();

	private Button _queenButton = null!;

	public override void _Ready()
	{
		_queenButton = GetNode<Button>("Overlay/Center/Panel/Options/Queen");

		GetNode<Button>("Overlay/Center/Panel/Options/Rook").Pressed += () => Choose(PieceType.Rook);

		GetNode<Button>("Overlay/Center/Panel/Options/Bishop").Pressed += () => Choose(PieceType.Bishop);

		GetNode<Button>("Overlay/Center/Panel/Options/Knight").Pressed += () => Choose(PieceType.Knight);

		_queenButton.Pressed += () => Choose(PieceType.Queen);

		GetNode<Button>("Overlay/Center/Panel/Options/Cancel").Pressed += Cancel;

		Hide();
	}

	public void OpenDialog()
	{
		Show();
		_queenButton.GrabFocus();
	}

	public override void _Input(InputEvent input)
	{
		if (!Visible)
			return;

		if (input is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
		{
			Cancel();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Choose(PieceType pieceType)
	{
		Hide();
		EmitSignal(SignalName.PieceChosen, (int)pieceType);
	}

	private void Cancel()
	{
		Hide();
		EmitSignal(SignalName.Cancelled);
	}
}
