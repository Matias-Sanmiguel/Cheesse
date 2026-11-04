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

    // Recolor the original 12x12 artwork without losing its pixel silhouette.
    // Dark ink contrasts with the gold button; cream ink contrasts with the brown one.
    private static Texture2D? RecolorIcon(Texture2D? source, Color ink)
    {
        if (source is null) return null;
        var image = source.GetImage();
        if (image.IsCompressed()) image.Decompress();
        image.Convert(Image.Format.Rgba8);
        for (int y = 0; y < image.GetHeight(); y++)
        for (int x = 0; x < image.GetWidth(); x++)
        {
            var pixel = image.GetPixel(x, y);
            if (pixel.A > 0f)
                image.SetPixel(x, y, new Color(ink.R, ink.G, ink.B, pixel.A));
        }
        return ImageTexture.CreateFromImage(image);
    }

    // The winner is a real chess king sprite with a 1px contour, not the
    // old 12x12 square-backed team icon. Transparent surroundings stay transparent.
    private static Texture2D? OutlinedPiece(Texture2D? source, Color contour)
    {
        if (source is null) return null;
        var image = source.GetImage();
        if (image.IsCompressed()) image.Decompress();
        image.Convert(Image.Format.Rgba8);
        int width = image.GetWidth(), height = image.GetHeight();
        var outlined = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        outlined.Fill(Colors.Transparent);

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (image.GetPixel(x, y).A < 0.5f) continue;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height &&
                    image.GetPixel(nx, ny).A < 0.5f)
                    outlined.SetPixel(nx, ny, contour);
            }
        }
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var pixel = image.GetPixel(x, y);
            if (pixel.A > 0f) outlined.SetPixel(x, y, pixel);
        }
        return ImageTexture.CreateFromImage(outlined);
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

        var buttonIcon = AddIcon(button, name + "Icon", "04_icons/" + icon + "_12x12.png",
            7, 3, 12, 12);
        buttonIcon.Texture = RecolorIcon(buttonIcon.Texture,
            material == "primary" ? new Color("47281d") : new Color("fff0c8"));
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
        _winnerIcon = AddIcon(_content, "WinnerIcon", "res://assets/pieces/white/king.png",
            23, 65, 24, 24);
        // Keep the enlarged chess king and the title balanced within the ribbon.
        _title = AddText(_content, "Title", "GANARON BLANCAS", 49, 70, 138, 13, 32,
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
        if (decisive)
        {
            string king = winnerIsWhite ? "white" : "black";
            _winnerIcon.Texture = OutlinedPiece(
                Texture("res://assets/pieces/" + king + "/king.png"),
                new Color(winnerIsWhite ? "3c261c" : "ffe0a0"));
            _winnerIcon.Position = Size(23, 65);
            _winnerIcon.Size = Size(24, 24);
        }
        else
        {
            _winnerIcon.Texture = Texture("04_icons/trophy_12x12.png");
            _winnerIcon.Position = Size(29, 71);
            _winnerIcon.Size = Size(12, 12);
        }
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
