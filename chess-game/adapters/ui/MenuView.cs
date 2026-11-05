using Cheesse.Game.Adapters.Appearance;
using Godot;
using Chess.Core.Model;

namespace Cheesse.Game.Adapters.Ui;

/// <summary>
/// Four-step pixel UI: home -> game mode -> rules -> match setup.
/// Rules are displayed as readable UI copy, never implemented in the menu.
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

    private enum Page { Home, Modes, Rules, Setup, Settings }
    private Page _current;
    private Control _stage = null!;
    private Control _home = null!;
    private Control _modes = null!;
    private Control _rules = null!;
    private Control _setup = null!;
    private Control _settings = null!;
    private Button _continue = null!;
    private Label _message = null!;
    private OptionButton _appearance = null!;
    private Button _classicStyleCard = null!;
    private Button _picadaStyleCard = null!;
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
    private const string SetupAssets = "res://assets/ui/setup_v4/";

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
        BuildRules();
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
                Page.Rules => Page.Modes,
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
            new Vector2(500, 75), new Vector2(441, 96), 3);
        _stage.AddChild(_subpageLogo);
    }

    private void BuildHome()
    {
        _home = MakePage("HomePage");

        // Full-size 219x64 CHEESSE logo at x4; centered without stretching.
        _home.AddChild(PixelUiSkin.Image("HomeLogo", "logos/logo_cheese.png",
            new Vector2(282, 56), new Vector2(876, 256), 4));

        // Single entry point for a new game: Home -> Modes -> Rules -> Setup.
        // Saves are still resumed from the setup screen.
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
            new Vector2(left, 384), new Vector2(width, height),
            () => ShowPage(Page.Modes), fontSize: 44));
        navigation.AddChild(PixelUiSkin.Button("Settings", "CONFIGURACION",
            new Vector2(left, 504), new Vector2(width, height),
            () => ShowPage(Page.Settings), fontSize: 43));
        navigation.AddChild(PixelUiSkin.Button("Exit", "SALIR",
            new Vector2(left, 624), new Vector2(width, height),
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
        // Suggested presets are optional: each mode still lets players choose
        // the classic appearance or customize the board and both teams.
        bool picada = mode == GameMode.ReyDeLaPicada;
        bool fondue = mode == GameMode.Fondue;
        _appearance.Select(picada || fondue ? 1 : 0);
        _boardSkin.Select(fondue ? 3 : 0); // Mármol Bistro
        _whiteSkin.Select(fondue ? 2 : picada ? 1 : 0); // Cheddar
        _blackSkin.Select(fondue ? 2 : picada ? 1 : 0); // Chorizo
        _setup.GetNode<Label>("Heading").Text = "PREPARA TU PARTIDA";
        _setup.GetNode<Label>("Preview/RuleTag").Text = mode switch
        {
            GameMode.ReyDeLaPicada => "OBJETIVO: REY EN D4 / E4 / D5 / E5",
            GameMode.QuesoSuizo => "AGUJEROS: BLOQUEAN CASILLAS Y CAMINOS",
            GameMode.Fondue => "FONDUE: OTRAS PIEZAS DERRITEN AREA 3X3",
            _ => "REGLAS CLASICAS · PARTIDA NORMAL"
        };
        UpdatePreview();
        ShowPage(Page.Setup);
    }

    private void OpenRules(GameMode mode)
    {
        SelectedMode = mode;
        ModeRulesPageBuilder.Configure(_rules, mode);
        ShowPage(Page.Rules);
    }

    private void BuildModes()
    {
        _modes = MakePage("ModesPage");
        ModeSelectionPageBuilder.Build(
            _modes,
            () => OpenRules(GameMode.Classic),
            () => OpenRules(GameMode.ReyDeLaPicada),
            () => OpenRules(GameMode.QuesoSuizo),
            () => OpenRules(GameMode.Fondue),
            () => ShowPage(Page.Home));
    }

    private void BuildRules()
    {
        _rules = MakePage("RulesPage");
        ModeRulesPageBuilder.Build(_rules,
            () => ShowPage(Page.Modes),
            () => OpenSetup(Page.Rules, SelectedMode));
        ModeRulesPageBuilder.Configure(_rules, GameMode.Classic);
    }

    private void BuildSetup()
    {
        _setup = MakePage("SetupPage");

        // Import just the V4 panels, preview frames and style cards; all tiles,
        // chess pieces, dropdowns and gameplay buttons still use existing assets.
        _setup.AddChild(PixelUiSkin.Button("Back", "VOLVER",
            new Vector2(82, 89), new Vector2(200, 56),
            () => ShowPage(_setupReturnPage), "back", 24, "small"));
        _setup.AddChild(PixelUiSkin.Label("Heading", "PREPARA TU PARTIDA",
            new Vector2(319, 79), new Vector2(802, 77), 43,
            pixel: true, light: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.Bold));

        // V4 parchment panel: the original board and piece previews remain live.
        var preview = new Control
        {
            Name = "Preview",
            Position = new Vector2(62, 182),
            Size = new Vector2(604, 660),
            MouseFilter = Control.MouseFilterEnum.Pass,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        _setup.AddChild(preview);
        preview.AddChild(PixelUiSkin.Image("PanelArt",
            SetupAssets + "panels/panel_vista_previa_151x165.png",
            Vector2.Zero, new Vector2(604, 660), 4));
        preview.AddChild(PixelUiSkin.Label("Heading", "VISTA PREVIA",
            new Vector2(35, 19), new Vector2(534, 56), 32,
            pixel: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.Bold));

        // The V4 82x82 frame has a precise 64x64 content aperture at x4.
        preview.AddChild(PixelUiSkin.Image("BoardFrame",
            SetupAssets + "preview/marco_tablero_82x82.png",
            new Vector2(40, 103), new Vector2(328, 328), 4));
        _tiles = new TextureRect[16];
        for (int i = 0; i < _tiles.Length; i++)
        {
            int x = i % 4, y = i / 4;
            var backing = new ColorRect
            {
                Name = "Square" + i,
                // Use the actual classic board palette, not the previous
                // green placeholder. Keeping the same source prevents drift.
                Color = (x + y) % 2 == 0
                    ? BoardView.LightSquare : BoardView.DarkSquare,
                Position = new Vector2(76 + x * 64, 139 + y * 64),
                Size = new Vector2(64, 64),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            preview.AddChild(backing);
            var tile = PixelUiSkin.Image("Tile" + i, "",
                backing.Position, backing.Size);
            preview.AddChild(tile);
            _tiles[i] = tile;
        }

        preview.AddChild(PixelUiSkin.Image("WhiteFrame",
            SetupAssets + "preview/slot_pieza_40x39.png",
            new Vector2(388, 103), new Vector2(160, 156), 4));
        _whitePiece = PixelUiSkin.Image("WhitePiece", "",
            new Vector2(420, 133), new Vector2(96, 96), 2);
        preview.AddChild(_whitePiece);
        preview.AddChild(PixelUiSkin.Image("BlackFrame",
            SetupAssets + "preview/slot_pieza_40x39.png",
            new Vector2(388, 275), new Vector2(160, 156), 4));
        _blackPiece = PixelUiSkin.Image("BlackPiece", "",
            new Vector2(420, 305), new Vector2(96, 96), 2);
        preview.AddChild(_blackPiece);

        preview.AddChild(PixelUiSkin.Image("DetailsSeparator",
            SetupAssets + "preview/separador_vista_previa_132x5.png",
            new Vector2(38, 458), new Vector2(528, 20), 4));
        _boardName = PixelUiSkin.Label("BoardName", "TABLERO CLASICO",
            new Vector2(42, 485), new Vector2(520, 40), 23,
            pixel: true, weight: UiFontWeight.SemiBold);
        preview.AddChild(_boardName);
        _matchLabel = PixelUiSkin.Label("Teams", "BLANCAS VS NEGRAS",
            new Vector2(42, 533), new Vector2(520, 40), 23,
            pixel: true, weight: UiFontWeight.SemiBold);
        preview.AddChild(_matchLabel);
        preview.AddChild(PixelUiSkin.Label("RuleTag",
            "REGLAS CLASICAS - PARTIDA NORMAL",
            new Vector2(42, 581), new Vector2(520, 40), 19,
            pixel: true, weight: UiFontWeight.Medium));

        // V4 wood panel, using the native 170x165 art at exact x4 size.
        var form = new Control
        {
            Name = "Options",
            Position = new Vector2(696, 182),
            Size = new Vector2(680, 660),
            MouseFilter = Control.MouseFilterEnum.Pass,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        _setup.AddChild(form);
        form.AddChild(PixelUiSkin.Image("PanelArt",
            SetupAssets + "panels/panel_personalizacion_170x165.png",
            Vector2.Zero, new Vector2(680, 660), 4));
        form.AddChild(PixelUiSkin.Label("Heading", "PERSONALIZACION",
            new Vector2(40, 21), new Vector2(600, 54), 33,
            pixel: true, light: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.Bold));
        form.AddChild(PixelUiSkin.Label("AppearanceLabel", "ESTILO VISUAL",
            new Vector2(43, 73), new Vector2(560, 30), 23,
            pixel: true, light: true, weight: UiFontWeight.Medium));

        // Keep the hidden selector as the single source of truth for game mode,
        // saved configuration and the existing VisualThemeCatalog mapping.
        _appearance = MakeOption("Appearance", Vector2.Zero, new Vector2(1, 1));
        _appearance.AddItem("CLASICO");
        _appearance.AddItem("PICADA PERSONALIZADA");
        _appearance.Visible = false;
        form.AddChild(_appearance);

        _classicStyleCard = MakeStyleCard("ClassicStyle", "CLASICO", "classic", 0,
            new Vector2(41, 108), () => { _appearance.Select(0); UpdatePreview(); });
        form.AddChild(_classicStyleCard);
        _picadaStyleCard = MakeStyleCard("PicadaStyle", "PICADA\nPERSONALIZADA", "picada", 1,
            new Vector2(347, 108), () => { _appearance.Select(1); UpdatePreview(); });
        form.AddChild(_picadaStyleCard);

        _themeFields = new Control
        {
            Name = "ThemeFields",
            Position = new Vector2(0, 230),
            Size = new Vector2(680, 164),
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        form.AddChild(_themeFields);
        _themeFields.AddChild(PixelUiSkin.Label("BoardLabel", "TABLERO",
            new Vector2(43, 0), new Vector2(216, 44), 22,
            pixel: true, light: true, weight: UiFontWeight.Medium));
        _boardSkin = MakeOption("Board", new Vector2(264, 0), new Vector2(355, 44));
        foreach (var skin in VisualThemeCatalog.Boards) _boardSkin.AddItem(Ascii(skin.Label));
        _themeFields.AddChild(_boardSkin);
        _themeFields.AddChild(PixelUiSkin.Label("WhiteLabel", "BLANCAS - QUESO",
            new Vector2(43, 60), new Vector2(216, 44), 22,
            pixel: true, light: true, weight: UiFontWeight.Medium));
        _whiteSkin = MakeOption("White", new Vector2(264, 60), new Vector2(355, 44));
        _whiteSkin.AddItem("ALEATORIO");
        foreach (var skin in VisualThemeCatalog.Cheeses) _whiteSkin.AddItem(Ascii(skin.Label));
        _themeFields.AddChild(_whiteSkin);
        _themeFields.AddChild(PixelUiSkin.Label("BlackLabel", "NEGRAS - FIAMBRE",
            new Vector2(43, 120), new Vector2(216, 44), 22,
            pixel: true, light: true, weight: UiFontWeight.Medium));
        _blackSkin = MakeOption("Black", new Vector2(264, 120), new Vector2(355, 44));
        _blackSkin.AddItem("ALEATORIO");
        foreach (var skin in VisualThemeCatalog.Charcuterie) _blackSkin.AddItem(Ascii(skin.Label));
        _themeFields.AddChild(_blackSkin);

        _note = PixelUiSkin.Label("Note", "LAS SKINS NO CAMBIAN LAS REGLAS.",
            new Vector2(43, 409), new Vector2(574, 34), 17,
            light: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.Medium);
        form.AddChild(_note);

        // Retain the original gold buttons, 16px icons and handlers.
        form.AddChild(MakeSetupActionButton("Human", "CONTRA HUMANO", "play",
            new Vector2(43, 450), () => HumanRequested?.Invoke()));
        form.AddChild(MakeSetupActionButton("Random", "CONTRA STOCKFISH", "dice",
            new Vector2(43, 505), () => RandomRequested?.Invoke()));
        _continue = MakeSetupActionButton("Continue", "CONTINUAR PARTIDA", "load",
            new Vector2(43, 560), () => ContinueRequested?.Invoke());
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
        var button = PixelUiSkin.Button(name, label, position,
            new Vector2(575, 48), onPressed, fontSize: 26);
        button.AddChild(PixelUiSkin.Image("ActionIcon",
            "icons/" + icon + "_16x16.png",
            new Vector2(18, 6), new Vector2(32, 32), 2));
        return button;
    }

    private Button MakeStyleCard(string name, string label, string design, int index,
        Vector2 position, Action onSelected)
    {
        var button = new Button
        {
            Name = name,
            Text = "",
            Position = position,
            Size = new Vector2(292, 112),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            FocusMode = Control.FocusModeEnum.All,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());

        var cardArt = PixelUiSkin.Image("CardArt",
            StyleCardAsset(design, "idle"),
            Vector2.Zero, new Vector2(292, 112), 4);
        button.AddChild(cardArt);
        // The art contains its own illustration on the left. Keep both
        // captions entirely inside the text area and truly centered vertically.
        var caption = PixelUiSkin.Label("Caption", label,
            new Vector2(112, 0), new Vector2(164, 96),
            index == 0 ? 24 : 20, pixel: true,
            alignment: HorizontalAlignment.Center, weight: UiFontWeight.SemiBold);
        caption.AutowrapMode = TextServer.AutowrapMode.Off;
        button.AddChild(caption);

        void ShowState(string state) => cardArt.Texture = PixelUiSkin.Texture(
            StyleCardAsset(design, state), 4);
        bool Selected() => _appearance.Selected == index;
        // Use each original card state as supplied in the V4 asset pack.
        button.MouseEntered += () => ShowState(Selected() ? "selected" : "hover");
        button.MouseExited += () => ShowState(Selected() ? "selected" : "idle");
        button.ButtonDown += () => ShowState("pressed");
        button.ButtonUp += () => ShowState(Selected() ? "selected" : "hover");
        button.Pressed += onSelected;
        return button;
    }

    // Separate path for Picada's exact V4 pack art: avoids Godot's previously
    // imported 73x28 textures remaining cached after older image revisions.
    private static string StyleCardAsset(string design, string state) =>
        SetupAssets + "cards/" + (design == "picada" ? "picada_v4" : design) +
        "_" + state + "_73x28.png";

    private static void SetStyleCardSelected(Button card, bool selected)
    {
        string design = card.Name.ToString() == "ClassicStyle" ? "classic" : "picada";
        card.GetNode<TextureRect>("CardArt").Texture = PixelUiSkin.Texture(
            StyleCardAsset(design, selected ? "selected" : "idle"), 4);
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
        // Classic displays the same three controls in their disabled state.
        // Picada enables them and immediately refreshes the live preview.
        _themeFields.Visible = true;
        _boardSkin.Disabled = !themed;
        _whiteSkin.Disabled = !themed;
        _blackSkin.Disabled = !themed;
        SetStyleCardSelected(_classicStyleCard, !themed);
        SetStyleCardSelected(_picadaStyleCard, themed);
        _note.Text = SelectedMode == GameMode.ReyDeLaPicada
            ? "REY DE LA PICADA: LLEVA TU REY A D4, E4, D5 O E5."
            : SelectedMode == GameMode.QuesoSuizo
                ? "QUESO SUIZO: LOS AGUJEROS NO SE PUEDEN CRUZAR."
            : SelectedMode == GameMode.Fondue
                ? "FONDUE: COMER PEONES ES NORMAL. OTRAS PIEZAS DERRITEN 3X3."
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
        _rules.Visible = page == Page.Rules;
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