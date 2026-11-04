using Godot;

namespace Cheesse.Game.Adapters.Ui;

/// <summary>
/// Pixel-art presentation for the existing Godot controls. Only visual overrides:
/// no chess rules, input behavior, save logic or scene node paths are changed.
/// Original PNGs remain untouched; nearest-neighbor x2 images are cached in memory.
/// </summary>
internal static class PixelUiSkin
{
    private const string Ui = "res://assets/ui/";
    private const string LocalFont = "res://assets/ui/fonts/Pixeboy-z8XGD.ttf";
    private static readonly Dictionary<string, Texture2D> Cache = new();
    private static Font? _pixelFont;
    private static bool _fontChecked;

    private static readonly Color Ink = new("39251d");
    private static readonly Color Cream = new("f9e6bf");
    private static readonly Color MutedCream = new("e2ba82");

    public static void ApplyMenu(MenuView menu)
    {
        menu.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

        var background = Load(Ui + "brand/menu_background.png");
        if (background is not null && !menu.HasNode("PixelBackground"))
        {
            var texture = new TextureRect
            {
                Name = "PixelBackground",
                Texture = background,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorRight = 1,
                AnchorBottom = 1,
            };
            menu.AddChild(texture);
            menu.MoveChild(texture, 1); // Behind Center, in front of fallback ColorRect.
        }

        var panel = menu.GetNode<PanelContainer>("Center/Panel");
        panel.CustomMinimumSize = new Vector2(548, 0);
        panel.AddThemeStyleboxOverride("panel", MakeStyle("panels/wood_9slice_32x32.png", 8, 2));

        var margin = menu.GetNode<MarginContainer>("Center/Panel/Margin");
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);

        const string basePath = "Center/Panel/Margin/Options/";
        var options = menu.GetNode<VBoxContainer>("Center/Panel/Margin/Options");
        options.AddThemeConstantOverride("separation", 6);
        menu.GetNode<Control>(basePath + "Spacer").CustomMinimumSize = Vector2.Zero;

