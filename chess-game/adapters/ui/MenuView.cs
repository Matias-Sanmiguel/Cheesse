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

    private void OpenSetup(Page fromPage)
    {
        _setupReturnPage = fromPage;
        ShowPage(Page.Setup);
    }

    private void BuildModes()
    {
        _modes = MakePage("ModesPage");
        ModeSelectionPageBuilder.Build(
            _modes,
            () => OpenSetup(Page.Modes),
            () => ShowPage(Page.Home));
    }

    private void BuildSetup()
    {
        _setup = MakePage("SetupPage");
        _setup.AddChild(PixelUiSkin.Button("Back", "VOLVER", new Vector2(84, 166),
            new Vector2(200, 56), () => ShowPage(_setupReturnPage), "back", 24, "small"));
        _setup.AddChild(PixelUiSkin.Label("Heading", "PREPARA TU PARTIDA", new Vector2(333, 165),
            new Vector2(800, 60), 39, pixel: true, light: true));

        var preview = PixelUiSkin.MakePanel("Preview", "parchment", new Vector2(60, 232), new Vector2(613, 626));
        _setup.AddChild(preview);
        preview.AddChild(PixelUiSkin.Label("Heading", "VISTA PREVIA", new Vector2(29, 21),
            new Vector2(546, 55), 31, pixel: true));
        preview.AddChild(PixelUiSkin.Image("BoardFrame", "cards/board_preview_frame_96x96.png",
            new Vector2(23, 95), new Vector2(320, 320), 3));
        _tiles = new TextureRect[16];
        for (int i = 0; i < _tiles.Length; i++)
        {
            var x = i % 4;
            var y = i / 4;
            var backing = new ColorRect
            {
                Color = (x + y) % 2 == 0 ? new Color("e9e3d6") : new Color("769656"),
                Position = new Vector2(47 + x * 67, 121 + y * 67),
                Size = new Vector2(67, 67),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            preview.AddChild(backing);
            var tile = PixelUiSkin.Image("Tile" + i, "", backing.Position, backing.Size);
            preview.AddChild(tile);
            _tiles[i] = tile;
        }
        preview.AddChild(PixelUiSkin.Image("WhiteFrame", "cards/piece_preview_frame_64x64.png",
            new Vector2(354, 116), new Vector2(198, 198), 3));
        _whitePiece = PixelUiSkin.Image("WhitePiece", "", new Vector2(404, 168), new Vector2(96, 96), 2);
        preview.AddChild(_whitePiece);
        preview.AddChild(PixelUiSkin.Image("BlackFrame", "cards/piece_preview_frame_64x64.png",
            new Vector2(354, 314), new Vector2(198, 198), 3));
        _blackPiece = PixelUiSkin.Image("BlackPiece", "", new Vector2(404, 362), new Vector2(96, 96), 2);
        preview.AddChild(_blackPiece);
        _boardName = PixelUiSkin.Label("BoardName", "TABLERO CLASICO", new Vector2(25, 457),
            new Vector2(315, 48), 23, pixel: true);
        preview.AddChild(_boardName);
        _matchLabel = PixelUiSkin.Label("Teams", "PIEZAS CLASICAS", new Vector2(27, 515),
            new Vector2(530, 47), 26, pixel: true);
        preview.AddChild(_matchLabel);
        preview.AddChild(PixelUiSkin.Label("RuleTag", "REGLAS CLASICAS · PARTIDA NORMAL", new Vector2(28, 572),
            new Vector2(540, 31), 18));

        var form = PixelUiSkin.MakePanel("Options", "wood", new Vector2(696, 232), new Vector2(684, 626));
        _setup.AddChild(form);
        form.AddChild(PixelUiSkin.Label("Heading", "PERSONALIZACION", new Vector2(37, 25),
            new Vector2(580, 49), 32, pixel: true, light: true));
        form.AddChild(PixelUiSkin.Label("AppearanceLabel", "ESTILO VISUAL", new Vector2(41, 83),
            new Vector2(585, 32), 22, pixel: true, light: true, weight: UiFontWeight.Medium));
        _appearance = MakeOption("Appearance", new Vector2(42, 118), new Vector2(592, 50));
        _appearance.AddItem("CLASICO");
        _appearance.AddItem("PICADA PERSONALIZADA");
        form.AddChild(_appearance);

        _themeFields = new Control { Name = "ThemeFields", Position = new Vector2(0, 174), Size = new Vector2(684, 252), MouseFilter = Control.MouseFilterEnum.Pass };
        form.AddChild(_themeFields);
        _themeFields.AddChild(PixelUiSkin.Label("BoardLabel", "TABLERO", new Vector2(42, 5),
            new Vector2(560, 25), 21, pixel: true, light: true, weight: UiFontWeight.Medium));
        _boardSkin = MakeOption("Board", new Vector2(42, 31), new Vector2(592, 48));
        foreach (var skin in VisualThemeCatalog.Boards) _boardSkin.AddItem(Ascii(skin.Label));
        _themeFields.AddChild(_boardSkin);
        _themeFields.AddChild(PixelUiSkin.Label("WhiteLabel", "BLANCAS: ELEGIR QUESO", new Vector2(42, 86),
            new Vector2(560, 25), 21, pixel: true, light: true, weight: UiFontWeight.Medium));
        _whiteSkin = MakeOption("White", new Vector2(42, 113), new Vector2(592, 48));
        _whiteSkin.AddItem("ALEATORIO");
        foreach (var skin in VisualThemeCatalog.Cheeses) _whiteSkin.AddItem(Ascii(skin.Label));
        _themeFields.AddChild(_whiteSkin);
        _themeFields.AddChild(PixelUiSkin.Label("BlackLabel", "NEGRAS: ELEGIR FIAMBRE", new Vector2(42, 169),
            new Vector2(560, 25), 21, pixel: true, light: true, weight: UiFontWeight.Medium));
        _blackSkin = MakeOption("Black", new Vector2(42, 197), new Vector2(592, 48));
        _blackSkin.AddItem("ALEATORIO");
        foreach (var skin in VisualThemeCatalog.Charcuterie) _blackSkin.AddItem(Ascii(skin.Label));
        _themeFields.AddChild(_blackSkin);
        _note = PixelUiSkin.Label("Note", "EL MODO DE JUEGO MANTIENE LAS REGLAS CLASICAS.",
            new Vector2(42, 422), new Vector2(590, 28), 16, light: true);
        form.AddChild(_note);
        form.AddChild(PixelUiSkin.Button("Human", "CONTRA HUMANO", new Vector2(43, 454),
            new Vector2(590, 48), () => HumanRequested?.Invoke(), "play", 26));
        form.AddChild(PixelUiSkin.Button("Random", "CONTRA STOCKFISH", new Vector2(43, 509),
            new Vector2(590, 48), () => RandomRequested?.Invoke(), "dice", 26));
        _continue = PixelUiSkin.Button("Continue", "CONTINUAR PARTIDA", new Vector2(43, 564),
            new Vector2(590, 48), () => ContinueRequested?.Invoke(), "load", 26);
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

    private static OptionButton MakeOption(string name, Vector2 position, Vector2 size)
    {
        var option = new OptionButton { Name = name, Position = position, Size = size };
        PixelUiSkin.StyleOption(option);
        return option;
    }

    private void UpdatePreview()
    {
        bool themed = _appearance.Selected == 1;
        _themeFields.Visible = themed;
        _note.Text = themed
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
        _subpageLogo.Visible = page != Page.Home;
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