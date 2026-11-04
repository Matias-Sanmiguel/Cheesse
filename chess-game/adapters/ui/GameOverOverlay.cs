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
    private Tween? _resultTween;

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
        bool picada = game.Status == GameStatus.PicadaVictory;
        bool checkmate = game.Status == GameStatus.Checkmate;
        bool decisive = checkmate || picada;
        string winner = game.Turn == PlayerColor.White ? "NEGRAS" : "BLANCAS";
        _title.Text = picada ? "REY DE LA PICADA"
            : checkmate ? "GANAN " + winner : "TABLAS";
        _reason.Text = picada ? "GANAN " + winner + " - REY AL CENTRO"
            : game.Status switch
            {
                GameStatus.Checkmate => "JAQUE MATE",
                GameStatus.Stalemate => "REY AHOGADO",
                GameStatus.DrawByFiftyMove => "REGLA DE 50 MOVIMIENTOS",
                GameStatus.DrawByRepetition => "TRIPLE REPETICION",
                GameStatus.DrawByInsufficientMaterial => "MATERIAL INSUFICIENTE",
                _ => "FIN DE PARTIDA"
            };
        PixelUiSkin.ShowGameOverBanner(this, !decisive);

        // Crown and a short reveal make Picada wins feel different from checkmate.
        var banner = GetNode<TextureRect>("Overlay/Center/Panel/Content/ResultBanner");
        var crown = banner.GetNodeOrNull<TextureRect>("PicadaCrown");
        if (crown is null)
        {
            crown = PixelUiSkin.Image("PicadaCrown", "icons/crown_16x16.png",
                new Vector2(338, 30), new Vector2(32, 32), 2);
            banner.AddChild(crown);
        }
        crown.Visible = picada;
        var panel = GetNode<PanelContainer>("Overlay/Center/Panel");
        _resultTween?.Kill();
        panel.Modulate = Colors.White;
        Show();
        if (picada)
        {
            panel.Modulate = new Color(1, 1, 1, 0.15f);
            _resultTween = CreateTween();
            _resultTween.TweenProperty(panel, "modulate:a", 1f, 0.45)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
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