        var title = menu.GetNode<Label>(basePath + "Title");
        var logo = Load(Ui + "brand/logo_cheesse.png", 2);
        if (logo is not null && !options.HasNode("PixelBranding"))
        {
            title.Visible = false;
            var wrapper = new CenterContainer
            {
                Name = "PixelBranding",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            var image = new TextureRect
            {
                Name = "Logo",
                Texture = logo,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(384, 128),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            wrapper.AddChild(image);
            options.AddChild(wrapper);
            options.MoveChild(wrapper, 0);
        }

        LabelStyle(menu.GetNode<Label>(basePath + "Subtitle"), 16, MutedCream, pixelFont: false);
        LabelStyle(menu.GetNode<Label>(basePath + "AppearanceLabel"), 18, Cream);
        LabelStyle(menu.GetNode<Label>(basePath + "ThemeDetails/BoardLabel"), 15, Cream, pixelFont: false);
        LabelStyle(menu.GetNode<Label>(basePath + "ThemeDetails/WhiteLabel"), 15, Cream, pixelFont: false);
        LabelStyle(menu.GetNode<Label>(basePath + "ThemeDetails/BlackLabel"), 15, Cream, pixelFont: false);
        LabelStyle(menu.GetNode<Label>(basePath + "ThemeDetails/ThemeNote"), 13, MutedCream, pixelFont: false);
        LabelStyle(menu.GetNode<Label>(basePath + "Message"), 16, Cream, pixelFont: false);

        var themeDetails = menu.GetNode<VBoxContainer>(basePath + "ThemeDetails");
        themeDetails.AddThemeConstantOverride("separation", 3);

        foreach (var name in new[] { "Appearance", "ThemeDetails/Board", "ThemeDetails/White", "ThemeDetails/Black" })
        {
            var select = menu.GetNode<OptionButton>(basePath + name);
            ButtonStyle(select, medium: true, fontSize: 16, pixelFont: false);
            select.CustomMinimumSize = new Vector2(0, 34);
        }

        foreach (var name in new[] { "Human", "Random", "Continue" })
        {
            var button = menu.GetNode<Button>(basePath + name);
            ButtonStyle(button, medium: false, fontSize: 19);
            button.CustomMinimumSize = new Vector2(0, 46);
        }

        if (!options.HasNode("ModePreview"))
            AddModePreview(options);
    }

    public static void SetModePreviewVisible(MenuView menu, bool visible)
    {
        var preview = menu.GetNodeOrNull<HBoxContainer>("Center/Panel/Margin/Options/ModePreview");
        if (preview is not null)
            preview.Visible = visible;
    }

    public static void ApplySidePanel(SidePanelView panel)
    {
        panel.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        panel.AddThemeStyleboxOverride("panel", MakeStyle("panels/parchment_9slice_32x32.png", 8, 2));

        const string basePath = "Margin/Content/";
        LabelStyle(panel.GetNode<Label>(basePath + "Title"), 29, Ink);
        LabelStyle(panel.GetNode<Label>(basePath + "Turn"), 22, Ink, pixelFont: false);
        LabelStyle(panel.GetNode<Label>(basePath + "Status"), 19, Ink, pixelFont: false);
        LabelStyle(panel.GetNode<Label>(basePath + "Message"), 15, Ink, pixelFont: false);
        LabelStyle(panel.GetNode<Label>(basePath + "Help"), 14, Ink, pixelFont: false);

        foreach (var name in new[] { "History/Undo", "History/Redo", "Files/Save", "Files/Load", "NewGame", "Menu" })
            ButtonStyle(panel.GetNode<Button>(basePath + name), medium: true, fontSize: 17);

        var opponent = panel.GetNode<CheckButton>(basePath + "Random");
        opponent.AddThemeColorOverride("font_color", Ink);
        opponent.AddThemeColorOverride("font_hover_color", Ink);
        var on = Load(Ui + "controls/toggle_on.png");
        var off = Load(Ui + "controls/toggle_off.png");
        if (on is not null) opponent.AddThemeIconOverride("checked", on);
        if (off is not null) opponent.AddThemeIconOverride("unchecked", off);
    }

    private static void AddModePreview(VBoxContainer options)
    {
        var row = new HBoxContainer
        {
            Name = "ModePreview",
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", 8);
        options.AddChild(row);
        // Logo, hidden Title, Subtitle, then the mode preview.
        options.MoveChild(row, options.HasNode("PixelBranding") ? 3 : 2);

        var choices = new (string Image, string Caption)[]
        {
            ("mode_classic_icon.png", "CLASICO"),
            ("mode_picada_king_icon.png", "PICADA"),
            ("mode_swiss_cheese_icon.png", "SUIZO"),
            ("mode_fondue_icon.png", "FONDUE"),
        };
        for (int index = 0; index < choices.Length; index++)
        {
            var card = new Control
            {
                Name = $"Mode{index}",
                CustomMinimumSize = new Vector2(96, 112),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                TooltipText = index == 0 ? "Ajedrez clasico" : "Modo especial: proximamente",
                Modulate = index == 0 ? Colors.White : new Color(1, 1, 1, 0.66f),
            };
            var frame = new TextureRect
            {
                Name = "Frame",
                Texture = Load(Ui + (index == 0 ? "cards/card_selected_96x112.png" : "cards/card_frame_96x112.png")),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Keep,
                Position = Vector2.Zero,
                Size = new Vector2(96, 112),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            card.AddChild(frame);
            var icon = new TextureRect
            {
                Name = "Icon",
                Texture = Load(Ui + "modes/" + choices[index].Image),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Keep,
                Position = new Vector2(16, 10),
                Size = new Vector2(64, 64),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            card.AddChild(icon);
            var caption = new Label
            {
                Name = "Caption",
                Text = choices[index].Caption,
                HorizontalAlignment = HorizontalAlignment.Center,
                Position = new Vector2(5, 80),
                Size = new Vector2(86, 20),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            LabelStyle(caption, 13, Cream);
            card.AddChild(caption);
            row.AddChild(card);
        }
    }

    private static void LabelStyle(Label label, int size, Color color, bool pixelFont = true)
    {
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", size);
        if (pixelFont && PixelFont() is { } font)
            label.AddThemeFontOverride("font", font);
    }

    private static void ButtonStyle(Button button, bool medium, int fontSize, bool pixelFont = true)
    {
        string folder = medium ? "buttons/medium_120x24/" : "buttons/large_160x24/";
        button.AddThemeStyleboxOverride("normal", MakeStyle(folder + "idle.png", 8, 2));
        button.AddThemeStyleboxOverride("hover", MakeStyle(folder + "hover.png", 8, 2));
        button.AddThemeStyleboxOverride("pressed", MakeStyle(folder + "pressed.png", 8, 2));
        button.AddThemeStyleboxOverride("disabled", MakeStyle(folder + "disabled.png", 8, 2));
        button.AddThemeStyleboxOverride("focus", MakeStyle(folder + "hover.png", 8, 2));
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", Ink);
        button.AddThemeColorOverride("font_pressed_color", Ink);
        button.AddThemeColorOverride("font_focus_color", Ink);
        button.AddThemeColorOverride("font_disabled_color", new Color("665547"));
        button.AddThemeFontSizeOverride("font_size", fontSize);
        if (pixelFont && PixelFont() is { } font)
            button.AddThemeFontOverride("font", font);
    }

    private static StyleBox MakeStyle(string relativePath, int sourceMargin, int scale)
    {
        var texture = Load(Ui + relativePath, scale);
        if (texture is null)
            return new StyleBoxFlat { BgColor = new Color("c6953f") };

        var margin = sourceMargin * scale;
        return new StyleBoxTexture
        {
            Texture = texture,
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Tile,
            TextureMarginLeft = margin,
            TextureMarginTop = margin,
            TextureMarginRight = margin,
            TextureMarginBottom = margin,
            ContentMarginLeft = margin,
            ContentMarginTop = 5,
            ContentMarginRight = margin,
            ContentMarginBottom = 5,
        };
    }

    private static Font? PixelFont()
    {
        if (_fontChecked) return _pixelFont;
        _fontChecked = true;
        // Pixeboy is an optional locally-installed personal-use font, never committed.
        if (ResourceLoader.Exists(LocalFont))
            _pixelFont = GD.Load<Font>(LocalFont);
        return _pixelFont;
    }

    private static Texture2D? Load(string path, int scale = 1)
    {
        string key = path + "@" + scale;
        if (Cache.TryGetValue(key, out var cached))
            return cached;
        if (!ResourceLoader.Exists(path))
            return null;
        var source = GD.Load<Texture2D>(path);
        if (source is null) return null;
        Texture2D result = source;
        if (scale > 1)
        {
            var image = source.GetImage();
            image.Resize(source.GetWidth() * scale, source.GetHeight() * scale, Image.Interpolation.Nearest);
            result = ImageTexture.CreateFromImage(image);
        }
        Cache[key] = result;
        return result;
    }
}