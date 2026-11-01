
using Chess.Core.Model;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

public partial class GameOverOverlay : CanvasLayer
{
	[Signal]
	public delegate void RematchRequestedEventHandler();

	[Signal]
	public delegate void MainMenuRequestedEventHandler();

	private Label _title = null!;
	private Label _reason = null!;

	public override void _Ready()
	{
		_title = GetNode<Label>("Overlay/Center/Panel/Content/Title");
		_reason = GetNode<Label>("Overlay/Center/Panel/Content/Reason");

		GetNode<Button>("Overlay/Center/Panel/Content/Rematch").Pressed += OnRematch;
		GetNode<Button>("Overlay/Center/Panel/Content/MainMenu").Pressed += OnMainMenu;

		Hide();
	}

	public void ShowResult(ChessGame game)
	{
		if (game.Status is GameStatus.Ongoing or GameStatus.Check) return;

        _title.Text = game.Winner == PlayerColor.White ? "WHITE WINS" : game.Winner == PlayerColor.Black ? "BLACK WINS" : "DRAW";
        _reason.Text = SidePanelView.StatusText(game.Status);

		Show();
	}

	private void OnRematch()
	{
		Hide();
		EmitSignal(SignalName.RematchRequested);
	}

	private void OnMainMenu()
	{
		Hide();
		EmitSignal(SignalName.MainMenuRequested);
	}
}
