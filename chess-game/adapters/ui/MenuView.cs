using Cheesse.Game.Adapters.Appearance;
using Godot;
using Chess.Core.Model;

namespace Cheesse.Game.Adapters.Ui;

/// <summary>
/// Three-step pixel UI: home -> game mode -> match setup.
/// No chess rules live here; the mode previews remain locked until their rule sets exist.
/// </summary>
public partial class MenuView : Control
{
    public event Action? HumanRequested;
    public event Action? RandomRequested;
    public event Action? ContinueRequested;

    public static readonly TimeControl[] Controls = [TimeControl.Unlimited, new(180, 2), new(300, 3), new(600, 0), new(900, 10)];
    public static readonly int[] EngineElos = [1400, 1800, 2200, 0];
    public static readonly string[] EngineLabels = ["Inicial · 1400", "Intermedio · 1800", "Avanzado · 2200", "Máximo"];
    private OptionButton _time = null!, _difficulty = null!;
    public TimeControl SelectedTimeControl => Controls[Math.Clamp(_time.Selected, 0, Controls.Length - 1)];
    public int SelectedEngineElo => EngineElos[Math.Clamp(_difficulty.Selected, 0, EngineElos.Length - 1)];

    private enum Page { Home, Modes, Setup, Settings }
    private Page _current;
    private Control _stage = null!;
    private Control _home = null!;
    private Control _modes = null!;
    private Control _setup = null!;
    private Control _settings = null!;
    private Button _continue = null!;
    private Label _message = null!;
    private OptionButton _appearance = null!;
    private Button _classicStyleCard = null!;
    private Button _picadaStyleCard = null!;
    private Control _classicStyleDescription = null!;
    private OptionButton _boardSkin = null!;
    private OptionButton _whiteSkin = null!;
    private OptionButton _blackSkin = null!;
    private Control _themeFields = null!;
    private Label _matchLabel = null!;
    private Label _boardName = null!;
    private TextureRect _whitePiece = null!;
    private TextureRect _blackPiece = null!;
    private TextureRect[] _tiles = [];
    private Label _note = null!;
    private Button _soundButton = null!;
    private TextureRect _subpageLogo = null!;
    private Page _setupReturnPage = Page.Home;

    public GameMode SelectedMode { get; private set; } = GameMode.Classic;

    public override void _Ready()
    {
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        _stage = new Control { Name = "Stage", Size = new Vector2(1440, 900) };
        AddChild(_stage);
        BuildBackground();
        BuildHeader();
        BuildHome();
        BuildModes();
        BuildSetup();
        BuildSettings();
        Resized += FitStage;
        FitStage();
        ShowPage(Page.Home);
    }

