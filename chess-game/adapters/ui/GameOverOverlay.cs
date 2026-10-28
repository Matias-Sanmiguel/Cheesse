
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

		if (game.Status == GameStatus.Checkmate)
		{
			_title.Text = game.Turn == PlayerColor.White ? "BLACK WINS" : "WHITE WINS";
			_reason.Text = "CHECKMATE";
		}
		else
		{
			_title.Text = "DRAW";
			_reason.Text = game.Status switch
			{
				GameStatus.Stalemate => "STALEMATE",
				GameStatus.DrawByFiftyMove => "FIFTY-MOVE RULE",
				GameStatus.DrawByRepetition => "THREEFOLD REPETITION",
				GameStatus.DrawByInsufficientMaterial => "INSUFFICIENT MATERIAL",
				_ => "DRAW"
			};
		}

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
