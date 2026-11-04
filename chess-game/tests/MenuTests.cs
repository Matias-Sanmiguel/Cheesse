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
                  root.GetNode<Button>(Home + "Modes").Text == "MODOS" &&
                  root.GetNode<Button>(Home + "Settings").Text == "CONFIGURACION" &&
                  root.GetNode<Button>(Home + "Exit").Text == "SALIR",
                "landing page matches four-button menu layout");
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
            Check(root.GetNode<Control>("Menu/Stage/SetupPage").Visible, "Jugar opens setup directly");
            Press(root, "Menu/Stage/SetupPage/Back");
            Check(root.GetNode<Control>("Menu/Stage/HomePage").Visible, "back returns to correct page");
            Press(root, Home + "Modes");
            Check(root.GetNode<Control>("Menu/Stage/ModesPage").Visible, "Modes navigates to mode selection");
            Check(root.GetNode<Button>(Modes + "Mode1").Disabled &&
                  root.GetNode<Button>(Modes + "Mode2").Disabled &&
                  root.GetNode<Button>(Modes + "Mode3").Disabled,
                "unfinished modes are visibly unavailable");
            var modePage = root.GetNode<Control>("Menu/Stage/ModesPage");
            Check(modePage.HasNode("BrickWall") && modePage.HasNode("Back") &&
                  modePage.HasNode("GameTitle"),
                "mode selection has its own pixel-art backdrop and navigation");
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
            Check(root.GetNode<Label>(Modes + "Mode0/State").Text == "JUGAR",
                "classic mode is playable with a dynamic JUGAR label");
            foreach (var locked in new[] { "Mode1", "Mode2", "Mode3" })
            {
                Check(root.GetNode<Button>(Modes + locked).GetNode<Label>("State").Text == "PROXIMAMENTE" &&
                      root.GetNode<Button>(Modes + locked).HasNode("LockIcon"),
                    locked + " is visibly locked with dynamic copy, without navigation");
            }

            Press(root, Modes + "Mode0");
            Check(root.GetNode<Control>("Menu/Stage/SetupPage").Visible, "classic navigates to match setup");
            var appearance = root.GetNode<OptionButton>(Setup + "Appearance");
            var boardSkin = root.GetNode<OptionButton>(Setup + "ThemeFields/Board");
            var cheeseSkin = root.GetNode<OptionButton>(Setup + "ThemeFields/White");
            var meatSkin = root.GetNode<OptionButton>(Setup + "ThemeFields/Black");
            Check(appearance.Selected == 0 && !root.GetNode<Control>(Setup + "ThemeFields").Visible,
                "classic theme is default");
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
            Check(root.GetNode<Label>("SidePanel/Margin/Content/MovesScroll/MovesText").Text.Contains("e2-e4"),
                "HUD move history reflects actual game state");
            Press(root, "SidePanel/Margin/Content/Files/Save");
            Check(FileAccess.FileExists(SavePath), "save works after themed match");
            Press(root, "SidePanel/Margin/Content/Menu");
            Check(!root.GetNode<Button>(Setup + "Continue").Disabled, "Continue enabled after saving");

            // A new random game must work through the same three-page menu flow.
            StartNew(root, againstRandom: true);
            await Frames();
            var random = root.CurrentGame!;
            Check(!ReferenceEquals(random, human) && root.AgainstRandom, "AI creates a new game");
            Check(random.History.Count == 0, "AI game starts with empty move history");
            Move(root, "e2", "e4");
            Check(random.History.Count == 1, "white can move against the AI");
            Move(root, "e7", "e5");
            Check(random.History.Count == 1, "human cannot move the AI pieces");
            await Frames();
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
            Check(root.CurrentGame.Undo(), "Continue allows undo");

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
        Press(root, Setup + (againstRandom ? "Random" : "Human"));
    }

    private static void ContinueSaved(Node root)
    {
        Press(root, Home + "NewGame");
        Press(root, Setup + "Continue");
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