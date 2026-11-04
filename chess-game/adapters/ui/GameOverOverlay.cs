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
        PixelUiSkin.ApplyGameOver(this);
        Hide();
    }

    public void ShowResult(ChessGame game)
    {
        if (game.Status is GameStatus.Ongoing or GameStatus.Check) return;
        bool checkmate = game.Status == GameStatus.Checkmate;
        _title.Text = checkmate
            ? (game.Turn == PlayerColor.White ? "GANAN NEGRAS" : "GANAN BLANCAS")
            : "TABLAS";
        _reason.Text = game.Status switch
        {
            GameStatus.Checkmate => "JAQUE MATE",
            GameStatus.Stalemate => "REY AHOGADO",
            GameStatus.DrawByFiftyMove => "REGLA DE 50 MOVIMIENTOS",
            GameStatus.DrawByRepetition => "TRIPLE REPETICION",
            GameStatus.DrawByInsufficientMaterial => "MATERIAL INSUFICIENTE",
            _ => "FIN DE PARTIDA"
        };
        PixelUiSkin.ShowGameOverBanner(this, !checkmate);
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