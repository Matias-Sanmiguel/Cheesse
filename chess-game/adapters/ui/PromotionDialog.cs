
using Godot;

namespace Cheesse.Game.Adapters.Ui;

public partial class PromotionDialog : CanvasLayer
{
	[Signal]
	public delegate void PieceChosenEventHandler(int pieceType);

	[Signal]
	public delegate void CancelledEventHandler();

	private PromotionWheel _wheel = null!;

	public override void _Ready()
	{
		_wheel = GetNode<PromotionWheel>("Overlay/Center/PromotionWheel");
		_wheel.PieceSelected += OnPieceSelected;
		_wheel.CancelRequested += Cancel;

		Hide();
	}

	public void OpenDialog()
	{
		Show();
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

	private void OnPieceSelected(int pieceType)
	{
		Hide();
		EmitSignal(SignalName.PieceChosen, pieceType);
	}

	private void Cancel()
	{
		Hide();
		EmitSignal(SignalName.Cancelled);
	}

	public override void _ExitTree()
	{
		if (_wheel is null)
			return;

		_wheel.PieceSelected -= OnPieceSelected;
		_wheel.CancelRequested -= Cancel;
	}
}
