#if DEBUG
using System.Text.Json.Nodes;
using Chess.Core.Model;
using Chess.Core.Services;
using Cheesse.Game.Adapters.Appearance;
using Cheesse.Game.Adapters.Infra;
using Cheesse.Game.Adapters.Input;
using Cheesse.Game.Adapters.Ui;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Cheesse.Game.Tests;

public partial class MenuTests : Node
{
    private const string SavePath = "user://cheesse-save.txt";
    private const string Home = "Menu/Stage/HomePage/Navigation/";
    private const string Modes = "Menu/Stage/ModesPage/";
    private const string Setup = "Menu/Stage/SetupPage/Options/";
    private int _checks;

    public override async void _Ready()
    {
        try
        {
            Check(OS.GetUserDataDir().Contains("Cheesse Menu Tests"), "isolated test user directory");
            RemoveSave();
            var scene = GD.Load<PackedScene>("res://scenes/Main.tscn");
            var root = scene.Instantiate<GameCompositionRoot>();
            AddChild(root);
            await Frames();

            Check(root.GetNode<Control>("Menu").Visible, "startup shows menu");
            Check(root.GetNode<Control>("Menu/Stage/HomePage").Visible, "startup displays a separate home screen");
            Check(!root.GetNode<Control>("Menu/Stage/ModesPage").Visible, "modes screen starts hidden");
            Check(!root.GetNode<BoardView>("Board").Visible, "startup hides board");
            var boardView = root.GetNode<BoardView>("Board");
            Check(BoardView.LightSquare == new Color("e9deb7") &&
                  BoardView.DarkSquare == new Color("c9aa63"),
                "classic board uses the agreed warm cheese palette");
            Check(boardView.CurrentTheme == VisualTheme.Classic && boardView.UsesFlatTiles,
                "classic board uses flat squares without tiled textures");
            Check(!root.GetNode<SidePanelView>("SidePanel").Visible, "startup hides HUD");
            Check(root.CurrentGame is null, "no game before choosing");
            Check(root.GetNode<Button>(Setup + "Continue").Disabled, "Continue disabled without a save");
            Check(!root.GetNode<InputAdapter>("Input").IsProcessingUnhandledInput(), "no chess input in menu");

            Check(ResourceLoader.Exists("res://assets/ui/logos/logo_cheese.png"), "new 192x64 logo imported");
            Check(ResourceLoader.Exists("res://assets/ui/logos/logo_cheese_compact.png"), "new 128x32 compact logo imported");
            Check(ResourceLoader.Exists("res://assets/backgrounds/menu_background.png"), "new 360x225 tavern background imported");
            Check(ResourceLoader.Exists("res://assets/ui/modes/mode_fondue_icon.png"), "four mode illustrations imported");
            foreach (string variant in new[] { "Regular", "Medium", "SemiBold", "Bold" })
            {
                Check(ResourceLoader.Exists("res://assets/ui/fonts/PixelifySans-" + variant + ".ttf"),
                    "Pixelify Sans " + variant + " is bundled and importable");
            }
            Check(root.GetNode<Button>(Home + "NewGame").GetThemeFont("font") ==
                  PixelUiSkin.FontFor(UiFontWeight.Bold), "main menu action uses Bold");
            Check(root.GetNode<Button>(Setup + "Human").GetThemeFont("font") ==
                  PixelUiSkin.FontFor(UiFontWeight.SemiBold), "game action uses SemiBold");
            Check(root.GetNode<OptionButton>(Setup + "Appearance").GetThemeFont("font") ==
                  PixelUiSkin.FontFor(UiFontWeight.Medium), "selector uses Medium");
            Check(root.GetNode<Label>(Setup + "AppearanceLabel").GetThemeFont("font") ==
                  PixelUiSkin.FontFor(UiFontWeight.Medium), "configuration label uses Medium");
            Check(root.GetNode<Label>("Menu/Stage/ModesPage/Hint").GetThemeFont("font") ==
                  PixelUiSkin.FontFor(UiFontWeight.Medium), "instruction uses Medium");
            Check(root.GetNode<Label>("Menu/Stage/ModesPage/Description1").GetThemeFont("font") ==
                  PixelUiSkin.FontFor(UiFontWeight.Regular), "description uses Regular");
            Check(root.GetNode<Label>("Menu/Stage/SetupPage/Options/Heading").GetThemeFont("font") ==
                  PixelUiSkin.FontFor(UiFontWeight.Bold), "section title uses Bold");
            Check(root.GetNode<Label>("SidePanel/Margin/Content/Turn").GetThemeFont("font") ==
                  PixelUiSkin.FontFor(UiFontWeight.SemiBold), "HUD turn label uses SemiBold");
            Check(root.GetNode<Control>("Menu").HasNode("Stage/PixelBackground"), "stage uses new pixel background");
            Check(root.GetNode<Control>("Menu").HasNode("Stage/HomePage/HomeLogo"), "main logo is on landing page");
            Check(root.GetNode<Button>(Home + "NewGame").Text == "JUGAR" &&
                  !root.HasNode(Home + "Modes") &&
                  root.GetNode<Button>(Home + "Settings").Text == "CONFIGURACION" &&
                  root.GetNode<Button>(Home + "Exit").Text == "SALIR" &&
                  root.GetNode<Button>(Home + "NewGame").Position.Y == 384 &&
                  root.GetNode<Button>(Home + "Settings").Position.Y == 504 &&
                  root.GetNode<Button>(Home + "Exit").Position.Y == 624,
                "home displays three centered actions without a redundant MODOS button");
            Check(root.GetNode<Button>(Home + "NewGame").GetThemeStylebox("normal") is StyleBoxTexture,
                "start button uses original pixel-art texture");
            Check(root.GetNode<Button>(Home + "NewGame").GetThemeStylebox("focus") is StyleBoxEmpty,
                "keyboard focus does not look pressed");
            Check(root.GetNode<SidePanelView>("SidePanel").HasNode("Margin/Content/Parchment"),
                "in-game HUD uses the supplied independent parchment artwork");
            Check(ResourceLoader.Exists("res://assets/ingame/02_board_ui/frame_board_422x422.png"),
                "original wooden board frame is available");
            Check(ResourceLoader.Exists("res://assets/ingame/03_sidebar/history_box_270x92.png"),
                "original history panel artwork is available");

            Press(root, Home + "NewGame");
            Check(root.GetNode<Control>("Menu/Stage/ModesPage").Visible &&
                  !root.GetNode<Control>("Menu/Stage/SetupPage").Visible,
                "JUGAR navigates to mode selection before setup");
            Press(root, Modes + "Mode0");
            Check(root.GetNode<Control>("Menu/Stage/SetupPage").Visible &&
                  root.GetNode<MenuView>("Menu").SelectedMode == GameMode.Classic,
                "choosing CLASICO leads to its match setup");
            Press(root, "Menu/Stage/SetupPage/Back");
            Check(root.GetNode<Control>("Menu/Stage/ModesPage").Visible,
                "setup back returns to mode selection");
            Press(root, Modes + "Back");
            Check(root.GetNode<Control>("Menu/Stage/HomePage").Visible,
                "mode selection back returns to home");
            Press(root, Home + "NewGame");
            Check(root.GetNode<Control>("Menu/Stage/ModesPage").Visible,
                "JUGAR consistently reopens the unchanged mode-selection screen");
            Check(!root.GetNode<Button>(Modes + "Mode1").Disabled &&
                  !root.GetNode<Button>(Modes + "Mode2").Disabled &&
                  root.GetNode<Button>(Modes + "Mode3").Disabled,
                "Picada and Swiss are available; Fondue remains locked");
            var modePage = root.GetNode<Control>("Menu/Stage/ModesPage");
            Check(modePage.HasNode("BrickWall") && modePage.HasNode("Back") &&
                  modePage.HasNode("GameLogo"),
                "mode selection has its own pixel-art backdrop, compact logo and navigation");
            var modeLogo = modePage.GetNode<TextureRect>("GameLogo");
            Check(modeLogo.Position.IsEqualApprox(new Vector2(528, 75)) &&
                  modeLogo.Size.IsEqualApprox(new Vector2(384, 96)),
                "mode logo uses the compact 128x32 artwork at x3 scale");
            Check(modePage.GetNode<Button>("Back").Position.IsEqualApprox(new Vector2(66, 86)) &&
                  modePage.GetNode<Label>("Heading").Position.Y >= 188,
                "back button stays in the header and heading is separated from the logo");
            for (int i = 0; i < 4; i++)
            {
                var card = root.GetNode<Button>(Modes + "Mode" + i);
                Check(card.Size.IsEqualApprox(new Vector2(288, 336)),
                    "all mode cards, including classic, occupy equal 96x112 x3 bounds");
                Check(card.GetNode<TextureRect>("Base").Texture ==
                      PixelUiSkin.Texture("res://assets/ui/mode_selection/components/card_base_96x112.png", 3),
                    "mode " + i + " uses the COMPONENT card base instead of a precomposed card");
                Check(card.GetNode<TextureRect>("Illustration").Size.IsEqualApprox(new Vector2(192, 192)) &&
                      card.GetNode<TextureRect>("Illustration").Position.IsEqualApprox(new Vector2(48, 45)),
                    "mode " + i + " draws its original 64x64 illustration at x3, offset 16,15");
                Check(card.HasNode("TitlePlaque") && card.HasNode("ActionPlate") &&
                      card.GetNode<Label>("ModeName").GetThemeFont("font") ==
                          PixelUiSkin.FontFor(UiFontWeight.SemiBold) &&
                      card.GetNode<Label>("State").GetThemeFont("font") ==
                          PixelUiSkin.FontFor(UiFontWeight.Medium),
                    "mode " + i + " composes plaque, editable title and action in Pixelify Sans");
                Check(card.HasNode("HoverOverlay") && card.HasNode("SelectedOverlay") &&
                      !card.GetNode<TextureRect>("HoverOverlay").Visible &&
                      !card.GetNode<TextureRect>("SelectedOverlay").Visible,
                    "mode " + i + " uses the original focus overlays only on interaction");
                Check(root.GetNode<Label>(Modes + "Description" + i).GetThemeFont("font") ==
                      PixelUiSkin.FontFor(UiFontWeight.Regular),
                    "mode " + i + " has independent editable description below the card");
            }
            Check(root.GetNode<Label>(Modes + "Mode0/State").Text == "JUGAR" &&
                  root.GetNode<Label>(Modes + "Mode1/State").Text == "JUGAR" &&
                  root.GetNode<Label>(Modes + "Mode2/State").Text == "JUGAR",
                "classic, Picada and Swiss are playable with dynamic JUGAR labels");
            foreach (var locked in new[] { "Mode3" })
            {
                Check(root.GetNode<Button>(Modes + locked).GetNode<Label>("State").Text == "PROXIMAMENTE" &&
                      root.GetNode<Button>(Modes + locked).HasNode("LockIcon"),
                    locked + " is visibly locked with dynamic copy, without navigation");
            }

            Check(ResourceLoader.Exists("res://assets/ui/overlays/hole_overlay.png"),
                "Swiss holes use the supplied 48x48 overlay without editing the asset");
            Press(root, Modes + "Mode2");
            Check(root.GetNode<Control>("Menu/Stage/SetupPage").Visible &&
                  root.GetNode<MenuView>("Menu").SelectedMode == GameMode.QuesoSuizo &&
                  root.GetNode<Label>("Menu/Stage/SetupPage/Preview/RuleTag").Text.Contains("AGUJEROS"),
                "Swiss mode opens setup with its own blocked-square rule");
            Check(root.GetNode<Button>("Menu/Stage/SetupPage/Options/Random").Disabled,
                "unsupported standard Stockfish cannot be started in Swiss mode");
            Press(root, "Menu/Stage/SetupPage/Back");
            Check(root.GetNode<Control>("Menu/Stage/ModesPage").Visible,
                "Swiss setup back preserves mode selection");
            Press(root, Modes + "Mode1");
            Check(root.GetNode<Control>("Menu/Stage/SetupPage").Visible &&
                  root.GetNode<MenuView>("Menu").SelectedMode == GameMode.ReyDeLaPicada,
                "Picada opens its own match setup");
            var setupPage = root.GetNode<Control>("Menu/Stage/SetupPage");
            var setupPreview = setupPage.GetNode<Control>("Preview");
            var setupOptions = setupPage.GetNode<Control>("Options");
            Check(!root.GetNode<TextureRect>("Menu/Stage/CompactLogo").Visible &&
                  setupPage.GetNode<Label>("Heading").Text == "PREPARA TU PARTIDA" &&
                  setupPreview.Position.IsEqualApprox(new Vector2(62, 182)) &&
                  setupOptions.Position.IsEqualApprox(new Vector2(696, 182)) &&
                  setupPreview.Size.IsEqualApprox(new Vector2(604, 660)) &&
                  setupOptions.Size.IsEqualApprox(new Vector2(680, 660)) &&
                  setupPreview.GetNode<TextureRect>("PanelArt").Texture?.GetWidth() == 604 &&
                  setupOptions.GetNode<TextureRect>("PanelArt").Texture?.GetWidth() == 680,
                "setup uses the supplied native V4 panels at exact x4 scale without an extra logo");
            Check(setupPreview.GetNode<TextureRect>("BoardFrame").Texture?.GetWidth() == 328 &&
                  setupPreview.GetNode<TextureRect>("WhiteFrame").Texture?.GetWidth() == 160 &&
                  setupPreview.GetNode<TextureRect>("BlackFrame").Texture?.GetWidth() == 160 &&
                  setupPreview.GetNode<TextureRect>("DetailsSeparator").Texture?.GetWidth() == 528 &&
                  setupOptions.GetNode<Button>("ClassicStyle").Size.X == 292 &&
                  setupOptions.GetNode<Button>("PicadaStyle").Size.X == 292 &&
                  !root.GetNode<OptionButton>(Setup + "Appearance").Visible,
                "V4 preview frames, separator and style cards use exact integer scaling");
            Check(setupOptions.GetNode<Label>("Heading").Position.Y == 21 &&
                  setupOptions.GetNode<Label>("AppearanceLabel").Position.Y == 73 &&
                  setupOptions.GetNode<Control>("ThemeFields").Position.Y == 230 &&
                  setupOptions.GetNode<OptionButton>("ThemeFields/White").Position.Y == 60 &&
                  setupOptions.GetNode<OptionButton>("ThemeFields/Black").Position.Y == 120 &&
                  setupOptions.GetNode<Label>("Note").Position.Y == 409 &&
                  setupOptions.GetNode<Button>("Human").Position.Y == 450 &&
                  setupOptions.GetNode<Button>("Random").Position.Y == 505 &&
                  setupOptions.GetNode<Button>("Continue").Position.Y == 560 &&
                  setupOptions.Size.Y - (setupOptions.GetNode<Button>("Continue").Position.Y + 48) >= 50,
                "setup content sits higher with compact controls and 50px of bottom padding");
            Check(setupPreview.GetNode<TextureRect>("WhiteFrame").Position == new Vector2(388, 103) &&
                  setupPreview.GetNode<TextureRect>("BlackFrame").Position == new Vector2(388, 275) &&
                  setupPreview.GetNode<TextureRect>("WhitePiece").Position == new Vector2(420, 133) &&
                  setupPreview.GetNode<TextureRect>("BlackPiece").Position == new Vector2(420, 305) &&
                  setupPreview.GetNode<TextureRect>("WhitePiece").Size == new Vector2(96, 96) &&
                  setupPreview.GetNode<TextureRect>("BlackPiece").Size == new Vector2(96, 96),
                "piece preview frames move left together with their unscaled-content king sprites");
            foreach (var action in new[] { "Human", "Random", "Continue" })
            {
                var actionButton = setupOptions.GetNode<Button>(action);
                var actionIcon = actionButton.GetNode<TextureRect>("ActionIcon");
                Check(actionButton.Icon is null && actionIcon.Texture is not null &&
                      actionIcon.Position.IsEqualApprox(new Vector2(18, 8)) &&
                      actionIcon.Size.IsEqualApprox(new Vector2(32, 32)) &&
                      actionIcon.MouseFilter == Control.MouseFilterEnum.Ignore,
                    action + " icon fits inside its 48px button without blocking input");
            }
            foreach (var state in new[] { "idle", "hover", "pressed", "selected" })
            {
                var picture = GD.Load<Texture2D>(
                    "res://assets/ui/setup_v4/cards/picada_" + state + "_73x28.png")!.GetImage();
                Check(picture.GetWidth() > 0 && picture.GetHeight() > 0 &&
                      picture.GetWidth() * 28 == picture.GetHeight() * 73,
                    "Picada " + state + " preserves its 73:28 pixel-art aspect ratio");
            }
            var classicCard = setupOptions.GetNode<Button>("ClassicStyle");
            var picadaCard = setupOptions.GetNode<Button>("PicadaStyle");
            var classicCaption = classicCard.GetNode<Label>("Caption");
            var picadaCaption = picadaCard.GetNode<Label>("Caption");
            Check(classicCard.Position == new Vector2(41, 108) &&
                  picadaCard.Position == new Vector2(347, 108) &&
                  picadaCard.Position.X - (classicCard.Position.X + classicCard.Size.X) == 14 &&
                  classicCaption.Position == new Vector2(112, 0) &&
                  picadaCaption.Position == new Vector2(112, 0) &&
                  classicCaption.Size == new Vector2(164, 112) &&
                  picadaCaption.Size == new Vector2(164, 112) &&
                  picadaCaption.AutowrapMode == TextServer.AutowrapMode.Off,
                "style cards are centered with even outer margins and aligned captions");
            Press(root, Setup + "ClassicStyle");
            Check(root.GetNode<OptionButton>(Setup + "Appearance").Selected == 0 &&
                  root.GetNode<Control>(Setup + "ThemeFields").Visible &&
                  setupOptions.GetNode<OptionButton>("ThemeFields/Board").Disabled &&
                  setupOptions.GetNode<OptionButton>("ThemeFields/White").Disabled &&
                  setupOptions.GetNode<OptionButton>("ThemeFields/Black").Disabled &&
                  setupPreview.GetNode<ColorRect>("Square0").Color == BoardView.LightSquare &&
                  setupPreview.GetNode<ColorRect>("Square1").Color == BoardView.DarkSquare &&
                  setupPreview.GetNode<TextureRect>("Tile0").Texture is null &&
                  setupOptions.GetNode<Button>("ClassicStyle").GetNode<TextureRect>("CardArt").Texture ==
                      PixelUiSkin.Texture("res://assets/ui/setup_v4/cards/classic_selected_73x28.png", 4),
                "classic style displays original green/ivory board and disabled theme controls");
            Press(root, Setup + "PicadaStyle");
            Check(root.GetNode<OptionButton>(Setup + "Appearance").Selected == 1 &&
                  root.GetNode<Control>(Setup + "ThemeFields").Visible &&
                  !setupOptions.GetNode<OptionButton>("ThemeFields/Board").Disabled &&
                  !setupOptions.GetNode<OptionButton>("ThemeFields/White").Disabled &&
                  !setupOptions.GetNode<OptionButton>("ThemeFields/Black").Disabled &&
                  setupOptions.GetNode<Button>("PicadaStyle").GetNode<TextureRect>("CardArt").Texture ==
                      PixelUiSkin.Texture("res://assets/ui/setup_v4/cards/picada_selected_73x28.png", 4),
                "Picada style selects V4 artwork and enables existing theme dropdowns");
            Check(root.GetNode<OptionButton>(Setup + "Appearance").Selected == 1 &&
                  root.GetNode<Label>("Menu/Stage/SetupPage/Preview/RuleTag").Text.Contains("D4"),
                "Picada defaults to themed pieces and explains the center objective");
            Press(root, Setup + "Human");
            await Frames();
            Check(root.CurrentGame!.Mode == GameMode.ReyDeLaPicada &&
                  root.GetNode<BoardView>("Board").ShowsPicadaGoals &&
                  root.GetNode<Control>("InGamePresentation/ArtworkStage/PicadaBadge").Visible,
                "Picada reaches gameplay with four highlighted goals and a HUD badge");

            Check(root.GetNode<Label>("SidePanel/Margin/Content/Mode").Text.Contains("PICADA"),
                "sidebar identifies the active Picada rules rather than classic chess");

            // Real input path: walk the king to d4 without exposing it to check.
            Move(root, "f2", "f4");
            Move(root, "a7", "a6");
            Move(root, "e1", "f2");
            Move(root, "a6", "a5");
            Move(root, "f2", "e3");
            Move(root, "a5", "a4");
            Move(root, "e3", "d4");
            Check(root.CurrentGame.Status == GameStatus.PicadaVictory &&
                  root.CurrentGame.History.Count == 7,
                "Picada victory is triggered by a legal center move through UI input");
            var victoryFx = root.GetNodeOrNull<Node2D>("Board/PicadaVictoryFx");
            Check(victoryFx is not null && victoryFx.Visible &&
                  victoryFx.HasNode("WinningCrown") &&
                  victoryFx.HasNode("VictoryPlaque/VictoryCaption") &&
                  victoryFx.GetChildren().OfType<Sprite2D>().Count() >= 24 &&
                  !root.GetNode<GameOverOverlay>("GameOverOverlay").Visible,
                "Picada starts a visible crown, caption and 24 particle celebration before the result");
            await ToSignal(GetTree().CreateTimer(0.95), SceneTreeTimer.SignalName.Timeout);
            Check(root.GetNodeOrNull<Node2D>("Board/PicadaVictoryFx") is { Visible: true } &&
                  !root.GetNode<GameOverOverlay>("GameOverOverlay").Visible,
                "Picada particles remain on the board long enough to be noticeable");
            await ToSignal(GetTree().CreateTimer(1.25), SceneTreeTimer.SignalName.Timeout);
            var result = root.GetNode<GameOverOverlay>("GameOverOverlay");
            var resultPanel = root.GetNode<Control>("GameOverOverlay/Overlay/Center/Panel");
            var modalContent = root.GetNode<Control>("GameOverOverlay/Overlay/Center/Panel/Content");
            var resultBanner = modalContent.GetNode<TextureRect>("ResultBanner");
            Check(result.Visible &&
                  modalContent.GetNode<Label>("Reason").Text == "REY AL CENTRO" &&
                  modalContent.GetNode<Label>("Title").Text == "GANARON BLANCAS" &&
                  modalContent.GetNode<Label>("TimeLabel").Text == "TIEMPO" &&
                  modalContent.GetNode<Label>("TimeValue").Text.Contains(':'),
                "Picada wins show a dynamic winner, reason and elapsed time");
            // The winner is drawn directly over the green ribbon; no inset badge.
            var winnerRibbonImage = modalContent.GetNode<TextureRect>("WinnerRibbon").Texture!.GetImage();
            var ribbonBehindIcon = winnerRibbonImage.GetPixel(15, 12);
            Check(ribbonBehindIcon.G > ribbonBehindIcon.R &&
                  ribbonBehindIcon.G > ribbonBehindIcon.B,
                "winner ribbon shows uninterrupted green behind the original piece icon");
            var winnerIcon = modalContent.GetNode<TextureRect>("WinnerIcon");
            Check(winnerIcon.Texture is not null &&
                  winnerIcon.Texture.ResourcePath.EndsWith("/white_12x12.png") &&
                  winnerIcon.Position.IsEqualApprox(new Vector2(29 * 4, 71 * 4)) &&
                  winnerIcon.Size.IsEqualApprox(new Vector2(12 * 4, 12 * 4)),
                "winner ribbon uses its original small white piece, centered without any extra box");
            var rematch = modalContent.GetNode<Button>("Rematch");
            var mainMenu = modalContent.GetNode<Button>("MainMenu");
            Check(!rematch.HasNode("RematchIcon") && !mainMenu.HasNode("MainMenuIcon") &&
                  rematch.GetNode<Label>("RematchLabel").Position.X == 0 &&
                  mainMenu.GetNode<Label>("MainMenuLabel").Position.X == 0,
                "rematch and main menu are clean text-only buttons with centered labels");
            Check(resultPanel.Size.IsEqualApprox(new Vector2(864, 776)) &&
                  resultBanner.Position.IsEqualApprox(new Vector2(48, 0)) &&
                  resultBanner.Size.IsEqualApprox(new Vector2(768, 256)) &&
                  modalContent.HasNode("ResultPanel") &&
                  modalContent.HasNode("WinnerRibbon") &&
                  modalContent.HasNode("StatsSeparator") &&
                  modalContent.HasNode("TimeIcon") &&
                  modalContent.HasNode("TimeValue") &&
                  !modalContent.HasNode("StatFrame0") &&
                  !modalContent.HasNode("StatFrame1") &&
                  !modalContent.HasNode("StatFrame2"),
                "result modal keeps the original V2 artwork with a compact clock only");
            Check(modalContent.GetNode<Button>("Rematch").Position.Y == 136 * 4 &&
                  modalContent.GetNode<Button>("MainMenu").Position.Y == 158 * 4,
                "result action buttons are raised below the compact time row");
            Check(Mathf.Abs(resultPanel.GlobalPosition.X - (1440 - 864) / 2f) < 1f &&
                  Mathf.Abs(resultPanel.GlobalPosition.Y - (900 - 776) / 2f) < 1f,
                "result panel centers in the entire 1440x900 viewport, not board bounds");
            Press(root, "GameOverOverlay/Overlay/Center/Panel/Content/MainMenu");
            Press(root, Home + "NewGame");
            Press(root, Modes + "Mode0");
            Check(root.GetNode<Control>("Menu/Stage/SetupPage").Visible, "classic navigates to match setup");
            Check(root.GetNode<MenuView>("Menu").SelectedMode == GameMode.Classic &&
                  root.GetNode<OptionButton>(Setup + "Appearance").Selected == 0,
                "switching back to classic resets the default theme and selected rules");
            var appearance = root.GetNode<OptionButton>(Setup + "Appearance");
            var boardSkin = root.GetNode<OptionButton>(Setup + "ThemeFields/Board");
            var cheeseSkin = root.GetNode<OptionButton>(Setup + "ThemeFields/White");
            var meatSkin = root.GetNode<OptionButton>(Setup + "ThemeFields/Black");
            Check(appearance.Selected == 0 &&
                  root.GetNode<Control>(Setup + "ThemeFields").Visible &&
                  boardSkin.Disabled && cheeseSkin.Disabled && meatSkin.Disabled,
                "classic theme is default with green board and visible disabled selectors");
            Check(boardSkin.ItemCount == 8 && cheeseSkin.ItemCount == 7 && meatSkin.ItemCount == 7,
                "all eight board options, six cheeses and six meats are available");
            appearance.Select(1);
            appearance.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
            boardSkin.Select(0);
            cheeseSkin.Select(1); // Brie
            meatSkin.Select(3); // Pepperoni
            Check(root.GetNode<Control>(Setup + "ThemeFields").Visible, "picnic controls appear");
            Check(root.GetNode<MenuView>("Menu").CreateVisualTheme() ==
                new VisualTheme("01_TablaDePicada", "01_Brie_vs_Salame", "03_Emmental_vs_Pepperoni"),
                "independent cheese and meat selections map to exact folders");
            var randomTheme = VisualThemeCatalog.Create(0, 0, 0, new Random(3));
            Check(VisualThemeCatalog.Cheeses.Any(option => option.Folder == randomTheme.WhitePack) &&
                VisualThemeCatalog.Charcuterie.Any(option => option.Folder == randomTheme.BlackPack),
                "random is resolved to a complete team per side");

            Press(root, Setup + "Human");
            await Frames();
            var human = root.CurrentGame!;
            Check(!root.AgainstRandom, "human mode selected");
            Check(FenSerializer.Write(human.Board) == FenSerializer.InitialPosition,
                "human game starts from initial position");
            Check(root.GetNode<BoardView>("Board").CurrentTheme ==
                new VisualTheme("01_TablaDePicada", "01_Brie_vs_Salame", "03_Emmental_vs_Pepperoni"),
                "board receives chosen appearance");
            Check(!boardView.UsesFlatTiles,
                "custom boards keep their individual tile textures");
            Check(!root.GetNode<Control>("Menu").Visible && root.GetNode<BoardView>("Board").Visible,
                "starting game hides menu and shows board");
            Move(root, "e2", "e4");
            await Frames();
            Check(human.History.Count == 1 && human.Turn == PlayerColor.Black, "human game waits for black");
            Move(root, "e7", "e5");
            Check(human.History.Count == 2, "both players can make legal moves");
            Check(root.GetNode<Label>("SidePanel/Margin/Content/MovesScroll/MovesText").Text.Contains("e4"),
                "HUD move history reflects actual game state");
            Press(root, "SidePanel/Margin/Content/Files/Save");
            Check(FileAccess.FileExists(SavePath), "save works after themed match");
            Press(root, "SidePanel/Margin/Content/Menu");
            Check(!root.GetNode<Button>(Setup + "Continue").Disabled, "Continue enabled after saving");

            // A new random game must work through the same three-page menu flow.
            // Opening a fresh setup resets its visual defaults; choose this match's theme.
            Press(root, Home + "NewGame");
            Press(root, Modes + "Mode0");
            appearance.Select(1);
            appearance.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
            boardSkin.Select(0);
            cheeseSkin.Select(1);
            meatSkin.Select(3);
            Press(root, Setup + "Random");
            await Frames();
            var random = root.CurrentGame!;
            Check(!ReferenceEquals(random, human) && root.AgainstRandom, "AI creates a new game");
            Check(random.History.Count == 0, "AI game starts with empty move history");
            Move(root, "e2", "e4");
            Check(random.History.Count == 1, "white can move against the AI");
            Move(root, "e7", "e5");
            Check(random.History.Count == 1, "human cannot move the AI pieces");
            Check(root.GetNode<BoardView>("Board").IsMoveAnimating && random.History.Count == 1,
                "AI waits for the white piece to finish sliding");
            await ToSignal(GetTree().CreateTimer(0.32), SceneTreeTimer.SignalName.Timeout);
            await Frames();
            await WaitFor(() => random.History.Count == 2);
            Check(random.History.Count == 2 && random.Turn == PlayerColor.White, "AI replies with a legal move");
            Press(root, "SidePanel/Margin/Content/Files/Save");
            var expectedFen = FenSerializer.Write(random.Board);
            var expectedHistory = random.History.ToArray();
            string saved;
            using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read))
                saved = file.GetAsText();

