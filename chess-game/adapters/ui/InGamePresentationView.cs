using Chess.Core.Model;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

/// <summary>
/// Surroundings of the untouched chess board at its ORIGINAL 768x768 screen size.
/// NEVER scale BoardView: 96px tiles and 48px sprites render at native x2.
/// Only background, frame, decorations and sidebar are adapted around the board.
/// </summary>
public partial class InGamePresentationView : Control
{
    // A 768px board plus a 38px frame on each side requires 844px.
    // This desktop composition has a 1440x900 minimum *design* size.
    private const float ReferenceWidth = 1440f;
    private const float ReferenceHeight = 900f;
    private const float FrameLeft = 206f;
    private const float FrameTop = 24f;
    private const float FrameScale = 2f;
    private const float FrameInset = 19f * FrameScale;
    private const float BoardSize = 8f * 96f;
    private BoardView? _board;
    private SidePanelView? _sidebar;
    private MenuView? _menu;
    private Control _stage = null!;
    private Control? _foreground;
    private Control? _utilityStage;
    private TextureRect _backdrop = null!;
    private Label _whiteClock = null!;
    private Label _blackClock = null!;
    private Button _sound = null!;
    private Label _helpText = null!;
    private Panel _helpPanel = null!;
    private Panel _settingsPanel = null!;
    private ChessGame? _game;
    private NinePatchRect _whiteCard = null!;
    private NinePatchRect _blackCard = null!;
    private double _whiteElapsed;
    private double _blackElapsed;
    private int _whiteDisplayed = -1;
    private int _blackDisplayed = -1;
    private bool _muted;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        _backdrop = InGameUiSkin.Sprite("InGameBackground",
            "01_background/ingame_picada_1440x900.png", Vector2.Zero, new Vector2(1440, 900));
        AddChild(_backdrop);
        _stage = new Control { Name = "ArtworkStage", Size = new Vector2(1440, 900), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_stage);
        BuildArtwork();
        GetViewport().SizeChanged += Refit;
        Refit();
    }

    public void Configure(BoardView board, SidePanelView sidebar, MenuView menu)
    {
        _board = board;
        _sidebar = sidebar;
        _menu = menu;
        Refit();
    }

    /// <summary>Put utility buttons and popups above the sidebar, but below the menu/modal.</summary>
    public void InstallForeground(Node root, Node beforeMenu)
    {
        _foreground = new Control
        {
            Name = "InGameUtilities",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        root.AddChild(_foreground);
        root.MoveChild(_foreground, beforeMenu.GetIndex());
        _utilityStage = new Control
        {
            Name = "UtilityStage",
            Size = new Vector2(1440, 900),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _foreground.AddChild(_utilityStage);
        BuildUtilities();
        Refit();
    }

    public void ShowUtilities(bool visible)
    {
        if (_foreground is not null)
            _foreground.Visible = visible;
    }

    /// <summary>
    /// Time played per side in the current session. Not a competitive 10-minute
    /// chess clock: no timeout win rule exists in Chess.Core yet.
    /// </summary>
    public void Attach(ChessGame game)
    {
        _game = game;
        _whiteElapsed = 0;
        _blackElapsed = 0;
        _whiteDisplayed = -1;
        _blackDisplayed = -1;
        RefreshElapsedLabels();
    }

    public override void _Process(double delta)
    {
        if (!Visible || _game is null || _menu?.Visible == true ||
            _game.Status is not (GameStatus.Ongoing or GameStatus.Check))
            return;

        if (_game.Turn == PlayerColor.White)
            _whiteElapsed += delta;
        else
            _blackElapsed += delta;

        _whiteCard.Modulate = _game.Turn == PlayerColor.White ? Colors.White : new Color(0.84f, 0.84f, 0.84f);
        _blackCard.Modulate = _game.Turn == PlayerColor.Black ? Colors.White : new Color(0.84f, 0.84f, 0.84f);
        RefreshElapsedLabels();
    }

    private void RefreshElapsedLabels()
    {
        static string Format(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";
        int white = Math.Max(0, (int)_whiteElapsed);
        int black = Math.Max(0, (int)_blackElapsed);

        if (white != _whiteDisplayed)
        {
            _whiteDisplayed = white;
            _whiteClock.Text = Format(white);
        }
        if (black != _blackDisplayed)
        {
            _blackDisplayed = black;
            _blackClock.Text = Format(black);
        }
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= Refit;

    private void BuildArtwork()
    {
        void Art(string name, string file, float x, float y, float w, float h) =>
            _stage.AddChild(InGameUiSkin.Sprite(name, file, new Vector2(x, y), new Vector2(w, h)));

        Art("GameLogo", "res://assets/ui/logos/logo_cheese.png", 8, 18, 192, 64);
        Art("BoardFrame", "02_board_ui/frame_board_422x422.png",
            FrameLeft, FrameTop, 844, 844);

        float boardX = FrameLeft + FrameInset;
        float boardY = FrameTop + FrameInset;

        // BoardView draws the coordinate glyphs; place the badge art under the
        // actual glyph centers rather than guessing by using fixed left offsets.
        Font coordFont = PixelUiSkin.FontFor(UiFontWeight.Medium) ?? ThemeDB.FallbackFont;
        const int coordSize = 16;
        float baseline = boardY + BoardSize + 22f;
        float opticalCenterY = baseline - (coordFont.GetAscent(coordSize) - coordFont.GetDescent(coordSize)) / 2f;
        float fileBadgeY = Mathf.Min(Mathf.Round(opticalCenterY - 22f), FrameTop + 844f - 44f);
        for (int i = 0; i < 8; i++)
        {
            string rankText = (8 - i).ToString();
            float textWidth = coordFont.GetStringSize(rankText,
                HorizontalAlignment.Left, -1, coordSize).X;
            float rankTextCenter = boardX - 12f - textWidth / 2f;
            Art("RankBadge" + (8 - i), "02_board_ui/coordinate_badge_22x22.png",
                Mathf.Round(rankTextCenter - 22f), boardY + 96 * i + 26, 44, 44);
            Art("FileBadge" + i, "02_board_ui/coordinate_badge_22x22.png",
                boardX + 96 * i + 26, fileBadgeY, 44, 44);
        }

        // The 128x66 artwork is kept at its native height. Nine-patch extends
        // only the plain right-hand text field (not the icon box or the corners).
        NinePatchRect Card(string name, string path, int x, int y)
        {
            var card = new NinePatchRect
            {
                Name = name,
                Texture = InGameUiSkin.Texture(path),
                Position = new Vector2(x, y),
                Size = new Vector2(188, 66),
                PatchMarginLeft = 58,
                PatchMarginRight = 12,
                PatchMarginTop = 10,
                PatchMarginBottom = 10,
                MouseFilter = MouseFilterEnum.Ignore,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest
            };
            _stage.AddChild(card);
            return card;
        }

        _whiteCard = Card("WhiteCard", "02_board_ui/player_card_white_128x66.png", 12, 343);
        _blackCard = Card("BlackCard", "02_board_ui/player_card_black_128x66.png", 12, 463);
        Art("WhiteBadge", "02_board_ui/player_cheese_badge_32x32.png", 31, 360, 32, 32);
        Art("BlackBadge", "02_board_ui/player_salami_badge_32x32.png", 31, 480, 32, 32);
        Art("WhiteTimerIcon", "05_icons/timer_16.png", 79, 382, 16, 16);
        Art("BlackTimerIcon", "05_icons/timer_16.png", 79, 502, 16, 16);

        _stage.AddChild(InGameUiSkin.Text("WhiteName", "BLANCAS", new Vector2(78, 350),
            new Vector2(112, 24), 17, InGameUiSkin.Ink, UiFontWeight.SemiBold));
        _stage.AddChild(InGameUiSkin.Text("BlackName", "NEGRAS", new Vector2(78, 470),
            new Vector2(112, 24), 17, InGameUiSkin.Ivory, UiFontWeight.SemiBold));
        _whiteClock = InGameUiSkin.Text("WhiteClock", "00:00", new Vector2(103, 378),
            new Vector2(89, 27), 18, InGameUiSkin.Ink);
        _blackClock = InGameUiSkin.Text("BlackClock", "00:00", new Vector2(103, 498),
            new Vector2(89, 27), 18, InGameUiSkin.Ivory);
        const string timerHint = "Tiempo jugado en esta sesión; no es una partida a contrarreloj ni se guarda.";
        _whiteClock.TooltipText = timerHint;
        _blackClock.TooltipText = timerHint;
        _stage.AddChild(_whiteClock);
        _stage.AddChild(_blackClock);
    }

    private void BuildUtilities()
    {
        Button Utility(string name, string icon, int x, Action onClick)
        {
            var button = new Button
            {
                Name = name,
                Position = new Vector2(x, 6),
                Size = new Vector2(32, 32),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest
            };
            var idle = InGameUiSkin.Texture("03_sidebar/utility_button_idle_32x32.png");
            var hover = InGameUiSkin.Texture("03_sidebar/utility_button_hover_32x32.png");
            button.AddThemeStyleboxOverride("normal", new StyleBoxTexture { Texture = idle });
            button.AddThemeStyleboxOverride("hover", new StyleBoxTexture { Texture = hover });
            button.AddThemeStyleboxOverride("pressed", new StyleBoxTexture { Texture = hover });
            button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            button.Pressed += onClick;
            _utilityStage!.AddChild(button);
            button.AddChild(InGameUiSkin.Sprite("Icon", "05_icons/" + icon + "_16.png",
                new Vector2(8, 8), new Vector2(16, 16)));
            return button;
        }

        var help = Utility("HelpButton", "help", 1281, () => _helpPanel.Visible = !_helpPanel.Visible);
        help.TooltipText = "Ayuda";
        var settings = Utility("SettingsButton", "settings", 1324, () => _settingsPanel.Visible = !_settingsPanel.Visible);
        settings.TooltipText = "Opciones de la partida";
        _sound = Utility("SoundButton", "sound_on", 1367, ToggleSound);
        _sound.TooltipText = "Activar o silenciar sonido";

        _helpPanel = Popup("HelpPopup", new Vector2(1090, 52), new Vector2(312, 100),
            "JUGADA: elegi una pieza y despues su destino.\nGUARDAR: conserva el estado de la partida.");
        _settingsPanel = Popup("SettingsPopup", new Vector2(1090, 52), new Vector2(312, 112),
            "OPCIONES DE PARTIDA\nSonido: usa el icono del altavoz.\nOponente: cambia con el boton junto a turno.");
        _helpText = _helpPanel.GetNode<Label>("Text");
    }

    private Panel Popup(string name, Vector2 position, Vector2 size, string text)
    {
        var panel = new Panel { Name = name, Position = position, Size = size, Visible = false,
            MouseFilter = MouseFilterEnum.Pass };
        panel.AddThemeStyleboxOverride("panel",
            PixelUiSkin.NineSlice("panels/parchment_9slice_32x32.png", sourceMargin: 8, scale: 1));
        panel.AddChild(InGameUiSkin.Text("Text", text, new Vector2(10, 8),
            size - new Vector2(20, 16), 12, InGameUiSkin.Ink, UiFontWeight.Regular));
        _utilityStage!.AddChild(panel);
        return panel;
    }

    private void ToggleSound()
    {
        _muted = !_muted;
        AudioServer.SetBusMute(0, _muted);
        var icon = _sound.GetNode<TextureRect>("Icon");
        icon.Texture = InGameUiSkin.Texture(_muted ? "05_icons/sound_off_16.png" : "05_icons/sound_on_16.png");
    }

    private void Refit()
    {
        if (_stage is null || _backdrop is null) return;
        Vector2 viewport = GetViewportRect().Size;
        if (viewport.X < 1 || viewport.Y < 1) return;
        Size = viewport;

        // Only the decorative backdrop fits the window. Never transform the board.
        bool wide = viewport.X / viewport.Y >= 1.7f;
        _backdrop.Texture = InGameUiSkin.Texture(wide
            ? "01_background/ingame_picada_960x540.png"
            : "01_background/ingame_picada_1440x900.png");
        if (_backdrop.Texture is { } texture)
        {
            // Uniform cover (never independent X/Y stretching of background art).
            Vector2 source = texture.GetSize();
            float cover = Mathf.Max(viewport.X / source.X, viewport.Y / source.Y);
            _backdrop.Size = source * cover;
            _backdrop.Position = (viewport - _backdrop.Size) / 2f;
        }

        // Native desktop coordinates. Larger windows center the whole layout.
        // Smaller windows can crop the surroundings, but MUST NOT shrink chess.
        Vector2 offset = new(
            Mathf.Max(0f, (viewport.X - ReferenceWidth) * 0.5f),
            Mathf.Max(0f, (viewport.Y - ReferenceHeight) * 0.5f));

        _stage.Position = offset;
        _stage.Scale = Vector2.One;

        if (_foreground is not null && _utilityStage is not null)
        {
            _foreground.Size = viewport;
            _utilityStage.Position = offset;
            _utilityStage.Scale = Vector2.One;
        }

        if (_board is not null)
        {
            _board.Position = offset + new Vector2(
                FrameLeft + FrameInset, FrameTop + FrameInset);
            _board.Scale = Vector2.One; // NON-NEGOTIABLE: 8 * 96 = 768 native pixels.
        }
        if (_sidebar is not null)
        {
            _sidebar.Position = offset + new Vector2(1080, 42);
            _sidebar.Scale = Vector2.One;
        }
    }
}