    public override void _ExitTree()
    {
        Resized -= FitStage;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && _current != Page.Home)
        {
            ShowPage(_current switch
            {
                Page.Setup => _setupReturnPage,
                _ => Page.Home
            });
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildBackground()
    {
        // Native 360x225 pixel art background, rendered at exact x4 in 1440x900.
        // It belongs to Stage so all menu pages scale together without distortion.
        var background = PixelUiSkin.Image("PixelBackground",
            "res://assets/backgrounds/menu_background.png", Vector2.Zero,
            new Vector2(1440, 900), 4);
        _stage.AddChild(background);
    }

    private void FitStage()
    {
        if (_stage is null) return;
        var viewport = Size;
        if (viewport.X <= 1 || viewport.Y <= 1) viewport = GetViewportRect().Size;
        float scale = Mathf.Min(viewport.X / 1440f, viewport.Y / 900f);
        _stage.Scale = new Vector2(scale, scale);
        _stage.Position = (viewport - new Vector2(1440, 900) * scale) / 2f;
    }

    private void BuildHeader()
    {
        // Secondary pages use the compact new logo; the home page gets the full-size one.
        _subpageLogo = PixelUiSkin.Image("CompactLogo", "logos/logo_cheese_compact.png",
            new Vector2(528, 75), new Vector2(384, 96), 3);
        _stage.AddChild(_subpageLogo);
    }

    private void BuildHome()
    {
        _home = MakePage("HomePage");

        // Full-size 192x64 native logo at x4; do not trim or interpolate it.
        _home.AddChild(PixelUiSkin.Image("HomeLogo", "logos/logo_cheese.png",
            new Vector2(336, 56), new Vector2(768, 256), 4));

        // Keep only the four primary actions from the supplied visual reference.
        // The existing save continues to be available from the setup screen.
        var navigation = new Control
        {
            Name = "Navigation",
            Position = Vector2.Zero,
            Size = new Vector2(1440, 900),
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        _home.AddChild(navigation);

        const float left = 430f;
        const float width = 580f;
        const float height = 82f;
        navigation.AddChild(PixelUiSkin.Button("NewGame", "JUGAR",
            new Vector2(left, 372), new Vector2(width, height),
            () => OpenSetup(Page.Home), fontSize: 44));
        navigation.AddChild(PixelUiSkin.Button("Modes", "MODOS",
            new Vector2(left, 476), new Vector2(width, height),
            () => ShowPage(Page.Modes), fontSize: 44));
        navigation.AddChild(PixelUiSkin.Button("Settings", "CONFIGURACION",
            new Vector2(left, 580), new Vector2(width, height),
            () => ShowPage(Page.Settings), fontSize: 43));
        navigation.AddChild(PixelUiSkin.Button("Exit", "SALIR",
            new Vector2(left, 684), new Vector2(width, height),
            () => GetTree().Quit(), fontSize: 44));

        // Empty in the normal state; used only for recoverable loading errors.
        _message = PixelUiSkin.Label("Message", "",
            new Vector2(300, 796), new Vector2(840, 56), 24,
            pixel: true, light: true, alignment: HorizontalAlignment.Center, weight: UiFontWeight.Regular);
        navigation.AddChild(_message);
    }

    private void OpenSetup(Page fromPage, GameMode mode = GameMode.Classic)
    {
        _setupReturnPage = fromPage;
        SelectedMode = mode;
        // Picada has a themed default; users may still customize teams and board.
        bool picada = mode == GameMode.ReyDeLaPicada;
        _appearance.Select(picada ? 1 : 0);
        _boardSkin.Select(0);
        _whiteSkin.Select(picada ? 1 : 0);
        _blackSkin.Select(picada ? 1 : 0);
        _setup.GetNode<Label>("Heading").Text = "PREPARA TU PARTIDA";
        _setup.GetNode<Label>("Preview/RuleTag").Text = picada
            ? "OBJETIVO: REY EN D4 / E4 / D5 / E5"
            : "REGLAS CLASICAS · PARTIDA NORMAL";
        UpdatePreview();
        ShowPage(Page.Setup);
    }

    private void BuildModes()
    {
        _modes = MakePage("ModesPage");
        ModeSelectionPageBuilder.Build(
            _modes,
            () => OpenSetup(Page.Modes),
            () => OpenSetup(Page.Modes, GameMode.ReyDeLaPicada),
            () => ShowPage(Page.Home));
    }

    private void BuildSetup()
    {
        _setup = MakePage("SetupPage");

        // Same 1440x900 stage and original art, reorganized like the supplied
        // two-column reference. No logo on this screen: use the freed header space.
        _setup.AddChild(PixelUiSkin.Button("Back", "VOLVER",
            new Vector2(82, 89), new Vector2(200, 56),
            () => ShowPage(_setupReturnPage), "back", 24, "small"));
        _setup.AddChild(PixelUiSkin.Label("Heading", "PREPARA TU PARTIDA",
            new Vector2(319, 79), new Vector2(802, 77), 43,
            pixel: true, light: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.Bold));

        // LEFT: live board and piece preview, followed by the three descriptive lines.
        var preview = PixelUiSkin.MakePanel("Preview", "parchment",
            new Vector2(60, 170), new Vector2(630, 680));
        _setup.AddChild(preview);
        preview.AddChild(PixelUiSkin.Label("Heading", "VISTA PREVIA",
            new Vector2(41, 22), new Vector2(546, 56), 34,
            pixel: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.Bold));

        preview.AddChild(PixelUiSkin.Image("BoardFrame",
            "cards/board_preview_frame_96x96.png",
            new Vector2(55, 108), new Vector2(320, 320), 3));

        _tiles = new TextureRect[16];
        for (int i = 0; i < _tiles.Length; i++)
        {
            int x = i % 4, y = i / 4;
            var backing = new ColorRect
            {
                Name = "Square" + i,
                Color = (x + y) % 2 == 0 ? new Color("e9e3d6") : new Color("769656"),
                Position = new Vector2(79 + x * 67, 134 + y * 67),
                Size = new Vector2(67, 67),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            preview.AddChild(backing);
            var tile = PixelUiSkin.Image("Tile" + i, "", backing.Position, backing.Size);
            preview.AddChild(tile);
            _tiles[i] = tile;
        }

        // Keep the supplied 64x64 piece frames at their clean x2 scale.
        preview.AddChild(PixelUiSkin.Image("WhiteFrame",
            "cards/piece_preview_frame_64x64.png",
            new Vector2(423, 123), new Vector2(128, 128), 2));
        _whitePiece = PixelUiSkin.Image("WhitePiece", "",
            new Vector2(439, 139), new Vector2(96, 96), 2);
        preview.AddChild(_whitePiece);

        preview.AddChild(PixelUiSkin.Image("BlackFrame",
            "cards/piece_preview_frame_64x64.png",
            new Vector2(423, 289), new Vector2(128, 128), 2));
        _blackPiece = PixelUiSkin.Image("BlackPiece", "",
            new Vector2(439, 305), new Vector2(96, 96), 2);
        preview.AddChild(_blackPiece);

        preview.AddChild(new ColorRect
        {
            Name = "DetailsSeparator",
            Position = new Vector2(40, 468),
            Size = new Vector2(550, 4),
            Color = new Color("b8874d"),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        preview.AddChild(new ColorRect
        {
            Name = "SeparatorGem",
            Position = new Vector2(310, 464),
            Size = new Vector2(12, 12),
            Color = new Color("e3a845"),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _boardName = PixelUiSkin.Label("BoardName", "TABLERO CLASICO",
            new Vector2(42, 492), new Vector2(550, 40), 24,
            pixel: true, weight: UiFontWeight.SemiBold);
        preview.AddChild(_boardName);
        _matchLabel = PixelUiSkin.Label("Teams", "BLANCAS VS NEGRAS",
            new Vector2(42, 540), new Vector2(550, 40), 24,
            pixel: true, weight: UiFontWeight.SemiBold);
        preview.AddChild(_matchLabel);
        preview.AddChild(PixelUiSkin.Label("RuleTag",
            "REGLAS CLASICAS - PARTIDA NORMAL",
            new Vector2(42, 591), new Vector2(554, 45), 20,
            pixel: true, weight: UiFontWeight.Medium));

        // RIGHT: visual style choice above the familiar themed dropdowns and CTAs.
        var form = PixelUiSkin.MakePanel("Options", "wood",
            new Vector2(715, 170), new Vector2(665, 680));
        _setup.AddChild(form);
        form.AddChild(PixelUiSkin.Label("Heading", "PERSONALIZACION",
            new Vector2(40, 19), new Vector2(585, 58), 35,
            pixel: true, light: true,
            alignment: HorizontalAlignment.Center, weight: UiFontWeight.Bold));
        form.AddChild(new ColorRect
        {
            Name = "HeaderDivider",
            Color = new Color("a8693c"),
            Position = new Vector2(43, 79),
            Size = new Vector2(580, 3),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        form.AddChild(PixelUiSkin.Label("AppearanceLabel", "ESTILO VISUAL",
            new Vector2(43, 88), new Vector2(570, 33), 23,
            pixel: true, light: true, weight: UiFontWeight.Medium));

        // Preserve the original OptionButton and its selected index as the
        // source of truth for themes, saves, and the existing tests.
        _appearance = MakeOption("Appearance", Vector2.Zero, new Vector2(1, 1));
        _appearance.AddItem("CLASICO");
        _appearance.AddItem("PICADA PERSONALIZADA");
        _appearance.Visible = false;
        form.AddChild(_appearance);

        _classicStyleCard = MakeStyleCard("ClassicStyle", "CLASICO",
            "modes/mode_classic_icon.png",
            new Vector2(43, 127), new Vector2(283, 87),
            () => { _appearance.Select(0); UpdatePreview(); });
        form.AddChild(_classicStyleCard);
        _picadaStyleCard = MakeStyleCard("PicadaStyle", "PICADA\nPERSONALIZADA",
            "modes/mode_picada_king_icon.png",
            new Vector2(340, 127), new Vector2(283, 87),
            () => { _appearance.Select(1); UpdatePreview(); });
        form.AddChild(_picadaStyleCard);

        _themeFields = new Control
        {
            Name = "ThemeFields",
            Position = new Vector2(0, 232),
            Size = new Vector2(665, 223),
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        form.AddChild(_themeFields);

        _themeFields.AddChild(PixelUiSkin.Label("BoardLabel", "TABLERO",
            new Vector2(43, 0), new Vector2(210, 47), 22,
            pixel: true, light: true, weight: UiFontWeight.Medium));
        _boardSkin = MakeOption("Board", new Vector2(266, 0), new Vector2(351, 47));
        foreach (var skin in VisualThemeCatalog.Boards) _boardSkin.AddItem(Ascii(skin.Label));
        _themeFields.AddChild(_boardSkin);

        _themeFields.AddChild(PixelUiSkin.Label("WhiteLabel", "BLANCAS - QUESO",
            new Vector2(43, 76), new Vector2(218, 47), 22,
            pixel: true, light: true, weight: UiFontWeight.Medium));
        _whiteSkin = MakeOption("White", new Vector2(266, 76), new Vector2(351, 47));
        _whiteSkin.AddItem("ALEATORIO");
        foreach (var skin in VisualThemeCatalog.Cheeses) _whiteSkin.AddItem(Ascii(skin.Label));
        _themeFields.AddChild(_whiteSkin);

        _themeFields.AddChild(PixelUiSkin.Label("BlackLabel", "NEGRAS - FIAMBRE",
            new Vector2(43, 152), new Vector2(218, 47), 22,
            pixel: true, light: true, weight: UiFontWeight.Medium));
        _blackSkin = MakeOption("Black", new Vector2(266, 152), new Vector2(351, 47));
        _blackSkin.AddItem("ALEATORIO");
        foreach (var skin in VisualThemeCatalog.Charcuterie) _blackSkin.AddItem(Ascii(skin.Label));
        _themeFields.AddChild(_blackSkin);

        // A helpful explanation fills the same area when classic style is used.
        _classicStyleDescription = PixelUiSkin.MakePanel("ClassicInfo", "parchment",
            new Vector2(43, 247), new Vector2(576, 190));
        form.AddChild(_classicStyleDescription);
        _classicStyleDescription.AddChild(PixelUiSkin.Label("Title", "AJEDREZ CLASICO",
            new Vector2(24, 24), new Vector2(525, 52), 29,
            pixel: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.Bold));
        _classicStyleDescription.AddChild(PixelUiSkin.Label("Detail",
            "TABLERO ORIGINAL\nPIEZAS TRADICIONALES\nSIN CAMBIOS VISUALES",
            new Vector2(29, 84), new Vector2(517, 80), 22,
            pixel: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.Medium));

        _note = PixelUiSkin.Label("Note",
            "LAS SKINS NO CAMBIAN LAS REGLAS.",
            new Vector2(44, 424), new Vector2(572, 44), 18,
            light: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.Medium);
        form.AddChild(_note);

        // Keep the same button textures and original 16px icon art. Native
        // Button.Icon laid the icons beyond the 48px-high button artwork;
        // explicit 32x32 child images align them within the button at x2.
        form.AddChild(MakeSetupActionButton("Human", "CONTRA HUMANO", "play",
            new Vector2(43, 474), () => HumanRequested?.Invoke()));
        form.AddChild(MakeSetupActionButton("Random", "CONTRA STOCKFISH", "dice",
            new Vector2(43, 530), () => RandomRequested?.Invoke()));
        _continue = MakeSetupActionButton("Continue", "CONTINUAR PARTIDA", "load",
            new Vector2(43, 586), () => ContinueRequested?.Invoke());
        _continue.Disabled = true;
        form.AddChild(_continue);

        _appearance.Select(0);
        _boardSkin.Select(0);
        _whiteSkin.Select(0);
        _blackSkin.Select(0);
        _appearance.ItemSelected += _ => UpdatePreview();
        _boardSkin.ItemSelected += _ => UpdatePreview();
        _whiteSkin.ItemSelected += _ => UpdatePreview();
        _blackSkin.ItemSelected += _ => UpdatePreview();
        UpdatePreview();
    }

    private static Button MakeSetupActionButton(string name, string label,
        string icon, Vector2 position, Action onPressed)
    {
        // Keep Godot's standard button text centered and handle the decorative
        // icon separately so its full native x2 sprite stays inside the frame.
        var button = PixelUiSkin.Button(name, label, position,
            new Vector2(575, 48), onPressed, fontSize: 26);
        button.AddChild(PixelUiSkin.Image("ActionIcon",
            "icons/" + icon + "_16x16.png",
            new Vector2(18, 8), new Vector2(32, 32), 2));
        return button;
    }

    private static Button MakeStyleCard(string name, string label, string illustration,
        Vector2 position, Vector2 size, Action onSelected)
    {
        var button = new Button
        {
            Name = name,
            Text = "",
            Position = position,
            Size = size,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            FocusMode = Control.FocusModeEnum.All,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        PixelUiSkin.StyleButton(button, 22, "medium");
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddChild(PixelUiSkin.Image("Art", illustration,
            new Vector2(14, 11), new Vector2(65, 65), 1));
        button.AddChild(PixelUiSkin.Label("Caption", label,
            new Vector2(87, 6), new Vector2(size.X - 100, size.Y - 12), 21,
            pixel: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.SemiBold));
        button.Pressed += onSelected;
        return button;
    }

    private static void SetStyleCardSelected(Button card, bool selected)
    {
        // Gold = active; parchment = inactive. Both skins already exist in the game.
        card.AddThemeStyleboxOverride("normal", PixelUiSkin.NineSlice(
            selected ? "buttons/medium_120x24/idle.png" : "panels/parchment_9slice_32x32.png"));
        card.AddThemeStyleboxOverride("hover", PixelUiSkin.NineSlice(
            "buttons/medium_120x24/hover.png"));
        card.AddThemeStyleboxOverride("pressed", PixelUiSkin.NineSlice(
            "buttons/medium_120x24/pressed.png"));
    }

    private static OptionButton MakeOption(string name, Vector2 position, Vector2 size)
    {
        var option = new OptionButton { Name = name, Position = position, Size = size };
        PixelUiSkin.StyleOption(option);
        return option;
    }

    private void UpdatePreview()
    {
        bool themed = _appearance.Selected == 1;
        var engineButton = _setup.GetNode<Button>("Options/Random");
        engineButton.Disabled = SelectedMode != GameMode.Standard;
        engineButton.TooltipText = engineButton.Disabled ? "Stockfish solo admite ajedrez clasico." : "Jugar contra Stockfish";
        _themeFields.Visible = themed;
        _classicStyleDescription.Visible = !themed;
        SetStyleCardSelected(_classicStyleCard, !themed);
        SetStyleCardSelected(_picadaStyleCard, themed);
        _note.Text = SelectedMode == GameMode.ReyDeLaPicada
            ? "REY DE LA PICADA: LLEVA TU REY A D4, E4, D5 O E5."
            : themed
                ? "EL QUESO Y EL FIAMBRE CAMBIAN EL ASPECTO, NO LAS REGLAS."
                : "REGLAS CLASICAS. PIEZAS Y TABLERO ORIGINALES.";
        string? boardRoot = themed ?
            "res://assets/skins/boards/" + VisualThemeCatalog.Boards[Math.Clamp(_boardSkin.Selected, 0, VisualThemeCatalog.Boards.Length - 1)].Folder : null;
        for (int i = 0; i < _tiles.Length; i++)
        {
            string side = (i % 4 + i / 4) % 2 == 0 ? "light_tile.png" : "dark_tile.png";
            _tiles[i].Texture = boardRoot is null ? null : PixelUiSkin.Texture(boardRoot + "/" + side);
        }
        if (!themed)
        {
            _boardName.Text = "TABLERO CLASICO";
            _matchLabel.Text = "BLANCAS VS NEGRAS";
            _whitePiece.Texture = PixelUiSkin.Texture("res://assets/pieces/white/king.png", 2);
            _blackPiece.Texture = PixelUiSkin.Texture("res://assets/pieces/black/king.png", 2);
            return;
        }
        _boardName.Text = Ascii(VisualThemeCatalog.Boards[_boardSkin.Selected].Label);
        string cheese = _whiteSkin.Selected == 0 ? "ALEATORIO" : Ascii(VisualThemeCatalog.Cheeses[_whiteSkin.Selected - 1].Label);
        string meat = _blackSkin.Selected == 0 ? "ALEATORIO" : Ascii(VisualThemeCatalog.Charcuterie[_blackSkin.Selected - 1].Label);
        _matchLabel.Text = cheese + " VS " + meat;
        _whitePiece.Texture = _whiteSkin.Selected == 0
            ? PixelUiSkin.Texture("cards/random_skin_32x32.png", 3)
            : PixelUiSkin.Texture("res://assets/skins/pieces/" + VisualThemeCatalog.Cheeses[_whiteSkin.Selected - 1].Folder + "/White/king.png", 2);
        _blackPiece.Texture = _blackSkin.Selected == 0
            ? PixelUiSkin.Texture("cards/random_skin_32x32.png", 3)
            : PixelUiSkin.Texture("res://assets/skins/pieces/" + VisualThemeCatalog.Charcuterie[_blackSkin.Selected - 1].Folder + "/Black/king.png", 2);
    }

    private void BuildSettings()
    {
        _settings = MakePage("SettingsPage");
        _settings.AddChild(PixelUiSkin.Button("Back", "VOLVER", new Vector2(83, 184),
            new Vector2(205, 55), () => ShowPage(Page.Home), "back", 24, "small"));
        var panel = PixelUiSkin.MakePanel("SettingsPanel", "parchment", new Vector2(304, 230), new Vector2(831, 640));
        _settings.AddChild(panel);
        panel.AddChild(PixelUiSkin.Label("Heading", "OPCIONES", new Vector2(51, 30),
            new Vector2(720, 60), 40, pixel: true));
        panel.AddChild(PixelUiSkin.Label("SoundTitle", "SONIDO", new Vector2(55, 120),
            new Vector2(700, 42), 27, pixel: true));
        _soundButton = PixelUiSkin.Button("Sound", "ACTIVAR / SILENCIAR", new Vector2(55, 170),
            new Vector2(720, 65), ToggleSound, "sound_on", 24);
        panel.AddChild(_soundButton);
        UpdateSoundButton();
        panel.AddChild(PixelUiSkin.Label("TimeLabel", "RELOJ PARA NUEVAS PARTIDAS",
            new Vector2(55, 257), new Vector2(720, 30), 22, pixel: true));
        _time = MakeOption("TimeControl", new Vector2(55, 291), new Vector2(720, 44));
        foreach (var label in new[] { "Sin reloj", "3 min + 2 s", "5 min + 3 s", "10 min", "15 min + 10 s" }) _time.AddItem(label);
        panel.AddChild(_time);
        panel.AddChild(PixelUiSkin.Label("DifficultyLabel", "NIVEL DE STOCKFISH",
            new Vector2(55, 345), new Vector2(720, 30), 22, pixel: true));
        _difficulty = MakeOption("Difficulty", new Vector2(55, 379), new Vector2(720, 44));
        foreach (var label in EngineLabels) _difficulty.AddItem(label);
        _difficulty.Select(1);
        panel.AddChild(_difficulty);
        panel.AddChild(PixelUiSkin.Label("PixelTitle", "ESTILO DE PANTALLA", new Vector2(55, 435),
            new Vector2(720, 46), 27, pixel: true));
        panel.AddChild(PixelUiSkin.Label("PixelDetail", "PIXEL ART  |  ESCALADO NEAREST  |  1440 X 900",
            new Vector2(55, 477), new Vector2(720, 40), 21));
        panel.AddChild(PixelUiSkin.Label("ControlsTitle", "CONTROLES", new Vector2(55, 530),
            new Vector2(720, 42), 27, pixel: true));
        panel.AddChild(PixelUiSkin.Label("ControlsDetail", "CLICK: ELEGIR PIEZA Y DESTINO   /   CTRL+Z: DESHACER   /   ESC: VOLVER",
            new Vector2(55, 570), new Vector2(720, 40), 18));
    }

    private void ToggleSound()
    {
        var bus = AudioServer.GetBusIndex("Master");
        if (bus < 0) return;
        AudioServer.SetBusMute(bus, !AudioServer.IsBusMute(bus));
        UpdateSoundButton();
    }

    private void UpdateSoundButton()
    {
        if (_soundButton is null) return;
        var bus = AudioServer.GetBusIndex("Master");
        bool muted = bus >= 0 && AudioServer.IsBusMute(bus);
        _soundButton.Text = muted ? "SONIDO: APAGADO" : "SONIDO: ENCENDIDO";
        _soundButton.Icon = PixelUiSkin.Texture(muted ? "icons/sound_off_16x16.png" : "icons/sound_on_16x16.png", 2);
    }

    private Control MakePage(string name)
    {
        var page = new Control { Name = name, Position = Vector2.Zero, Size = new Vector2(1440, 900), Visible = false };
        _stage.AddChild(page);
        return page;
    }

    private void ShowPage(Page page)
    {
        _current = page;
        _home.Visible = page == Page.Home;
        _modes.Visible = page == Page.Modes;
        _setup.Visible = page == Page.Setup;
        _settings.Visible = page == Page.Settings;
        // Modes have their own compact logo; setup intentionally has no logo.
        _subpageLogo.Visible = page == Page.Settings;
        GetViewport().GuiReleaseFocus();
    }

    private static string Ascii(string text) => text.ToUpperInvariant()
        .Replace('Á', 'A').Replace('É', 'E').Replace('Í', 'I')
        .Replace('Ó', 'O').Replace('Ú', 'U').Replace('Ñ', 'N');

    /// <summary>
    /// Creates a resolved theme only when a new game starts; random team choices are stable afterward.
    /// </summary>
    public VisualTheme CreateVisualTheme() => _appearance.Selected == 1
        ? VisualThemeCatalog.Create(_boardSkin.Selected, _whiteSkin.Selected, _blackSkin.Selected)
        : VisualTheme.Classic;

    public void SetContinueAvailable(bool available)
    {
        if (_continue is null) return;
        _continue.Disabled = !available;
        _continue.TooltipText = available ? "Continuar la ultima partida guardada" : "No hay ninguna partida guardada";
    }

    public void SetMessage(string message)
    {
        if (_message is null) return;
        _message.Text = message;
        // Errors from Continue must be visible instead of hiding behind the setup page.
        if (!string.IsNullOrEmpty(message)) ShowPage(Page.Home);
    }

    public void FocusFirstOption() => ShowPage(Page.Home);
}