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
        content.CustomMinimumSize = new Vector2(340, 808);
        // Nine-slice extends the parchment vertically, without stretching its border pixels.
        var background = new Panel
        {
            Name = "Parchment",
            Position = Vector2.Zero,
            Size = new Vector2(340, 808),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        background.AddThemeStyleboxOverride("panel", new StyleBoxTexture
        {
            Texture = Texture("03_sidebar/sidebar_parchment_306x454.png"),
            TextureMarginLeft = 14,
            TextureMarginRight = 14,
            TextureMarginTop = 14,
            TextureMarginBottom = 14
        });
        content.AddChild(background);
        content.MoveChild(background, 0);

        void Decor(string name, string path, float x, float y, float w, float h)
        {
            var art = Sprite(name, path, new Vector2(x, y), new Vector2(w, h));
            content.AddChild(art);
            content.MoveChild(art, 1);
        }

        // The upper three bars, divider and icons stay at their native PNG dimensions.
        // Resizing 270x28 to 294x40 was stretching pixels and blurring the edges.
        Decor("HeaderRibbon", "03_sidebar/header_ribbon_210x34.png", 65, 6, 210, 34);
        Decor("ModeBar", "03_sidebar/status_cream_270x28.png", 35, 76, 270, 28);
        Decor("TurnBar", "03_sidebar/status_turn_270x28.png", 35, 117, 270, 28);
        // Status is deliberately a lightweight dot + label on the parchment.
        // It does not need a third rectangular ribbon.
        Decor("ActionsDivider", "03_sidebar/divider_270x12.png", 35, 228, 270, 12);

        // History artwork also uses nine-slice: only its center expands.
        var historyPanel = new Panel
        {
            Name = "HistoryPanel",
            Position = new Vector2(24, 475),
            Size = new Vector2(292, 225),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        historyPanel.AddThemeStyleboxOverride("panel", new StyleBoxTexture
        {
            Texture = Texture("03_sidebar/history_box_270x92.png"),
            TextureMarginLeft = 12, TextureMarginRight = 12,
            TextureMarginTop = 12, TextureMarginBottom = 12
        });
        content.AddChild(historyPanel);
        content.MoveChild(historyPanel, 1);

        Decor("CrownBadge", "05_icons/crown_24.png", 42, 78, 24, 24);
        Decor("TurnBadge", "05_icons/cheese_24.png", 42, 119, 24, 24);
        Decor("StatusDot", "05_icons/status_dot_16.png", 46, 164, 16, 16);

        // Reflow EXISTING controls. Paths/signals in SidePanelView stay unchanged.
        void Place(string path, float x, float y, float w, float h)
        {
            var control = panel.GetNode<Control>("Margin/Content/" + path);
            control.Position = new Vector2(x, y);
            control.Size = new Vector2(w, h);
        }

        Place("Title", 65, 8, 210, 32);
        Place("Mode", 75, 77, 194, 26);
        Place("Turn", 75, 118, 190, 26);
        // Random is reparented into the in-game settings popup, not the turn bar.
        Place("Status", 70, 159, 226, 26);
        Place("ActionsHeading", 103, 199, 134, 25);

        Place("History", 24, 256, 292, 48);
        Place("History/Undo", 0, 0, 140, 44);
        Place("History/Redo", 152, 0, 140, 44);
        Place("Files", 24, 316, 292, 48);
        Place("Files/Save", 0, 0, 140, 44);
        Place("Files/Load", 152, 0, 140, 44);
        Place("NewGame", 24, 376, 292, 49);

        Place("MovesHeading", 61, 438, 218, 30);
        Place("MovesScroll", 38, 495, 265, 181);
        Place("Menu", 24, 710, 292, 49);
        Place("Message", 27, 769, 286, 25);
        Place("Help", 27, 769, 286, 25);

        void Style(string path, int size, Color color, UiFontWeight weight)
        {
            PixelUiSkin.StyleLabel(panel.GetNode<Label>("Margin/Content/" + path), size, color, weight: weight);
        }
        Style("Title", 22, Ivory, UiFontWeight.Bold);
        Style("Mode", 18, Ink, UiFontWeight.SemiBold);
        Style("Turn", 18, Ink, UiFontWeight.SemiBold);
        Style("Status", 16, Ink, UiFontWeight.Medium);
        Style("ActionsHeading", 17, Ink, UiFontWeight.SemiBold);
        Style("MovesHeading", 17, Ink, UiFontWeight.SemiBold);
        Style("MovesScroll/MovesText", 18, Ivory, UiFontWeight.Regular);
        Style("Message", 15, Ink, UiFontWeight.Regular);
        Style("Help", 15, Ink, UiFontWeight.Regular);

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
            StyleButton(button, item.Item2, item.Item3, 19);
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
        random.TooltipText = "La IA juega con las piezas negras";
        random.Text = string.Empty;
        random.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        // CheckButton uses 'on'/'off' icons (not CheckBox's 'checked'/'unchecked').
        // Using the wrong names made Godot draw the default switch outside the bar.
        var on = Texture("05_icons/random_16.png");
        var off = Texture("05_icons/switch_16.png");
        if (on is not null)
        {
            random.AddThemeIconOverride("on", on);
            random.AddThemeIconOverride("on_disabled", on);
        }
        if (off is not null)
        {
            random.AddThemeIconOverride("off", off);
            random.AddThemeIconOverride("off_disabled", off);
        }
        random.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        random.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        random.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        random.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        random.AddThemeConstantOverride("h_separation", 0);
    }
}
