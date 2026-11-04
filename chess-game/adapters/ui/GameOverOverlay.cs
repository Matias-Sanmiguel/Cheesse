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
    private Tween? _introTween;

    public override void _Ready()
    {
        _panel = GetNode<Control>("Overlay/Center/Panel");
        _content = GetNode<Control>("Overlay/Center/Panel/Content");
        // Fixed-size pixel-art composition, positioned by the CenterContainer.
        _panel.CustomMinimumSize = Size(216, 194);
        _panel.Size = Size(216, 194);
        _content.Position = Vector2.Zero;
        // Content has full-rect anchors: its size follows the centered panel.
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

    // Icons keep their original aspect ratio when the menu is scaled.
    private static TextureRect AddIcon(Control parent, string name, string path,
        float x, float y, float width = 12, float height = 12)
    {
        var icon = new TextureRect
        {
            Name = name,
            Texture = Texture(path),
            Position = Size(x, y),
            Size = Size(width, height),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(icon);
        return icon;
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

        AddIcon(button, name + "Icon", "04_icons/" + icon + "_12x12.png",
            7, 3, 12, 12);
        AddText(button, name + "Label", text, 24, 0, 128, 18, 32,
            material == "primary" ? new Color("47281d") : new Color("fff0c8"),
            UiFontWeight.Bold);
        return button;
    }

    private void BuildComponents()
    {
        // Layer order matters: base parchment, banner, ribbons, compact clock and buttons.
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

        // One discreet centered time row instead of three bulky stat cards.
        AddIcon(_content, "TimeIcon", "04_icons/clock_12x12.png",
            64, 119, 12, 12);
        AddText(_content, "TimeLabel", "TIEMPO", 81, 119, 39, 12, 22,
            new Color("684538"), UiFontWeight.Medium);
        _timeValue = AddText(_content, "TimeValue", "00:00", 122, 119, 36, 12, 28,
            new Color("442c21"), UiFontWeight.Bold);

        // Raise both buttons while keeping the full original pixel-art artwork.
        AddImageButton("Rematch", "REVANCHA", "primary", "rematch", 20, 136,
            () => { Hide(); EmitSignal(SignalName.RematchRequested); });
        AddImageButton("MainMenu", "MENU PRINCIPAL", "secondary", "home", 20, 158,
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
