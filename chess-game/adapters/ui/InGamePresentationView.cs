using Chess.Core.Model;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

/// <summary>
/// Surroundings of the untouched chess board. Layout follows the supplied
/// 960x540 JSON, scaling the existing 96px-tile BoardView as a single Node2D.
/// The actual board/tiles/pieces, move logic and input conversion are unchanged.
/// </summary>
public partial class InGamePresentationView : Control
{
    private const float ReferenceWidth = 960f;
    private const float ReferenceHeight = 540f;
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
        _stage = new Control { Name = "ArtworkStage", Size = new Vector2(960, 540), MouseFilter = MouseFilterEnum.Ignore };
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
            Size = new Vector2(960, 540),
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

        // Logo and the board frame are separate images. The middle of the frame is transparent.
        Art("GameLogo", "res://assets/ui/logos/logo_cheese.png", 18, 10, 192, 64);
        Art("BoardFrame", "02_board_ui/frame_board_422x422.png", 170, 75, 422, 422);
        for (int i = 0; i < 8; i++)
        {
            Art("RankBadge" + (8 - i), "02_board_ui/coordinate_badge_22x22.png",
                168, 94 + 48 * i + 13, 22, 22);
            Art("FileBadge" + i, "02_board_ui/coordinate_badge_22x22.png",
                189 + 48 * i + 13, 478, 22, 22);
        }
        Art("WhiteCard", "02_board_ui/player_card_white_128x66.png", 18, 239, 128, 66);
        Art("BlackCard", "02_board_ui/player_card_black_128x66.png", 18, 313, 128, 66);
        Art("WhiteBadge", "02_board_ui/player_cheese_badge_32x32.png", 26, 255, 32, 32);
        Art("BlackBadge", "02_board_ui/player_salami_badge_32x32.png", 26, 329, 32, 32);

        var white = InGameUiSkin.Text("WhiteName", "BLANCAS", new Vector2(65, 248),
            new Vector2(77, 25), 14, InGameUiSkin.Ink, UiFontWeight.SemiBold);
        var black = InGameUiSkin.Text("BlackName", "NEGRAS", new Vector2(65, 323),
            new Vector2(77, 25), 14, InGameUiSkin.Ivory, UiFontWeight.SemiBold);
        _stage.AddChild(white);
        _stage.AddChild(black);
        _whiteClock = InGameUiSkin.Text("WhiteClock", "--:--", new Vector2(77, 274),
            new Vector2(63, 18), 13, InGameUiSkin.Ink);
        _blackClock = InGameUiSkin.Text("BlackClock", "--:--", new Vector2(77, 348),
            new Vector2(63, 18), 13, InGameUiSkin.Ivory);
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
                Position = new Vector2(x, 13),
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

        var help = Utility("HelpButton", "help", 841, () => _helpPanel.Visible = !_helpPanel.Visible);
        help.TooltipText = "Ayuda";
        var settings = Utility("SettingsButton", "settings", 878, () => _settingsPanel.Visible = !_settingsPanel.Visible);
        settings.TooltipText = "Opciones de la partida";
        _sound = Utility("SoundButton", "sound_on", 915, ToggleSound);
        _sound.TooltipText = "Activar o silenciar sonido";

        _helpPanel = Popup("HelpPopup", new Vector2(625, 55), new Vector2(313, 80),
            "JUGADA: elegi una pieza y despues su destino.\nGUARDAR: conserva el estado de la partida.");
        _settingsPanel = Popup("SettingsPopup", new Vector2(645, 55), new Vector2(293, 80),
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
        Vector2 size = GetViewportRect().Size;
        if (size.X < 1 || size.Y < 1) return;
        Size = size;
        bool wide = size.X / size.Y >= 1.7f;
        _backdrop.Texture = InGameUiSkin.Texture(wide
            ? "01_background/ingame_picada_960x540.png"
            : "01_background/ingame_picada_1440x900.png");
        _backdrop.Position = Vector2.Zero;
        _backdrop.Size = size;

        float scale = Mathf.Min(size.X / ReferenceWidth, size.Y / ReferenceHeight);
        Vector2 offset = (size - new Vector2(ReferenceWidth, ReferenceHeight) * scale) * 0.5f;
        _stage.Position = offset;
        _stage.Scale = Vector2.One * scale;
        if (_foreground is not null && _utilityStage is not null)
        {
            _foreground.Size = size;
            _utilityStage.Position = offset;
            _utilityStage.Scale = Vector2.One * scale;
        }

        // BoardView still draws each 96x96 square exactly as before. Scaling its node
        // by scale/2 maps its 768px grid onto the 384px frame opening at 960x540.
        if (_board is not null)
        {
            _board.Position = offset + new Vector2(189, 94) * scale;
            _board.Scale = Vector2.One * (scale * 0.5f);
        }
        if (_sidebar is not null)
        {
            _sidebar.Position = offset + new Vector2(629, 58) * scale;
            _sidebar.Scale = Vector2.One * scale;
        }
    }
}