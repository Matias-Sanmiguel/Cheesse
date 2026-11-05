using Godot;

namespace Cheesse.Game.Adapters.Ui;

/// <summary>
/// Shared presentation primitives for pixel-art UI. No game logic belongs here.
/// All textures are taken from the original 48px / UI asset pack and filtered with Nearest.
/// </summary>
internal enum UiFontWeight { Regular, Medium, SemiBold, Bold }

internal static class PixelUiSkin
{
    private const string Ui = "res://assets/ui/";
    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static readonly Dictionary<UiFontWeight, Font> Fonts = new();

    public static readonly Color Ink = new("39251d");
    public static readonly Color Cream = new("ffedca");
    public static readonly Color Gold = new("f5c14d");
    public static readonly Color Muted = new("b9a18b");
    public static readonly Color Burgundy = new("78142c");

    public static Texture2D? Texture(string path, int scale = 1)
    {
        string resource = path.StartsWith("res://", StringComparison.Ordinal) ? path : Ui + path;
        string key = resource + "@" + scale;
        if (Textures.TryGetValue(key, out var cached)) return cached;
        if (!ResourceLoader.Exists(resource)) return null;
        var source = GD.Load<Texture2D>(resource);
        if (source is null) return null;
        if (scale <= 1) return Textures[key] = source;
        var image = source.GetImage();
        image.Resize(source.GetWidth() * scale, source.GetHeight() * scale, Godot.Image.Interpolation.Nearest);
        return Textures[key] = ImageTexture.CreateFromImage(image);
    }

    /// <summary>Load the four project-bundled OFL Pixelify Sans weights once.</summary>
    public static Font? FontFor(UiFontWeight weight)
    {
        if (Fonts.TryGetValue(weight, out var cached))
            return cached;

        string path = Ui + "fonts/PixelifySans-" + weight + ".ttf";
        if (!ResourceLoader.Exists(path))
            return null;

        var font = GD.Load<Font>(path);
        if (font is not null)
            Fonts[weight] = font;

        return font;
    }

    public static StyleBoxTexture NineSlice(string path, int sourceMargin = 8, int scale = 2)
    {
        var texture = Texture(path, scale);
        var margin = sourceMargin * scale;
        return new StyleBoxTexture
        {
            Texture = texture,
            TextureMarginLeft = margin,
            TextureMarginTop = margin,
            TextureMarginRight = margin,
            TextureMarginBottom = margin,
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Stretch,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Stretch,
            ContentMarginLeft = margin,
            ContentMarginTop = 8,
            ContentMarginRight = margin,
            ContentMarginBottom = 8,
        };
    }

    public static Panel MakePanel(string name, string skin, Vector2 position, Vector2 size)
    {
        var panel = new Panel { Name = name, Position = position, Size = size };
        panel.AddThemeStyleboxOverride("panel", NineSlice("panels/" + skin + "_9slice_32x32.png"));
        return panel;
    }

    public static Label Label(string name, string text, Vector2 position, Vector2 size, int fontSize = 22,
        bool pixel = false, bool light = false, HorizontalAlignment alignment = HorizontalAlignment.Left,
        UiFontWeight? weight = null)
    {
        var label = new Label
        {
            Name = name,
            Text = text,
            Position = position,
            Size = size,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        StyleLabel(label, fontSize, light ? Cream : Ink, pixel, weight);
        return label;
    }

    /// <summary>
    /// Bold for display headings, SemiBold for small headings, Medium for body
    /// headings, and Regular for descriptions. Use explicit weight for form labels.
    /// </summary>
    public static void StyleLabel(Label label, int fontSize, Color color, bool pixel = false,
        UiFontWeight? weight = null)
    {
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);

        UiFontWeight selected = weight ?? (pixel
            ? (fontSize >= 30 ? UiFontWeight.Bold : UiFontWeight.SemiBold)
            : (fontSize >= 21 ? UiFontWeight.Medium : UiFontWeight.Regular));

        if (FontFor(selected) is { } font)
            label.AddThemeFontOverride("font", font);
    }