            Press(root, "SidePanel/Margin/Content/Menu");
            await Frames();
            Check(random.History.Count == 2, "AI is paused while menu is open");
            ContinueSaved(root);
            Check(root.CurrentGame is not null && !ReferenceEquals(root.CurrentGame, random),
                "Continue constructs a new game from saved state");
            Check(FenSerializer.Write(root.CurrentGame!.Board) == expectedFen, "Continue restores board");
            Check(root.CurrentGame.History.SequenceEqual(expectedHistory), "Continue restores move history");
            Check(root.AgainstRandom, "Continue restores AI mode");
            Check(root.GetNode<BoardView>("Board").CurrentTheme ==
                new VisualTheme("01_TablaDePicada", "01_Brie_vs_Salame", "03_Emmental_vs_Pepperoni"),
                "Continue preserves Nico's chosen theme alongside engine metadata");
            Press(root, "SidePanel/Margin/Content/NewGame");
            Check(root.GetNode<BoardView>("Board").CurrentTheme ==
                new VisualTheme("01_TablaDePicada", "01_Brie_vs_Salame", "03_Emmental_vs_Pepperoni"),
                "rematch preserves the independently selected visual theme");

            Press(root, "SidePanel/Margin/Content/Menu");
            using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write))
                file.StoreString("{");
            var before = root.CurrentGame;
            ContinueSaved(root);
            Check(root.GetNode<Control>("Menu").Visible, "invalid save keeps menu open");
            Check(ReferenceEquals(before, root.CurrentGame), "invalid save preserves current game");
            Check(root.GetNode<Label>(Home + "Message").Text.Length > 0,
                "invalid save displays feedback");

            var legacy = JsonNode.Parse(saved)!;
            legacy.AsObject().Remove("AgainstRandom");
            legacy.AsObject().Remove("Theme");
            legacy.AsObject().Remove("Mode");
            legacy.AsObject().Remove("AgainstStockfish");
            legacy["Version"] = 1;
            using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write))
                file.StoreString(legacy.ToJsonString());
            root.Free();
            root = scene.Instantiate<GameCompositionRoot>();
            AddChild(root);
            await Frames();
            Check(root.GetNode<Control>("Menu/Stage/HomePage").Visible,
                "restart with a save still starts at home");
            Check(!root.GetNode<Button>(Setup + "Continue").Disabled,
                "restart recognizes an old save");
            ContinueSaved(root);
            Check(!root.AgainstRandom, "old save defaults to human opponent");
            Check(FenSerializer.Write(root.CurrentGame!.Board) == expectedFen, "old save preserves board");
            Check(root.GetNode<BoardView>("Board").CurrentTheme == VisualTheme.Classic,
                "old save defaults to classic appearance");

            Press(root, "SidePanel/Margin/Content/Menu");
            RemoveSave();
            root.ShowMenu();
            Check(root.GetNode<Button>(Setup + "Continue").Disabled, "Continue disabled after save is removed");
            StartNew(root, againstRandom: false);
            Check(root.CurrentGame!.History.Count == 0, "new game clears saved history");
            Check(FenSerializer.Write(root.CurrentGame.Board) == FenSerializer.InitialPosition,
                "new game resets position");

            GD.Print($"PASS: menu, {_checks} checks.");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private static void StartNew(Node root, bool againstRandom)
    {
        Press(root, Home + "NewGame");
        Press(root, Modes + "Mode0");
        Press(root, Setup + (againstRandom ? "Random" : "Human"));
    }

    private static void ContinueSaved(Node root)
    {
        Press(root, Home + "NewGame");
        Press(root, Modes + "Mode0");
        Press(root, Setup + "Continue");
    }

    private async Task WaitFor(Func<bool> condition)
    {
        var deadline = Time.GetTicksMsec() + 15000;
        while (!condition())
        {
            if (Time.GetTicksMsec() > deadline) throw new TimeoutException("Stockfish did not reply.");
            await Frames();
        }
    }

    private async Task Frames()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Press(Node root, string path) =>
        root.GetNode<Button>(path).EmitSignal(BaseButton.SignalName.Pressed);

    private void Move(Node root, string from, string to)
    {
        Click(root, from);
        Click(root, to);
    }

    private void Click(Node root, string square)
    {
        var local = new Vector2((square[0] - 'a') * 96 + 48, (7 - (square[1] - '1')) * 96 + 48);
        var position = root.GetNode<BoardView>("Board").GetGlobalTransformWithCanvas() * local;
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left,
            Pressed = true, ButtonMask = MouseButtonMask.Left,
        }, true);
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false,
        }, true);
    }

    private static void RemoveSave()
    {
        if (FileAccess.FileExists(SavePath) && DirAccess.RemoveAbsolute(SavePath) != Error.Ok)
            throw new InvalidOperationException("Cannot remove isolated test save.");
    }

    private void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + description);
        _checks++;
    }
}
#endif