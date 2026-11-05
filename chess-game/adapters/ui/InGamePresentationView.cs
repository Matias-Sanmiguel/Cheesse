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
    private Control _picadaBadge = null!;
    private NinePatchRect _whiteCard = null!;
    private NinePatchRect _blackCard = null!;
    private double _whiteElapsed;
    private double _blackElapsed;
    private int _whiteDisplayed = -1;
    private int _blackDisplayed = -1;
    private bool _muted;

    /// <summary>Seconds played in this session, across both sides; not a countdown.</summary>
    public int TotalElapsedSeconds => Math.Max(0, (int)(_whiteElapsed + _blackElapsed));

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
    /// Keep the existing player cards and show the domain countdown for timed games.
    /// Unlimited games retain the per-side session timers.
    /// </summary>
    public void Attach(ChessGame game)
    {
        _game = game;
        _picadaBadge.Visible = game.Mode == GameMode.ReyDeLaPicada;
        _whiteElapsed = 0;
        _blackElapsed = 0;
        _whiteDisplayed = -1;
        _blackDisplayed = -1;
        RefreshClocks();
    }

    public override void _Process(double delta)
    {
        if (!Visible || _game is null || _menu?.Visible == true ||
            _game.Status is not (GameStatus.Ongoing or GameStatus.Check))
            return;

        if (!_game.Caretaker.CanRedo &&
            (_game.TimeControl.InitialSeconds == 0 || _game.ClockState.Running))
        {
            if (_game.Turn == PlayerColor.White) _whiteElapsed += delta;
            else _blackElapsed += delta;
        }

        _whiteCard.Modulate = _game.Turn == PlayerColor.White ? Colors.White : new Color(0.84f, 0.84f, 0.84f);
        _blackCard.Modulate = _game.Turn == PlayerColor.Black ? Colors.White : new Color(0.84f, 0.84f, 0.84f);
        RefreshClocks();
    }

    public void RefreshClocks()
    {
        if (_game is null) return;
        static string Format(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";
        bool timed = _game.TimeControl.InitialSeconds > 0;
        int white = Math.Max(0, timed ? (int)Math.Ceiling(_game.ClockState.WhiteSeconds) : (int)_whiteElapsed);
        int black = Math.Max(0, timed ? (int)Math.Ceiling(_game.ClockState.BlackSeconds) : (int)_blackElapsed);
        string hint = timed ? "Tiempo restante de la partida; se conserva al guardar." :
            "Tiempo jugado en esta sesión; no es una partida a contrarreloj ni se guarda.";
        _whiteClock.TooltipText = _blackClock.TooltipText = hint;

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

        // The 219x64 source is wider than the space before the board frame.
        // Fit proportionally inside the original 192px HUD slot, never distort it.
        var gameLogo = InGameUiSkin.Sprite("GameLogo",
            "res://assets/ui/logos/logo_cheese.png", new Vector2(8, 18), new Vector2(192, 64));
        gameLogo.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _stage.AddChild(gameLogo);
        // The PNG has a transparent 384x384 opening. Draw only its wooden
        // border in front of the board, so the chess squares cannot cut off
        // the inner rim. It never captures mouse input and has no effect on
        // BoardView (which stays 768x768 at scale 1).
        var frame = InGameUiSkin.Sprite("BoardFrame",
            "02_board_ui/frame_board_422x422.png",
            new Vector2(FrameLeft, FrameTop), new Vector2(844, 844));
        frame.ZIndex = 1;
        frame.MouseFilter = MouseFilterEnum.Ignore;
        _stage.AddChild(frame);

        float boardX = FrameLeft + FrameInset;
        float boardY = FrameTop + FrameInset;

        // Drawing the wooden rim above BoardView must NOT hide the rank/file
        // badges or their text. The board still draws its native coordinates;
        // re-create those labels on a dedicated foreground layer because the
        // original DrawString calls are underneath the opaque rim.
        // Draw order: board 0 < wooden frame 1 < coordinate badges 2 < text 3.
        const int coordSize = 16;
        const float badgeSize = 44f; // source 22x22 at integer x2 (no pixel distortion)
        Font coordFont = PixelUiSkin.FontFor(UiFontWeight.Medium) ?? ThemeDB.FallbackFont;
        float baseline = boardY + BoardSize + 22f;
        float opticalCenterY = baseline -
            (coordFont.GetAscent(coordSize) - coordFont.GetDescent(coordSize)) / 2f;
        float fileBadgeY = Mathf.Min(Mathf.Round(opticalCenterY - badgeSize / 2f),
            FrameTop + 844f - badgeSize);

        void Coordinate(string name, string value, Vector2 position)
        {
            var badge = InGameUiSkin.Sprite(name,
                "02_board_ui/coordinate_badge_22x22.png", position,
                new Vector2(badgeSize, badgeSize));
            badge.ZIndex = 2;
            badge.MouseFilter = MouseFilterEnum.Ignore;
            _stage.AddChild(badge);

            var text = InGameUiSkin.Text(name + "Text", value, position,
                new Vector2(badgeSize, badgeSize), coordSize,
                new Color("E9E3D6"), UiFontWeight.Medium,
                HorizontalAlignment.Center);
            text.ZIndex = 3;
            text.MouseFilter = MouseFilterEnum.Ignore;
            _stage.AddChild(text);
        }

        for (int i = 0; i < 8; i++)
        {
            string rank = (8 - i).ToString();
            float textWidth = coordFont.GetStringSize(rank,
                HorizontalAlignment.Left, -1, coordSize).X;
            float rankTextCenter = boardX - 12f - textWidth / 2f;
            Coordinate("RankBadge" + rank, rank,
                new Vector2(Mathf.Round(rankTextCenter - badgeSize / 2f),
                    boardY + 96 * i + 26));
            Coordinate("FileBadge" + i, ((char)('a' + i)).ToString(),
                new Vector2(boardX + 96 * i + 26, fileBadgeY));
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

        // Match the actual chess orientation: black plays at the top, white at
        // the bottom. Both cards share the same internal spacing and hierarchy.
        // Only these decorations move; the board remains native-size.
        _blackCard = Card("BlackCard", "02_board_ui/player_card_black_aligned_128x66.png", 12, 184);
        _whiteCard = Card("WhiteCard", "02_board_ui/player_card_white_aligned_128x66.png", 12, 642);
        // The aligned card variants shift the BUILT-IN 37x36 icon well +4 px
        // without distorting the card or changing its nine-patch margins.
        // Move each 32x32 team badge by the exact same amount; the background
        // square and foreground icon must always stay together.
        Art("BlackBadge", "02_board_ui/player_salami_badge_32x32.png", 26, 200, 32, 32);
        Art("WhiteBadge", "02_board_ui/player_cheese_badge_32x32.png", 26, 658, 32, 32);

        // Center names in the right-hand text area. Bring the clock icon and
        // digits into a compact second row with breathing room from the border.
        Art("BlackTimerIcon", "05_icons/timer_16.png", 88, 219, 16, 16);
        Art("WhiteTimerIcon", "05_icons/timer_16.png", 88, 677, 16, 16);

        _stage.AddChild(InGameUiSkin.Text("BlackName", "NEGRAS", new Vector2(68, 197),
            new Vector2(116, 20), 17, InGameUiSkin.Ivory, UiFontWeight.SemiBold,
            HorizontalAlignment.Center));
        _stage.AddChild(InGameUiSkin.Text("WhiteName", "BLANCAS", new Vector2(68, 655),
            new Vector2(116, 20), 17, InGameUiSkin.Ink, UiFontWeight.SemiBold,
            HorizontalAlignment.Center));
        _blackClock = InGameUiSkin.Text("BlackClock", "00:00", new Vector2(111, 218),
            new Vector2(70, 22), 18, InGameUiSkin.Ivory);
        _whiteClock = InGameUiSkin.Text("WhiteClock", "00:00", new Vector2(111, 676),
            new Vector2(70, 22), 18, InGameUiSkin.Ink);
        const string timerHint = "Tiempo jugado en esta sesión; no es una partida a contrarreloj ni se guarda.";
        _whiteClock.TooltipText = timerHint;
        _blackClock.TooltipText = timerHint;
        _stage.AddChild(_whiteClock);
        _stage.AddChild(_blackClock);

        // A mode-specific goal reminder in the free space below the logo.
        // It does not alter the 768x768 board or the sidebar.
        _picadaBadge = new Control
        {
            Name = "PicadaBadge",
            Position = new Vector2(12, 93),
            Size = new Vector2(188, 78),
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        _stage.AddChild(_picadaBadge);
        var badgePanel = PixelUiSkin.MakePanel("Frame", "dark", Vector2.Zero, new Vector2(188, 78));
        badgePanel.MouseFilter = MouseFilterEnum.Ignore;
        _picadaBadge.AddChild(badgePanel);
        _picadaBadge.AddChild(InGameUiSkin.Sprite("Crown",
            "res://assets/ui/icons/crown_16x16.png", new Vector2(12, 9), new Vector2(32, 32)));
        _picadaBadge.AddChild(InGameUiSkin.Text("Title", "REY DE LA PICADA",
            new Vector2(51, 11), new Vector2(132, 27), 15, PixelUiSkin.Gold,
            UiFontWeight.SemiBold));
        _picadaBadge.AddChild(InGameUiSkin.Text("Goal", "OBJETIVO: D4 E4 D5 E5",
            new Vector2(13, 45), new Vector2(174, 23), 13, PixelUiSkin.Cream,
            UiFontWeight.Medium));
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
        _settingsPanel = Popup("SettingsPopup", new Vector2(1090, 52), new Vector2(312, 484),
            "OPCIONES DE PARTIDA\n\nSONIDO: boton de altavoz superior.");
        _settingsPanel.GetNode<Label>("Text").Size = new Vector2(292, 70);

        // The SAME CheckButton instance is moved from the sidebar into settings:
        // its Toggled event, save/load synchronization and opponent logic remain
        // connected. No duplicate controls or competing state.
        var opponent = _sidebar!.GetNode<CheckButton>("Margin/Content/Random");
        opponent.Reparent(_settingsPanel, false);
        opponent.Name = "Random";
        opponent.Position = new Vector2(16, 85);
        opponent.Size = new Vector2(280, 28);
        opponent.Text = "NEGRAS CON STOCKFISH";
        opponent.TooltipText = "La IA juega con las piezas negras";
        opponent.AddThemeFontSizeOverride("font_size", 16);
        opponent.AddThemeColorOverride("font_color", InGameUiSkin.Ink);
        if (PixelUiSkin.FontFor(UiFontWeight.Medium) is { } font)
            opponent.AddThemeFontOverride("font", font);

        _settingsPanel.AddChild(InGameUiSkin.Text("OpponentHelp",
            "Se puede cambiar durante la partida.",
            new Vector2(16, 118), new Vector2(280, 20), 13,
            InGameUiSkin.Ink, UiFontWeight.Regular));
        var difficulty = _sidebar.GetNode<OptionButton>("Margin/Content/Difficulty");
        difficulty.Reparent(_settingsPanel, false);
        difficulty.Position = new Vector2(16, 176);
        difficulty.Size = new Vector2(280, 38);
        PixelUiSkin.StyleOption(difficulty);
        difficulty.AddThemeFontSizeOverride("font_size", 18);
        _settingsPanel.AddChild(InGameUiSkin.Text("DifficultyLabel", "NIVEL DE STOCKFISH",
            new Vector2(16, 145), new Vector2(280, 26), 17, InGameUiSkin.Ink));
        _sidebar.GetNode<Button>("Margin/Content/TextFiles/Import").Text = "IMPORTAR";
        _sidebar.GetNode<Button>("Margin/Content/TextFiles/Export").Text = "EXPORTAR";
        foreach (var (name, y, height) in new[] { ("TextFiles", 235, 38), ("EndActions", 289, 114), ("Claim", 427, 38) })
        {
            var action = _sidebar.GetNode<Control>("Margin/Content/" + name);
            action.Reparent(_settingsPanel, false);
            action.Position = new Vector2(16, y);
            foreach (var button in action is Button single ? new[] { single } : action.GetChildren().OfType<Button>())
                InGameUiSkin.StyleButton(button, "burgundy", fontSize: 16);
            // Reflow after shortening labels and applying the pixel font's minimum sizes.
            action.ResetSize();
            action.Size = new Vector2(280, height);
        }
        _settingsPanel.GetNode<Button>("TextFiles/Import").Text = "IMPORTAR";
        _settingsPanel.GetNode<Button>("TextFiles/Import").TooltipText = "Importar FEN / PGN";
        _settingsPanel.GetNode<Button>("TextFiles/Export").Text = "EXPORTAR";
        _settingsPanel.GetNode<Button>("TextFiles/Export").TooltipText = "Exportar FEN / PGN";
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