    public static Button Button(string name, string text, Vector2 position, Vector2 size,
        Action? onPressed = null, string icon = "", int fontSize = 25, string variant = "large")
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            Position = position,
            Size = size,
            FocusMode = Control.FocusModeEnum.All,
        };
        StyleButton(button, fontSize, variant);
        if (!string.IsNullOrEmpty(icon)) button.Icon = Texture("icons/" + icon + "_16x16.png", 2);
        if (onPressed is not null) button.Pressed += onPressed;
        return button;
    }

    public static void StyleButton(Button button, int fontSize = 25, string variant = "large")
    {
        string folder = variant switch
        {
            "small" => "buttons/small_80x24/",
            "medium" => "buttons/medium_120x24/",
            _ => "buttons/large_160x24/"
        };
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
        {
            string file = state == "normal" ? "idle" : state;
            button.AddThemeStyleboxOverride(state, NineSlice(folder + file + ".png"));
        }
        // A focused control must not look hovered or pressed. This fixes the stuck first button.
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeFontSizeOverride("font_size", fontSize);
        foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            button.AddThemeColorOverride(color, Ink);
        button.AddThemeColorOverride("font_disabled_color", new Color("786a5c"));
        // Large navigation CTAs get Bold; normal controls use SemiBold.
        var weight = fontSize >= 38 ? UiFontWeight.Bold : UiFontWeight.SemiBold;
        if (FontFor(weight) is { } font)
            button.AddThemeFontOverride("font", font);
        button.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
    }

    public static void StyleOption(OptionButton select, int fontSize = 24)
    {
        StyleButton(select, fontSize, "medium");
        if (FontFor(UiFontWeight.Medium) is { } selectFont)
            select.AddThemeFontOverride("font", selectFont);
        select.AddThemeColorOverride("font_color", Ink);
        // Popup colors use an opaque texture-backed panel to remain readable on wood.
        var popup = select.GetPopup();
        popup.AddThemeStyleboxOverride("panel", NineSlice("panels/parchment_9slice_32x32.png"));
        popup.AddThemeColorOverride("font_color", Ink);
        popup.AddThemeColorOverride("font_hover_color", Burgundy);
        popup.AddThemeFontSizeOverride("font_size", 22);
        if (FontFor(UiFontWeight.Medium) is { } popupFont)
            popup.AddThemeFontOverride("font", popupFont);
    }

    public static TextureRect Image(string name, string path, Vector2 position, Vector2 size, int sourceScale = 1)
    {
        return new TextureRect
        {
            Name = name,
            Texture = Texture(path, sourceScale),
            Position = position,
            Size = size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
    }

    public static void ApplySidePanel(SidePanelView panel)
    {
        panel.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        panel.AddThemeStyleboxOverride("panel", NineSlice("panels/parchment_9slice_32x32.png"));
        const string at = "Margin/Content/";
        var content = panel.GetNode<VBoxContainer>("Margin/Content");
        content.AddThemeConstantOverride("separation", 6);
        var margin = panel.GetNode<MarginContainer>("Margin");
        margin.AddThemeConstantOverride("margin_left", 32);
        margin.AddThemeConstantOverride("margin_right", 32);
        margin.AddThemeConstantOverride("margin_top", 28);
        margin.AddThemeConstantOverride("margin_bottom", 24);

        var oldTitle = panel.GetNode<Label>(at + "Title");
        oldTitle.Visible = false;
        if (!content.HasNode("BrandLogo"))
        {
            var brand = Image("BrandLogo", "logos/logo_cheese_compact.png", Vector2.Zero, new Vector2(294, 64), 2);
            brand.CustomMinimumSize = new Vector2(294, 64);
            content.AddChild(brand);
            content.MoveChild(brand, 0);
        }

        StyleLabel(panel.GetNode<Label>(at + "Turn"), 25, Ink, pixel: true);
        StyleLabel(panel.GetNode<Label>(at + "Status"), 21, Ink, pixel: false);
        StyleLabel(panel.GetNode<Label>(at + "Message"), 19, Ink, pixel: false);
        StyleLabel(panel.GetNode<Label>(at + "Help"), 17, Ink, pixel: false);

        var turn = panel.GetNode<Label>(at + "Turn");
        var status = panel.GetNode<Label>(at + "Status");
        if (!turn.HasNode("TurnIcon"))
        {
            var icon = Image("TurnIcon", "hud/turn_white_24x24.png", Vector2.Zero, new Vector2(40, 40), 2);
            icon.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            icon.Position = new Vector2(-44, 0);
            turn.AddChild(icon);
        }
        if (!status.HasNode("StatusIcon"))
        {
            var icon = Image("StatusIcon", "hud/status_ongoing_24x24.png", Vector2.Zero, new Vector2(36, 36), 2);
            icon.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            icon.Position = new Vector2(-38, 0);
            status.AddChild(icon);
        }

        var separator = panel.GetNode<HSeparator>(at + "Separator");
        separator.CustomMinimumSize = new Vector2(0, 8);

        foreach (var (node, icon) in new[]
        {
            ("History/Undo", "undo"), ("History/Redo", "redo"),
            ("Files/Save", "save"), ("Files/Load", "load"),
            ("NewGame", "new_game"), ("Menu", "home"),
        })
        {
            var button = panel.GetNode<Button>(at + node);
            StyleButton(button, fontSize: 21, variant: "medium");
            button.Icon = Texture("icons/" + icon + "_16x16.png", 2);
            button.CustomMinimumSize = new Vector2(0, 52);
        }

        var opponent = panel.GetNode<CheckButton>(at + "Random");
        foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color", "font_disabled_color" })
            opponent.AddThemeColorOverride(key, Ink);
        opponent.AddThemeFontSizeOverride("font_size", 19);
        if (FontFor(UiFontWeight.Medium) is { } opponentFont)
            opponent.AddThemeFontOverride("font", opponentFont);
        opponent.AddThemeIconOverride("checked", Texture("controls/toggle_on.png", 2));
        opponent.AddThemeIconOverride("unchecked", Texture("controls/toggle_off.png", 2));

        if (!content.HasNode("MovesHeading"))
        {
            var heading = Label("MovesHeading", "HISTORIAL DE JUGADAS", Vector2.Zero, Vector2.Zero, 23, pixel: true);
            heading.CustomMinimumSize = new Vector2(0, 34);
            content.AddChild(heading);
            content.MoveChild(heading, content.GetChildren().IndexOf(opponent) + 1);
            var box = new ScrollContainer { Name = "MovesScroll", CustomMinimumSize = new Vector2(0, 150), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            box.AddThemeStyleboxOverride("panel", NineSlice("panels/dark_9slice_32x32.png"));
            var lines = Label("MovesText", "TODAVIA NO HAY JUGADAS", Vector2.Zero, Vector2.Zero, 18, light: false);
            lines.Name = "MovesText";
            lines.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            lines.AddThemeColorOverride("font_color", new Color("634632"));
            box.AddChild(lines);
            content.AddChild(box);
            content.MoveChild(box, content.GetChildren().IndexOf(heading) + 1);
        }
    }

    public static void ApplyGameOver(GameOverOverlay overlay)
    {
        var panel = overlay.GetNode<PanelContainer>("Overlay/Center/Panel");
        panel.AddThemeStyleboxOverride("panel", NineSlice("panels/parchment_9slice_32x32.png"));
        panel.CustomMinimumSize = new Vector2(490, 360);
        var title = overlay.GetNode<Label>("Overlay/Center/Panel/Content/Title");
        var reason = overlay.GetNode<Label>("Overlay/Center/Panel/Content/Reason");
        StyleLabel(title, 38, Ink, pixel: true);
        StyleLabel(reason, 24, Ink);
        var content = overlay.GetNode<VBoxContainer>("Overlay/Center/Panel/Content");
        if (!content.HasNode("ResultBanner"))
        {
            var banner = Image("ResultBanner", "dialogs/victory_banner_192x48.png", Vector2.Zero, new Vector2(384, 96), 2);
            banner.CustomMinimumSize = new Vector2(384, 96);
            content.AddChild(banner);
            content.MoveChild(banner, 0);
        }
        foreach (string action in new[] { "Rematch", "MainMenu" })
        {
            var btn = overlay.GetNode<Button>("Overlay/Center/Panel/Content/" + action);
            StyleButton(btn, 25);
            btn.CustomMinimumSize = new Vector2(0, 60);
        }
    }

    public static void ShowGameOverBanner(GameOverOverlay overlay, bool draw)
    {
        var banner = overlay.GetNodeOrNull<TextureRect>("Overlay/Center/Panel/Content/ResultBanner");
        if (banner is not null)
            banner.Texture = Texture(draw ? "dialogs/draw_banner_192x48.png" : "dialogs/victory_banner_192x48.png", 2);
    }
}