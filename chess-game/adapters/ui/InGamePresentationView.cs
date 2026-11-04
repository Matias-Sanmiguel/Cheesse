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

    public void Attach(ChessGame game)
    {
        _game = game;
        // Real match-time controls are not implemented yet: do not display a fake 10:00 clock.
        _whiteClock.Text = "--:--";
        _blackClock.Text = "--:--";
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= Refit;

    private void BuildArtwork()
    {
        void Art(string name, string file, float x, float y, float w, float h) =>
            _stage.AddChild(InGameUiSkin.Sprite(name, file, new Vector2(x, y), new Vector2(w, h)));

        // 422x422 artwork is displayed at EXACT 2x, so its 384px transparent
        // center becomes 768x768. The board pixels themselves are not resized.
        Art("GameLogo", "res://assets/ui/logos/logo_cheese.png", 8, 18, 192, 64);
        Art("BoardFrame", "02_board_ui/frame_board_422x422.png",
            FrameLeft, FrameTop, 422 * FrameScale, 422 * FrameScale);

        float boardX = FrameLeft + FrameInset;
        float boardY = FrameTop + FrameInset;

        // Separate pixel art coordinate badges around the native-size board.
        for (int i = 0; i < 8; i++)
        {
            Art("RankBadge" + (8 - i), "02_board_ui/coordinate_badge_22x22.png",
                boardX - 50, boardY + 96 * i + 26, 44, 44);
            Art("FileBadge" + i, "02_board_ui/coordinate_badge_22x22.png",
                boardX + 96 * i + 26, boardY + BoardSize + 1, 44, 36);
        }

        // Cards live in the available left column. They are not part of BoardView.
        Art("WhiteCard", "02_board_ui/player_card_white_128x66.png", 16, 343, 166, 86);
        Art("BlackCard", "02_board_ui/player_card_black_128x66.png", 16, 463, 166, 86);
        Art("WhiteBadge", "02_board_ui/player_cheese_badge_32x32.png", 24, 363, 40, 40);
        Art("BlackBadge", "02_board_ui/player_salami_badge_32x32.png", 24, 483, 40, 40);

        _stage.AddChild(InGameUiSkin.Text("WhiteName", "BLANCAS",
            new Vector2(67, 353), new Vector2(111, 30), 19,
            InGameUiSkin.Ink, UiFontWeight.SemiBold));
        _stage.AddChild(InGameUiSkin.Text("BlackName", "NEGRAS",
            new Vector2(67, 473), new Vector2(111, 30), 19,
            InGameUiSkin.Ivory, UiFontWeight.SemiBold));

        _whiteClock = InGameUiSkin.Text("WhiteClock", "--:--",
            new Vector2(69, 387), new Vector2(105, 27), 18, InGameUiSkin.Ink);
        _blackClock = InGameUiSkin.Text("BlackClock", "--:--",
            new Vector2(69, 507), new Vector2(105, 27), 18, InGameUiSkin.Ivory);
        _whiteClock.TooltipText = "Sin reloj de ajedrez configurado";
        _blackClock.TooltipText = "Sin reloj de ajedrez configurado";
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
        _backdrop.Position = Vector2.Zero;
        _backdrop.Size = viewport;

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
