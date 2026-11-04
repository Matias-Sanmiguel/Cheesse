using Chess.Core.Model;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

/// <summary>
/// Victory/result modal assembled from the individual V2 pack components.
/// Logical artwork: 216x194; x4 at 1440x900. No prefab or flattened UI PNG.
/// </summary>
public partial class GameOverOverlay : CanvasLayer
{
    [Signal] public delegate void RematchRequestedEventHandler();
    [Signal] public delegate void MainMenuRequestedEventHandler();

    private const int Scale = 4;
    private const string Assets = "res://assets/ui/victory_result/";

    private Control _panel = null!;
    private Control _content = null!;
    private TextureRect _banner = null!;
    private TextureRect _winnerIcon = null!;
    private Label _title = null!;
    private Label _reason = null!;
    private Label _timeValue = null!;
    private Label _movesValue = null!;
    private Label _modeValue = null!;
    private Tween? _introTween;

    public override void _Ready()
    {
        _panel = GetNode<Control>("Overlay/Center/Panel");
        _content = GetNode<Control>("Overlay/Center/Panel/Content");
        // Fixed-size pixel-art composition, positioned by the CenterContainer.
        _panel.CustomMinimumSize = Size(216, 194);
        _panel.Size = Size(216, 194);
        _content.Position = Vector2.Zero;
        _content.Size = Size(216, 194);
        _panel.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        _content.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

        BuildComponents();
        Hide();
    }

    private static Vector2 Size(float x, float y) => new(x * Scale, y * Scale);

