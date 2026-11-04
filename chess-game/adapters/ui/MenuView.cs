using Cheesse.Game.Adapters.Appearance;
using Godot;

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

    public override void _Ready()
    {
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        BuildBackground();
        _stage = new Control { Name = "Stage", Size = new Vector2(1440, 900) };
        AddChild(_stage);
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
                Page.Setup => Page.Modes,
                _ => Page.Home
            });
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildBackground()
    {
        var background = PixelUiSkin.Image("PixelBackground", "brand/menu_background.png", Vector2.Zero, Vector2.Zero);
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        background.StretchMode = TextureRect.StretchModeEnum.Scale;
        AddChild(background);
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
        _stage.AddChild(PixelUiSkin.Image("Logo", "brand/logo_cheesse.png", new Vector2(92, 42), new Vector2(384, 128), 2));
        _stage.AddChild(PixelUiSkin.Label("Tagline", "AJEDREZ. QUESOS. PICADA.",
            new Vector2(504, 100), new Vector2(700, 58), 30, pixel: true, light: true));
        _stage.AddChild(PixelUiSkin.Label("Version", "EDICION PIXEL ART  |  PROTOTIPO",
            new Vector2(1030, 48), new Vector2(328, 35), 17, light: true, alignment: HorizontalAlignment.Right));
    }

    private void BuildHome()
    {
        _home = MakePage("HomePage");
        var navigation = PixelUiSkin.MakePanel("Navigation", "parchment", new Vector2(65, 226), new Vector2(468, 608));
        _home.AddChild(navigation);
        navigation.AddChild(PixelUiSkin.Label("Heading", "TU PROXIMA PARTIDA", new Vector2(34, 42), new Vector2(400, 58), 31, pixel: true));
        navigation.AddChild(PixelUiSkin.Label("Subheading", "Arma una partida a tu medida.",
            new Vector2(38, 108), new Vector2(380, 52), 22));
        navigation.AddChild(PixelUiSkin.Image("Separator", "hud/separator_horizontal_160x8.png",
            new Vector2(38, 169), new Vector2(388, 15), 2));
        navigation.AddChild(PixelUiSkin.Button("NewGame", "NUEVA PARTIDA", new Vector2(39, 204),
            new Vector2(388, 68), () => ShowPage(Page.Modes), "play", 29));
        _continue = PixelUiSkin.Button("Continue", "CONTINUAR PARTIDA", new Vector2(39, 289),
            new Vector2(388, 66), () => ContinueRequested?.Invoke(), "load", 27);
        _continue.Disabled = true;
        navigation.AddChild(_continue);
        navigation.AddChild(PixelUiSkin.Button("Settings", "OPCIONES", new Vector2(39, 378),
            new Vector2(388, 62), () => ShowPage(Page.Settings), "settings", 26));
        navigation.AddChild(PixelUiSkin.Button("Exit", "SALIR", new Vector2(39, 454),
            new Vector2(388, 62), () => GetTree().Quit(), "close", 25));
        _message = PixelUiSkin.Label("Message", "", new Vector2(34, 538), new Vector2(400, 48), 18,
            alignment: HorizontalAlignment.Center);
        navigation.AddChild(_message);

        var showcase = PixelUiSkin.MakePanel("Showcase", "wood", new Vector2(558, 226), new Vector2(818, 608));
        _home.AddChild(showcase);
        showcase.AddChild(PixelUiSkin.Label("Heading", "CUATRO FORMAS DE JUGAR", new Vector2(38, 30),
            new Vector2(744, 58), 33, pixel: true, light: true));
        showcase.AddChild(PixelUiSkin.Label("Status", "Un clasico disponible. Nuevas reglas en desarrollo.",
            new Vector2(42, 87), new Vector2(740, 42), 20, light: true));
        var cards = new[]
        {
            ("CLASICO", "Ajedrez de siempre", "mode_classic_icon.png", "DISPONIBLE"),
            ("REY DE LA PICADA", "Conquista el centro", "mode_picada_king_icon.png", "PROXIMAMENTE"),
            ("QUESO SUIZO", "Casillas bloqueadas", "mode_swiss_cheese_icon.png", "PROXIMAMENTE"),
            ("FONDUE", "Capturas en cadena", "mode_fondue_icon.png", "PROXIMAMENTE"),
        };
        for (int i = 0; i < cards.Length; i++)
        {
            var x = 35 + (i % 2) * 381;
            var y = 151 + (i / 2) * 212;
            AddCompactCard(showcase, cards[i].Item1, cards[i].Item2, cards[i].Item3,
                cards[i].Item4, new Vector2(x, y), i == 0);
        }
    }

    private void AddCompactCard(Control parent, string title, string caption, string image, string status,
        Vector2 position, bool available)
    {
        var frame = PixelUiSkin.MakePanel(title.Replace(" ", "") + "Card", "dark", position, new Vector2(352, 194));
        parent.AddChild(frame);
        frame.AddChild(PixelUiSkin.Image("Illustration", "modes/" + image, new Vector2(18, 29),
            new Vector2(130, 130), 2));
        frame.AddChild(PixelUiSkin.Label("Title", title, new Vector2(154, 28), new Vector2(187, 65), 25,
            pixel: true, light: true));
        frame.AddChild(PixelUiSkin.Label("Description", caption, new Vector2(157, 91),
            new Vector2(175, 43), 17, light: true));
        frame.AddChild(PixelUiSkin.Label("Availability", status, new Vector2(155, 147),
            new Vector2(181, 28), 17, pixel: true, light: available));
    }

    private void BuildModes()
    {
        _modes = MakePage("ModesPage");
        _modes.AddChild(PixelUiSkin.Button("Back", "VOLVER", new Vector2(83, 184),
            new Vector2(205, 56), () => ShowPage(Page.Home), "back", 24, "small"));
        _modes.AddChild(PixelUiSkin.Label("Heading", "ELEGIR MODO DE JUEGO", new Vector2(325, 186),
            new Vector2(915, 59), 40, pixel: true, light: true));
        _modes.AddChild(PixelUiSkin.Label("Hint", "Elegi un modo para preparar la partida. Los especiales se habilitaran con sus reglas.",
            new Vector2(94, 252), new Vector2(1260, 44), 21, light: true, alignment: HorizontalAlignment.Center));
        var modes = new[]
        {
            ("CLASICO", "Las reglas del ajedrez original. Personaliza piezas y tablero.", "mode_classic_icon.png", true),
            ("REY DE LA PICADA", "Gana llegando con tu rey al centro o dando jaque mate.", "mode_picada_king_icon.png", false),
            ("QUESO SUIZO", "Los agujeros bloquean casillas y cambian las trayectorias.", "mode_swiss_cheese_icon.png", false),
            ("FONDUE", "Una captura derrite piezas vecinas. Cuidado con tu rey.", "mode_fondue_icon.png", false),
        };
        for (int i = 0; i < modes.Length; i++)
        {
            int x = 80 + i * 323;
            var card = PixelUiSkin.Button("Mode" + i, "", new Vector2(x, 320), new Vector2(292, 437),
                modes[i].Item4 ? () => ShowPage(Page.Setup) : null, fontSize: 20);
            var frame = PixelUiSkin.NineSlice(modes[i].Item4
                ? "cards/card_selected_96x112.png" : "cards/card_frame_96x112.png", 12);
            card.AddThemeStyleboxOverride("normal", frame);
            card.AddThemeStyleboxOverride("hover", PixelUiSkin.NineSlice("cards/card_selected_96x112.png", 12));
            card.AddThemeStyleboxOverride("pressed", PixelUiSkin.NineSlice("cards/card_selected_96x112.png", 12));
            card.AddThemeStyleboxOverride("disabled", frame);
            card.Disabled = !modes[i].Item4;
            _modes.AddChild(card);
            card.AddChild(PixelUiSkin.Image("Art", "modes/" + modes[i].Item3,
                new Vector2(66, 49), new Vector2(160, 160), 2));
            card.AddChild(PixelUiSkin.Label("ModeName", modes[i].Item1,
                new Vector2(15, 215), new Vector2(260, 58), 27, pixel: true,
                alignment: HorizontalAlignment.Center));
            card.AddChild(PixelUiSkin.Label("Description", modes[i].Item2,
                new Vector2(31, 284), new Vector2(228, 90), 17,
                alignment: HorizontalAlignment.Center));
            card.AddChild(PixelUiSkin.Label("State", modes[i].Item4 ? "JUGAR AHORA" : "PROXIMAMENTE",
                new Vector2(13, 385), new Vector2(267, 38), 22, pixel: true,
                alignment: HorizontalAlignment.Center));
        }
    }

    private void BuildSetup()
    {
        _setup = MakePage("SetupPage");
        _setup.AddChild(PixelUiSkin.Button("Back", "MODOS", new Vector2(84, 166),
            new Vector2(200, 56), () => ShowPage(Page.Modes), "back", 24, "small"));
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
            new Vector2(585, 32), 22, pixel: true, light: true));
        _appearance = MakeOption("Appearance", new Vector2(42, 118), new Vector2(592, 50));
        _appearance.AddItem("CLASICO");
        _appearance.AddItem("PICADA PERSONALIZADA");
        form.AddChild(_appearance);

        _themeFields = new Control { Name = "ThemeFields", Position = new Vector2(0, 174), Size = new Vector2(684, 252), MouseFilter = Control.MouseFilterEnum.Pass };
        form.AddChild(_themeFields);
        _themeFields.AddChild(PixelUiSkin.Label("BoardLabel", "TABLERO", new Vector2(42, 5),
            new Vector2(560, 25), 21, pixel: true, light: true));
        _boardSkin = MakeOption("Board", new Vector2(42, 31), new Vector2(592, 48));
        foreach (var skin in VisualThemeCatalog.Boards) _boardSkin.AddItem(Ascii(skin.Label));
        _themeFields.AddChild(_boardSkin);
        _themeFields.AddChild(PixelUiSkin.Label("WhiteLabel", "BLANCAS: ELEGIR QUESO", new Vector2(42, 86),
            new Vector2(560, 25), 21, pixel: true, light: true));
        _whiteSkin = MakeOption("White", new Vector2(42, 113), new Vector2(592, 48));
        _whiteSkin.AddItem("ALEATORIO");
        foreach (var skin in VisualThemeCatalog.Cheeses) _whiteSkin.AddItem(Ascii(skin.Label));
        _themeFields.AddChild(_whiteSkin);
        _themeFields.AddChild(PixelUiSkin.Label("BlackLabel", "NEGRAS: ELEGIR FIAMBRE", new Vector2(42, 169),
            new Vector2(560, 25), 21, pixel: true, light: true));
        _blackSkin = MakeOption("Black", new Vector2(42, 197), new Vector2(592, 48));
        _blackSkin.AddItem("ALEATORIO");
        foreach (var skin in VisualThemeCatalog.Charcuterie) _blackSkin.AddItem(Ascii(skin.Label));
        _themeFields.AddChild(_blackSkin);
        _note = PixelUiSkin.Label("Note", "EL MODO DE JUEGO MANTIENE LAS REGLAS CLASICAS.",
            new Vector2(42, 428), new Vector2(590, 44), 16, light: true);
        form.AddChild(_note);
        form.AddChild(PixelUiSkin.Button("Human", "CONTRA HUMANO", new Vector2(43, 488),
            new Vector2(590, 53), () => HumanRequested?.Invoke(), "play", 26));
        form.AddChild(PixelUiSkin.Button("Random", "CONTRA LA IA ALEATORIA", new Vector2(43, 549),
            new Vector2(590, 53), () => RandomRequested?.Invoke(), "dice", 26));

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
        var panel = PixelUiSkin.MakePanel("SettingsPanel", "parchment", new Vector2(304, 230), new Vector2(831, 590));
        _settings.AddChild(panel);
        panel.AddChild(PixelUiSkin.Label("Heading", "OPCIONES", new Vector2(51, 30),
            new Vector2(720, 60), 40, pixel: true));
        panel.AddChild(PixelUiSkin.Label("SoundTitle", "SONIDO", new Vector2(55, 120),
            new Vector2(700, 42), 27, pixel: true));
        _soundButton = PixelUiSkin.Button("Sound", "ACTIVAR / SILENCIAR", new Vector2(55, 170),
            new Vector2(720, 65), ToggleSound, "sound_on", 24);
        panel.AddChild(_soundButton);
        UpdateSoundButton();
        panel.AddChild(PixelUiSkin.Label("PixelTitle", "ESTILO DE PANTALLA", new Vector2(55, 277),
            new Vector2(720, 46), 27, pixel: true));
        panel.AddChild(PixelUiSkin.Label("PixelDetail", "PIXEL ART  |  ESCALADO NEAREST  |  1440 X 900",
            new Vector2(55, 329), new Vector2(720, 40), 21));
        panel.AddChild(PixelUiSkin.Label("ControlsTitle", "CONTROLES", new Vector2(55, 406),
            new Vector2(720, 42), 27, pixel: true));
        panel.AddChild(PixelUiSkin.Label("ControlsDetail", "CLICK: ELEGIR PIEZA Y DESTINO   /   CTRL+Z: DESHACER   /   ESC: VOLVER",
            new Vector2(55, 450), new Vector2(720, 76), 18));
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
        if (_message is not null) _message.Text = message;
    }

    public void FocusFirstOption() => ShowPage(Page.Home);
}