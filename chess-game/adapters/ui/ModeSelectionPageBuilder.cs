using Godot;

namespace Cheesse.Game.Adapters.Ui;

/// <summary>
/// Pixel-perfect mode selection, built from individual supplied PNG components.
/// The four precomposed card PNGs remain in the repository only as references.
/// This UI does not modify gameplay rules or the 768px chess board.
/// </summary>
internal static class ModeSelectionPageBuilder
{
    private const int PixelScale = 3;
    private static readonly Vector2 CardSize = new(96 * PixelScale, 112 * PixelScale);
    private const string Assets = "res://assets/ui/mode_selection/";
    private static Texture2D? _wallTexture;

    private readonly record struct Mode(
        string Name, string Illustration, string Summary, bool Available, int TitleSize);

    private static readonly Mode[] Modes =
    [
        new("CLASICO", "classic", "AJEDREZ TRADICIONAL", true, 30),
        new("REY DE LA PICADA", "picada", "LLEVA AL REY AL CENTRO", true, 24),
        new("QUESO SUIZO", "swiss", "EVITA LOS AGUJEROS", false, 28),
        new("FONDUE", "fondue", "CAPTURAS QUE DERRITEN", false, 32)
    ];

    public static void Build(Control page, Action onClassic, Action onPicada, Action onBack)
    {
        page.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        page.AddChild(new TextureRect
        {
            Name = "BrickWall",
            Texture = BrickWallTexture(),
            Position = Vector2.Zero,
            Size = new Vector2(1440, 900),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        // Compact 128x32 logo at exact x3, centered above the mode heading.
        page.AddChild(PixelUiSkin.Image("GameLogo", "logos/logo_cheese_compact.png",
            new Vector2(528, 75), new Vector2(384, 96), 3));
        page.AddChild(PixelUiSkin.Label("Heading", "ELEGIR MODO DE JUEGO",
            new Vector2(320, 188), new Vector2(800, 50), 32,
            pixel: true, light: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.SemiBold));
        page.AddChild(PixelUiSkin.Label("Hint", "SELECCIONA TU DESAFIO",
            new Vector2(320, 245), new Vector2(800, 42), 26,
            pixel: true, light: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.Medium));

        // 96x112 native art at exact x3; ALL four cards use the same dimensions.
        // Total width = 4 * 288 + 3 * 42 = 1278, centered in 1440.
        for (int i = 0; i < Modes.Length; i++)
        {
            const int firstX = 81;
            const int stepX = 330;
            int x = firstX + i * stepX;
            Action? selected = i switch { 0 => onClassic, 1 => onPicada, _ => null };
            BuildCard(page, i, Modes[i], new Vector2(x, 329), selected);
            page.AddChild(PixelUiSkin.Label("Description" + i, Modes[i].Summary,
                new Vector2(x - 9, 697), new Vector2(CardSize.X + 18, 61), 19,
                pixel: false, light: true, alignment: HorizontalAlignment.Center,
                weight: UiFontWeight.Regular));
        }

        // Back button shares the top header row with the centered logo.
        page.AddChild(PixelUiSkin.Button("Back", "VOLVER",
            new Vector2(66, 86), new Vector2(200, 57), onBack,
            icon: "back", fontSize: 24, variant: "small"));
    }

    private static void BuildCard(Control page, int index, Mode mode,
        Vector2 position, Action? onSelect)
    {
        var button = new Button
        {
            Name = "Mode" + index,
            Position = position,
            Size = CardSize,
            Text = string.Empty,
            Disabled = !mode.Available,
            FocusMode = mode.Available ? Control.FocusModeEnum.All : Control.FocusModeEnum.None,
            MouseDefaultCursorShape = mode.Available
                ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow,
            TooltipText = mode.Available
                ? "Iniciar una partida de ajedrez clasico"
                : mode.Name + ": proximamente",
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        page.AddChild(button);

        void Art(string name, string file, Vector2 nativePos, Vector2 nativeSize,
            int scale = PixelScale)
        {
            var image = PixelUiSkin.Image(name, Assets + file, nativePos * PixelScale,
                nativeSize * scale, scale);
            button.AddChild(image);
        }

        Art("Base", "components/card_base_96x112.png", Vector2.Zero, new Vector2(96, 112));
        Art("Illustration", "illustrations/" + mode.Illustration + "_64x64.png",
            new Vector2(16, 15), new Vector2(64, 64));
        Art("TitlePlaque", "components/title_plaque_78x18.png",
            new Vector2(9, 78), new Vector2(78, 18));
        var title = PixelUiSkin.Label("ModeName", mode.Name,
            new Vector2(9 * PixelScale, 78 * PixelScale), new Vector2(78 * PixelScale, 18 * PixelScale),
            mode.TitleSize, pixel: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.SemiBold);
        title.AutowrapMode = TextServer.AutowrapMode.Off;
        button.AddChild(title);

        string cta = mode.Available ? "components/cta_available_76x10.png" :
            "components/cta_locked_76x10.png";
        Art("ActionPlate", cta, new Vector2(10, 99), new Vector2(76, 10));
        button.AddChild(PixelUiSkin.Label("State",
            mode.Available ? "JUGAR" : "PROXIMAMENTE",
            new Vector2(10 * PixelScale, 98 * PixelScale), new Vector2(76 * PixelScale, 11 * PixelScale),
            17, pixel: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.Medium));

        if (!mode.Available)
            Art("LockIcon", "components/lock_icon_16x16.png",
                new Vector2(76, 6), new Vector2(16, 16), scale: 2);

        Art("HoverOverlay", "components/focus_hover_96x112.png",
            Vector2.Zero, new Vector2(96, 112));
        Art("SelectedOverlay", "components/focus_selected_96x112.png",
            Vector2.Zero, new Vector2(96, 112));
        var hover = button.GetNode<TextureRect>("HoverOverlay");
        var selected = button.GetNode<TextureRect>("SelectedOverlay");
        hover.Visible = false;
        selected.Visible = false;

        if (mode.Available && onSelect is not null)
        {
            button.MouseEntered += () =>
            {
                hover.Visible = true;
                button.GetNode<TextureRect>("ActionPlate").Texture =
                    PixelUiSkin.Texture(Assets + "components/cta_hover_76x10.png", PixelScale);
            };
            button.MouseExited += () =>
            {
                hover.Visible = false;
                button.GetNode<TextureRect>("ActionPlate").Texture =
                    PixelUiSkin.Texture(Assets + "components/cta_available_76x10.png", PixelScale);
            };
            button.FocusEntered += () => selected.Visible = true;
            button.FocusExited += () => selected.Visible = false;
            button.ButtonDown += () => button.GetNode<TextureRect>("ActionPlate").Texture =
                PixelUiSkin.Texture(Assets + "components/cta_pressed_76x10.png", PixelScale);
            button.ButtonUp += () => button.GetNode<TextureRect>("ActionPlate").Texture =
                PixelUiSkin.Texture(Assets + "components/cta_hover_76x10.png", PixelScale);
            button.Pressed += onSelect;
        }
    }

    private static Texture2D BrickWallTexture()
    {
        if (_wallTexture is not null) return _wallTexture;
        const int width = 480, height = 300;
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(new Color("301b31"));
        void Paint(int x, int y, int w, int h, Color color)
        {
            var rect = new Rect2I(x, y, w, h);
            image.FillRect(rect, color);
        }
        Color grout = new("47263f"), shadow = new("241723"), wood = new("98542f"), highlight = new("bf7941");
        for (int row = 0, y = 22; y < 285; row++, y += 16)
        {
            int start = row % 2 == 0 ? -18 : 8;
            for (int x = start; x < width; x += 40)
            {
                int px = Math.Max(x, 5);
                int right = Math.Min(x + 27, width - 5);
                if (right > px) Paint(px, y, right - px, 1, grout);
                if (x >= 5 && x < width - 5) Paint(x, y, 1, 8, grout);
            }
        }
        // Wooden rails & posts in native pixel units; no new raster assets.
        Paint(0, 0, width, 13, shadow);
        Paint(3, 2, width - 6, 8, wood);
        Paint(3, 2, width - 6, 1, highlight);
        Paint(0, height - 14, width, 14, shadow);
        Paint(0, height - 11, width, 9, wood);
        Paint(0, height - 11, width, 1, highlight);
        for (int x = 23; x < width - 5; x += 110)
        {
            Paint(x, 0, 3, 13, shadow);
            Paint(x, height - 14, 3, 7, shadow);
        }
        // Small pennants echo the provided reference without requiring a new asset.
        Paint(17, 15, 43, 17, new Color("5a2537"));
        Paint(24, 32, 29, 14, new Color("5a2537"));
        Paint(420, 15, 43, 17, new Color("5a2537"));
        Paint(428, 32, 28, 14, new Color("5a2537"));
        image.Resize(1440, 900, Image.Interpolation.Nearest);
        _wallTexture = ImageTexture.CreateFromImage(image);
        return _wallTexture;
    }
}