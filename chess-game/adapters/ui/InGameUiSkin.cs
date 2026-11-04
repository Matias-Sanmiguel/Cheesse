using Godot;

namespace Cheesse.Game.Adapters.Ui;

/// <summary>
/// Presentation-only resource catalogue for the 61 original in-game PNGs.
/// Native layout uses the kit's 960x540 reference grid. All buttons are genuine
/// Godot controls with 9-slice states and remain functional.
/// </summary>
internal static class InGameUiSkin
{
    private const string Root = "res://assets/ingame/";
    private static readonly Dictionary<string, Texture2D> Cache = new();

    public static readonly Color Ink = new("442c21");
    public static readonly Color Ivory = new("fff0ca");
    public static readonly Color Burgundy = new("6e2c42");

    public static Texture2D? Texture(string relative)
    {
        string path = relative.StartsWith("res://", StringComparison.Ordinal) ? relative : Root + relative;
        if (Cache.TryGetValue(path, out var cached)) return cached;
        if (!ResourceLoader.Exists(path)) return null;
        Texture2D? loaded = GD.Load<Texture2D>(path);
        if (loaded != null) Cache[path] = loaded;
        return loaded;
    }

    public static TextureRect Sprite(string name, string relative, Vector2 position, Vector2 size)
    {
        return new TextureRect
        {
            Name = name,
            Texture = Texture(relative),
            Position = position,
            Size = size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
    }

    public static Label Text(string name, string value, Vector2 position, Vector2 size, int fontSize,
        Color color, UiFontWeight weight = UiFontWeight.Medium,
        HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label
        {
            Name = name,
            Text = value,
            Position = position,
            Size = size,
            HorizontalAlignment = align,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.Off
        };
        PixelUiSkin.StyleLabel(label, fontSize, color, weight: weight);
        return label;
    }

    // The kit specifies source margins left/right=10, top/bottom=9.
    private static StyleBoxTexture ButtonSkin(string material, string state)
    {
        var path = $"04_buttons/{material}_{state}_9slice_48x32.png";
        return new StyleBoxTexture
        {
            Texture = Texture(path),
            TextureMarginLeft = 10,
            TextureMarginTop = 9,
            TextureMarginRight = 10,
            TextureMarginBottom = 9,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Stretch,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Stretch,
        };
    }

    public static void StyleButton(Button button, string material, string icon = "", int fontSize = 14)
    {
        button.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        button.FocusMode = Control.FocusModeEnum.All;
        button.AddThemeStyleboxOverride("normal", ButtonSkin(material, "idle"));
        button.AddThemeStyleboxOverride("hover", ButtonSkin(material, "hover"));
        button.AddThemeStyleboxOverride("pressed", ButtonSkin(material, "pressed"));
        button.AddThemeStyleboxOverride("disabled", ButtonSkin(material, "disabled"));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeFontSizeOverride("font_size", fontSize);
        if (PixelUiSkin.FontFor(UiFontWeight.SemiBold) is { } font)
            button.AddThemeFontOverride("font", font);
        Color ink = material == "cheese" ? Ink : Ivory;
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            button.AddThemeColorOverride(state, ink);
        button.AddThemeColorOverride("font_disabled_color", new Color("78685d"));
        button.Icon = string.IsNullOrEmpty(icon) ? null : Texture("05_icons/" + icon + "_16.png");
        button.IconAlignment = HorizontalAlignment.Left;
        button.Alignment = HorizontalAlignment.Center;
        button.AddThemeConstantOverride("h_separation", 5);
    }

    public static void SkinSidebar(SidePanelView panel)
    {
        panel.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var content = panel.GetNode<Control>("Margin/Content");
        var background = Sprite("Parchment", "03_sidebar/sidebar_parchment_306x454.png",
            Vector2.Zero, new Vector2(306, 454));
        content.AddChild(background);
        content.MoveChild(background, 0);

        void Decor(string name, string path, float x, float y, float w, float h)
        {
            var art = Sprite(name, path, new Vector2(x, y), new Vector2(w, h));
            content.AddChild(art);
            content.MoveChild(art, 1); // Behind all live controls, above parchment.
        }

        Decor("HeaderRibbon", "03_sidebar/header_ribbon_210x34.png", 48, -9, 210, 34);
        Decor("ModeBar", "03_sidebar/status_cream_270x28.png", 18, 54, 270, 28);
        Decor("TurnBar", "03_sidebar/status_turn_270x28.png", 18, 91, 270, 28);
        Decor("ActionsDivider", "03_sidebar/divider_270x12.png", 18, 139, 270, 12);
        Decor("HistoryPanel", "03_sidebar/history_box_270x92.png", 18, 285, 270, 92);
        Decor("CrownBadge", "05_icons/crown_24.png", 24, 57, 24, 24);
        Decor("TurnBadge", "05_icons/cheese_24.png", 24, 93, 24, 24);
        Decor("StatusDot", "05_icons/status_dot_16.png", 20, 124, 13, 13);

        void Style(string path, int size, Color color, UiFontWeight weight)
        {
            PixelUiSkin.StyleLabel(panel.GetNode<Label>("Margin/Content/" + path), size, color, weight: weight);
        }
        Style("Title", 19, Ivory, UiFontWeight.Bold);
        Style("Mode", 16, Ink, UiFontWeight.SemiBold);
        Style("Turn", 16, Ink, UiFontWeight.SemiBold);
        Style("Status", 11, Ink, UiFontWeight.Medium);
        Style("ActionsHeading", 13, Ink, UiFontWeight.SemiBold);
        Style("MovesHeading", 12, Ink, UiFontWeight.SemiBold);
        Style("MovesScroll/MovesText", 14, Ivory, UiFontWeight.Regular);
        Style("Message", 11, Ink, UiFontWeight.Regular);
        Style("Help", 11, Ink, UiFontWeight.Regular);

        var contentRoot = "Margin/Content/";
        foreach (var item in new[]
        {
            ("History/Undo", "burgundy", "undo"),
            ("History/Redo", "burgundy", "redo"),
            ("Files/Save", "burgundy", "save"),
            ("Files/Load", "burgundy", "load"),
            ("NewGame", "cheese", "plus"),
            ("Menu", "burgundy", "home")
        })
        {
            var button = panel.GetNode<Button>(contentRoot + item.Item1);
            StyleButton(button, item.Item2, item.Item3, 14);
        }

        var scroll = panel.GetNode<ScrollContainer>(contentRoot + "MovesScroll");
        scroll.GetVScrollBar().AddThemeStyleboxOverride("grabber", new StyleBoxTexture
        {
            Texture = Texture("03_sidebar/history_thumb_10x24.png"),
            TextureMarginLeft = 3, TextureMarginRight = 3,
            TextureMarginTop = 5, TextureMarginBottom = 5
        });
        scroll.GetVScrollBar().AddThemeStyleboxOverride("scroll", new StyleBoxEmpty());
        scroll.GetVScrollBar().AddThemeStyleboxOverride("scroll_focus", new StyleBoxEmpty());

        var random = panel.GetNode<CheckButton>(contentRoot + "Random");
        random.TooltipText = "Activar o desactivar negras aleatorias";
        random.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        random.AddThemeIconOverride("checked", Texture("05_icons/random_16.png"));
        random.AddThemeIconOverride("unchecked", Texture("05_icons/switch_16.png"));
        random.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }
}