    private static Texture2D? Texture(string relative)
    {
        string path = relative.StartsWith("res://", StringComparison.Ordinal)
            ? relative : Assets + relative;
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    private static TextureRect AddSprite(Control parent, string name, string path,
        float x, float y, float width, float height)
    {
        var sprite = new TextureRect
        {
            Name = name,
            Texture = Texture(path),
            Position = Size(x, y),
            Size = Size(width, height),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(sprite);
        return sprite;
    }

    private static Label AddText(Control parent, string name, string value,
        float x, float y, float width, float height, int fontSize, Color color,
        UiFontWeight weight = UiFontWeight.SemiBold)
    {
        var label = new Label
        {
            Name = name,
            Text = value,
            Position = Size(x, y),
            Size = Size(width, height),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Off,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipText = true
        };
        PixelUiSkin.StyleLabel(label, fontSize, color, weight: weight);
        parent.AddChild(label);
        return label;
    }

    private static StyleBoxTexture ButtonState(string relative)
    {
        return new StyleBoxTexture
        {
            Texture = Texture("03_buttons/" + relative + "_176x18.png"),
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Stretch,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Stretch
        };
    }

    private Button AddImageButton(string name, string text, string material,
        string icon, float x, float y, Action action)
    {
        var button = new Button
        {
            Name = name,
            Position = Size(x, y),
            Size = Size(176, 18),
            Text = "",
            FocusMode = Control.FocusModeEnum.All,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        button.AddThemeStyleboxOverride("normal", ButtonState(material + "_idle"));
        button.AddThemeStyleboxOverride("hover", ButtonState(material + "_hover"));
        button.AddThemeStyleboxOverride("pressed", ButtonState(material + "_pressed"));
        button.AddThemeStyleboxOverride("disabled", ButtonState("primary_disabled"));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Pressed += action;
        _content.AddChild(button);

        AddSprite(button, name + "Icon", "04_icons/" + icon + "_12x12.png",
            5, 3, 12, 12);
        AddText(button, name + "Label", text, 19, 0, 138, 18, 34,
            material == "primary" ? new Color("47281d") : new Color("fff0c8"),
            UiFontWeight.Bold);
        return button;
    }

    private Label AddStat(int index, string label, string icon)
    {
        float x = 21 + index * 60;
        AddSprite(_content, "StatFrame" + index, "02_panel/stat_box_54x23.png", x, 121, 54, 23);
        AddSprite(_content, "StatIcon" + index, "04_icons/" + icon + "_12x12.png",
            x + 3, 126, 12, 12);
        AddText(_content, "StatLabel" + index, label, x + 15, 122.5f, 37, 7, 20,
            new Color("684538"), UiFontWeight.Medium);
        return AddText(_content, "StatValue" + index, "--", x + 15, 131, 37, 10, 28,
            new Color("442c21"), UiFontWeight.Bold);
    }

    private void BuildComponents()
    {
        // Layer order matters: base parchment, banner, ribbons, statistics and real buttons.
        AddSprite(_content, "ResultPanel", "02_panel/result_panel_208x165.png",
            4, 29, 208, 165);
        _banner = AddSprite(_content, "ResultBanner", "01_banner/victory_banner_192x64.png",
            12, 0, 192, 64);

        AddSprite(_content, "WinnerRibbon", "02_panel/winner_ribbon_180x24.png",
            18, 65, 180, 24);
        _winnerIcon = AddSprite(_content, "WinnerIcon", "04_icons/white_12x12.png",
            29, 71, 12, 12);
        // Reserve space for the icon, keeping the whole winner row balanced.
        _title = AddText(_content, "Title", "GANARON BLANCAS", 45, 70, 142, 13, 32,
            new Color("fff0c8"), UiFontWeight.Bold);

        AddSprite(_content, "ReasonRibbon", "02_panel/result_reason_158x13.png",
            29, 93, 158, 13);
        _reason = AddText(_content, "Reason", "JAQUE MATE", 34, 95, 148, 11, 26,
            new Color("49281f"), UiFontWeight.Bold);

        AddSprite(_content, "StatsSeparator", "02_panel/stats_separator_174x5.png",
            21, 112, 174, 5);
        _timeValue = AddStat(0, "TIEMPO", "clock");
        _movesValue = AddStat(1, "JUGADAS", "moves");
        _modeValue = AddStat(2, "MODO", "mode");

        AddImageButton("Rematch", "REVANCHA", "primary", "rematch", 20, 150,
            () => { Hide(); EmitSignal(SignalName.RematchRequested); });
        AddImageButton("MainMenu", "MENU PRINCIPAL", "secondary", "home", 20, 172,
            () => { Hide(); EmitSignal(SignalName.MainMenuRequested); });
    }

    public void ShowResult(ChessGame game, int elapsedSeconds = 0)
    {
        if (game.Status is GameStatus.Ongoing or GameStatus.Check) return;

        bool picada = game.Status == GameStatus.PicadaVictory;
        bool checkmate = game.Status == GameStatus.Checkmate;
        bool decisive = picada || checkmate;
        bool winnerIsWhite = game.Turn == PlayerColor.Black;

        _title.Text = decisive ? "GANARON " + (winnerIsWhite ? "BLANCAS" : "NEGRAS") : "PARTIDA EN TABLAS";
        _title.AddThemeFontSizeOverride("font_size", decisive ? 32 : 29);
        _winnerIcon.Texture = Texture("04_icons/" +
            (!decisive ? "trophy" : winnerIsWhite ? "white" : "black") + "_12x12.png");
        _reason.Text = game.Status switch
        {
            GameStatus.Checkmate => "JAQUE MATE",
            GameStatus.PicadaVictory => "REY AL CENTRO",
            GameStatus.Stalemate => "REY AHOGADO",
            GameStatus.DrawByFiftyMove => "50 MOVIMIENTOS",
            GameStatus.DrawByRepetition => "TRIPLE REPETICION",
            GameStatus.DrawByInsufficientMaterial => "MATERIAL INSUFICIENTE",
            _ => "FIN DE PARTIDA"
        };

        _banner.Texture = decisive ? Texture("01_banner/victory_banner_192x64.png")
            : Texture("res://assets/ui/dialogs/draw_banner_192x48.png");
        _banner.Position = decisive ? Size(12, 0) : Size(12, 8);
        _banner.Size = decisive ? Size(192, 64) : Size(192, 48);
        int seconds = Math.Max(0, elapsedSeconds);
        _timeValue.Text = $"{seconds / 60:00}:{seconds % 60:00}";
        // Show full moves rather than individual half-moves.
        _movesValue.Text = ((game.History.Count + 1) / 2).ToString();
        _modeValue.Text = game.Mode == GameMode.ReyDeLaPicada ? "PICADA" : "CLASICO";

        _introTween?.Kill();
        _panel.Modulate = Colors.White;
        Show();
        if (picada)
        {
            _panel.Modulate = new Color(1, 1, 1, 0.1f);
            _introTween = CreateTween();
            _introTween.TweenProperty(_panel, "modulate:a", 1f, 0.35)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
    }
}
