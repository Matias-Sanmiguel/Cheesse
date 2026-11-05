#if DEBUG
using Chess.Core.Model;
using Chess.Core.Services;
using Cheesse.Game.Adapters.Infra;
using Cheesse.Game.Adapters.Ui;
using Godot;

namespace Cheesse.Game.Tests;

public partial class ReleaseTests : Node
{
    private int _checks;
    private const string Settings = "InGameUtilities/UtilityStage/SettingsPopup/";
    private const string Content = "SidePanel/Margin/Content/";

    public override async void _Ready()
    {
        try
        {
            Check(OS.GetUserDataDir().Contains("Cheesse Release Tests"), "isolated test directory");
            var root = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameCompositionRoot>();
            AddChild(root);
            await Frames();
            await Capture("menu");
            if (OS.GetCmdlineUserArgs().Contains("--screenshots"))
            {
                ClickControl(root, "Menu/Stage/HomePage/Navigation/NewGame");
                await Frames();
                await Capture("mode-selection");
                ClickControl(root, "Menu/Stage/ModesPage/Mode1");
                await Frames();
                Check(root.GetNode<Control>("Menu/Stage/RulesPage").Visible &&
                      root.GetNode<Label>("Menu/Stage/RulesPage/Objective").Text.Contains("D4"),
                    "Picada opens its rules screen before any game configuration");
                await Capture("picada-rules");
                ClickControl(root, "Menu/Stage/RulesPage/Continue");
                await Frames();
                await Capture("picada-setup");
                ClickControl(root, "Menu/Stage/SetupPage/Back");
                await Frames();
                Check(root.GetNode<Control>("Menu/Stage/RulesPage").Visible,
                    "setup Back returns to the selected rules page");
                ClickControl(root, "Menu/Stage/RulesPage/Back");
                await Frames();
                ClickControl(root, "Menu/Stage/ModesPage/Mode0");
                await Frames();
                await Capture("classic-rules");
                ClickControl(root, "Menu/Stage/RulesPage/Continue");
                await Frames();
            }
            root.GetNode<OptionButton>("Menu/Stage/SettingsPage/SettingsPanel/TimeControl").Select(2);
            root.StartHumanGame();
            Check(root.CurrentGame!.TimeControl == new TimeControl(300, 3), "menu selects 5+3 clock");
            Check(root.GetNode<Label>("InGamePresentation/ArtworkStage/WhiteClock").Text.Contains("05:00"), "white clock visible");
            await WaitUntil(() => root.CurrentGame!.ClockState.WhiteSeconds < 299.9);
            Check(root.CurrentGame.ClockState.WhiteSeconds < 299.9, "live frames consume thinking time");
            Play(root, "e2e4", "e7e5");
            Check(root.GetNode<Label>(Content + "MovesScroll/MovesText").Text.Contains("e4") && root.GetNode<Label>(Content + "MovesScroll/MovesText").Text.Contains("e5"), "SAN history rendered");
            await Capture("panel");
            ClickControl(root, "InGameUtilities/UtilityStage/SettingsButton");
            await Frames();
            var popup = root.GetNode<Control>(Settings.TrimEnd('/'));
            Check(popup.Visible, "settings opens with a real mouse click");
            foreach (var path in new[] { "Difficulty", "TextFiles/Import", "TextFiles/Export", "EndActions/Resign", "EndActions/Draw", "Claim" })
            {
                var rect = root.GetNode<Control>(Settings + path).GetGlobalRect();
                Check(popup.GetGlobalRect().Encloses(rect), path + " remains inside settings panel: " + rect + " within " + popup.GetGlobalRect());
            }
            await Capture("settings");
            ClickControl(root, Settings + "TextFiles/Import");
            Check(root.GetNode<FileDialog>("ImportDialog").Visible, "reparented Import button opens file picker by mouse");
            root.GetNode<FileDialog>("ImportDialog").Hide();
            ClickControl(root, Settings + "TextFiles/Export");
            Check(root.GetNode<FileDialog>("ExportDialog").Visible, "reparented Export button opens file picker by mouse");
            root.GetNode<FileDialog>("ExportDialog").Hide();
            ClickControl(root, "InGameUtilities/UtilityStage/SettingsButton");
            await Frames();
            var before = root.CurrentGame;
            Check(!root.ImportGameText("invalid", ".fen") && ReferenceEquals(before, root.CurrentGame), "bad import preserves live game");
            Check(root.ImportGameText("[Result \"1-0\"]\n\n1. e4 e5 1-0", ".pgn"), "PGN imported through game flow");
            Check(root.CurrentGame!.Status == GameStatus.Resigned && root.CurrentGame.Winner == PlayerColor.White, "imported winner shown");
            Check(root.GetNode<GameOverOverlay>("GameOverOverlay").Visible, "imported finished game shows result");
            Check(root.ExportGameText(".pgn").Contains("1-0"), "PGN export retains result");
            Check(root.ImportGameText(FenSerializer.InitialPosition, ".fen"), "FEN import opens new position");
            Check(!root.AgainstStockfish && root.CurrentGame!.TimeControl == TimeControl.Unlimited, "imports open human analysis without clock");
            Check(root.CurrentGame.OfferDraw(PlayerColor.White), "white can offer draw");
            Play(root, "e2e4");
            Press(root, "EndActions/Draw");
            Check(root.CurrentGame.Status == GameStatus.DrawByAgreement, "panel accepts opponent draw");
            root.StartHumanGame();
            root.GetNode<InGamePresentationView>("InGamePresentation")._Process(2.2);
            Press(root, "EndActions/Resign");
            var confirmation = root.GetNode<ConfirmationDialog>("ResignDialog");
            Check(confirmation.Visible, "resignation requires confirmation");
            confirmation.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            Check(root.CurrentGame!.Status == GameStatus.Resigned && root.CurrentGame.Winner == PlayerColor.Black, "resignation ends game");
            Check(root.GetNode<Label>("GameOverOverlay/Overlay/Center/Panel/Content/TimeValue").Text == "00:02",
                "timed game result includes the played session duration");

            root.GetNode<OptionButton>("Menu/Stage/SettingsPage/SettingsPanel/TimeControl").Select(0);
            root.StartHumanGame();
            root.CurrentGame!.ConfigureClock(new TimeControl(600, 5));
            Press(root, "NewGame");
            Check(root.CurrentGame.TimeControl == new TimeControl(600, 5), "new game preserves active time control");
            root.StartStockfishGame();
            var difficulty = root.GetNode<OptionButton>(Settings + "Difficulty");
            difficulty.Select(2);
            difficulty.EmitSignal(OptionButton.SignalName.ItemSelected, 2L);
            Press(root, "NewGame");
            Check(difficulty.Selected == 2, "new game preserves side-panel Stockfish strength");
            Check(root.AgainstStockfish, "Stockfish mode selected");
            Play(root, "e2e4");
            await WaitUntil(() => root.IsThinking);
            Check(!root.GetNode<BoardView>("Board").IsMoveAnimating, "Stockfish waits for the player's piece slide");
            Check(root.IsThinking, "engine searches asynchronously");
            Press(root, "History/Undo");
            await WaitUntil(() => !root.IsThinking);
            Check(root.CurrentGame!.History.Count == 0, "undo cancels stale engine reply");
            Play(root, "d2d4");
            await WaitUntil(() => root.CurrentGame!.History.Count == 2);
            Check(root.CurrentGame!.Turn == PlayerColor.White, "real bundled Stockfish replies legally");
            root.ShowMenu();
            var remaining = root.CurrentGame.ClockState;
            await Frames();
            Check(!root.CurrentGame.ClockState.Running, "menu pauses clock");
            root.StartHumanGame();
            root.CurrentGame!.ConfigureClock(new TimeControl(1, 0));
            await WaitUntil(() => root.CurrentGame.Status == GameStatus.Timeout);
            Check(root.CurrentGame.Winner == PlayerColor.Black && root.GetNode<GameOverOverlay>("GameOverOverlay").Visible, "real countdown expires and displays winner");
            Check(root.ImportGameText("[Variant \"Fondue\"]\n[SetUp \"1\"]\n[FEN \"7k/8/8/8/8/8/8/K2R4 b - - 0 1\"]\n[Result \"*\"]\n\n*", ".pgn"), "Fondue analysis imports");
            root.GetNode<CheckButton>(Settings + "Random").ButtonPressed = true;
            await Frames();
            Check(!root.IsThinking && !root.AgainstStockfish, "unsupported Fondue engine fails safely");
            Check(root.CurrentGame!.Board.Mode == GameMode.Fondue && root.CurrentGame.History.Count == 0,
                "unsupported engine leaves Fondue position untouched");
            Play(root, "h8h7");
            Check(root.CurrentGame.History.Count == 1, "Fondue continues between humans after engine fallback");
            const string picadaPosition = "[Variant \"Rey de la Picada\"] [SetUp \"1\"] [FEN \"7k/8/8/8/8/4K3/8/8 w - - 0 1\"] [Result \"*\"] *";
            Check(root.ImportGameText(picadaPosition, ".pgn"), "Picada analysis imports with its own rules");
            Play(root, "e3e4");
            await WaitUntil(() => root.GetNode<GameOverOverlay>("GameOverOverlay").Visible);
            await Capture("picada-result");
            root.GetNode<Button>("GameOverOverlay/Overlay/Center/Panel/Content/Rematch").EmitSignal(BaseButton.SignalName.Pressed);
            Check(root.CurrentGame!.Mode == GameMode.ReyDeLaPicada && root.CurrentGame.History.Count == 0,
                "Picada rematch retains its mode with a fresh board");
            Check(root.ImportGameText(picadaPosition, ".pgn"), "Picada cancellation fixture imports");
            Play(root, "e3e4");
            Press(root, "History/Undo");
            await ToSignal(GetTree().CreateTimer(2.2), SceneTreeTimer.SignalName.Timeout);
            Check(root.CurrentGame!.Status == GameStatus.Ongoing && !root.GetNode<GameOverOverlay>("GameOverOverlay").Visible,
                "Undo cancels a delayed Picada result");
            Press(root, "History/Redo");
            root.ShowMenu();
            await ToSignal(GetTree().CreateTimer(2.2), SceneTreeTimer.SignalName.Timeout);
            Check(root.GetNode<Control>("Menu").Visible && !root.GetNode<GameOverOverlay>("GameOverOverlay").Visible,
                "menu cancels a delayed Picada result");
            Check(root.ImportGameText(picadaPosition, ".pgn"), "Picada new-game fixture imports");
            Play(root, "e3e4");
            Press(root, "NewGame");
            await ToSignal(GetTree().CreateTimer(2.2), SceneTreeTimer.SignalName.Timeout);
            Check(root.CurrentGame!.Mode == GameMode.ReyDeLaPicada && root.CurrentGame.History.Count == 0 &&
                !root.GetNode<GameOverOverlay>("GameOverOverlay").Visible,
                "new Picada game retains mode and cancels the previous delayed result");
            // Swiss Cheese is a real selectable match, not just an unlocked card.
            root.ShowMenu();
            await Frames();
            ClickControl(root, "Menu/Stage/HomePage/Navigation/NewGame");
            await Frames();
            ClickControl(root, "Menu/Stage/ModesPage/Mode2");
            await Frames();
            Check(root.GetNode<Control>("Menu/Stage/RulesPage").Visible &&
                  root.GetNode<Label>("Menu/Stage/RulesPage/Rule0").Text.Contains("AGUJEROS"),
                "Swiss rules board explains the randomly generated holes");
            await Capture("swiss-rules");
            ClickControl(root, "Menu/Stage/RulesPage/Continue");
            await Frames();
            Check(root.GetNode<MenuView>("Menu").SelectedMode == GameMode.QuesoSuizo,
                "Swiss menu card selects the new ruleset");
            ClickControl(root, "Menu/Stage/SetupPage/Options/Human");
            await Frames();
            Check(root.CurrentGame!.Mode == GameMode.QuesoSuizo &&
                  root.GetNode<BoardView>("Board").ShowsSwissHoles &&
                  root.CurrentGame.Board.SwissHoles.Count == 6 &&
                  root.CurrentGame.Board.SwissHoles.All(hole => root.CurrentGame.Board.IsBlocked(hole)),
                "Swiss match displays six generated holes and installs blocking rules");
            Check(root.GetNode<CheckButton>(Settings + "Random").Disabled &&
                  !root.AgainstStockfish,
                "unsupported Stockfish toggle stays disabled in Swiss mode");
            var swissOpening = root.CurrentGame.LegalMoves().First();
            Play(root, swissOpening.From.ToString() + swissOpening.To.ToString());
            await Frames();
            Check(root.CurrentGame.History.Count == 1 &&
                  root.CurrentGame.LegalMoves().All(move => !root.CurrentGame.Board.IsBlocked(move.To)),
                "Swiss game accepts legal moves but never offers blocked destinations");
            await Capture("swiss-board");
            var originalSwissLayout = Chess.Core.Rules.SwissCheeseRules.Format(root.CurrentGame.Board.SwissHoles);
            Press(root, "NewGame");
            await Frames();
            Check(root.CurrentGame!.Mode == GameMode.QuesoSuizo &&
                  Chess.Core.Rules.SwissCheeseRules.Format(root.CurrentGame.Board.SwissHoles) != originalSwissLayout,
                "new Swiss match preserves the mode and rerolls the holes");

            // Fondue is selectable through the original card and reuses the
            // existing chess-core cascade, save flow and themed board.
            root.ShowMenu();
            await Frames();
            ClickControl(root, "Menu/Stage/HomePage/Navigation/NewGame");
            await Frames();
            ClickControl(root, "Menu/Stage/ModesPage/Mode3");
            await Frames();
            Check(root.GetNode<Control>("Menu/Stage/RulesPage").Visible &&
                  root.GetNode<Label>("Menu/Stage/RulesPage/Rule3").Text.Contains("CALOR") &&
                  root.GetNode<Label>("Menu/Stage/RulesPage/Rule4").Text.Contains("caballo", StringComparison.OrdinalIgnoreCase),
                "Fondue rules explain both local melting and hot-square knight jumps");
            await Capture("fondue-rules");
            ClickControl(root, "Menu/Stage/RulesPage/Continue");
            await Frames();
            Check(root.GetNode<MenuView>("Menu").SelectedMode == GameMode.Fondue &&
                  root.GetNode<Label>("Menu/Stage/SetupPage/Preview/RuleTag").Text.Contains("DERRITEN"),
                "Fondue menu card opens real match setup with melting rules");
            Check(root.GetNode<Button>("Menu/Stage/SetupPage/Options/Random").Disabled,
                "standard Stockfish is not offered for Fondue");
            ClickControl(root, "Menu/Stage/SetupPage/Options/Human");
            await Frames();
            Check(root.CurrentGame!.Mode == GameMode.Fondue &&
                  root.CurrentGame.Status == GameStatus.Ongoing && !root.AgainstStockfish,
                "Fondue starts a playable human match");
            await Capture("fondue-start");
            Press(root, "NewGame");
            Check(root.CurrentGame!.Mode == GameMode.Fondue && root.CurrentGame.History.Count == 0,
                "new Fondue match preserves the selected mode");

            // Regression: pawn captures must be playable without unadvertised
            // explosion/check. A real non-pawn capture remains a king threat.
            const string fonduePawnDemo = "[Variant \"Fondue\"]\n[SetUp \"1\"]\n[FEN \"rnbqkbnr/pp3pp1/3p3p/2p1P3/8/8/PPPP2PP/RNBQKBNR w KQkq - 0 1\"]\n[Result \"*\"]\n\n*";
            Check(root.ImportGameText(fonduePawnDemo, ".pgn"),
                "Fondue screenshot position can be loaded");
            Play(root, "e5d6");
            await Frames();
            var fondueBoard = root.GetNode<BoardView>("Board");
            Check(root.CurrentGame!.History.Count == 1 &&
                  root.CurrentGame.Board.At(new Square(3, 5)) ==
                      new Piece(PieceType.Pawn, PlayerColor.White) &&
                  !fondueBoard.ShowsFondueMeltFx,
                "e5xd6 is a normal pawn capture and causes no explosion");
            Check(root.ImportGameText(fonduePawnDemo, ".pgn"),
                "Fondue screenshot position resets");
            Play(root, "e5e6");
            await Frames();
            Check(root.CurrentGame!.Status == GameStatus.Ongoing,
                "e6 no longer creates a false melt check by threatening the f7 pawn");
            await Capture("fondue-regular-pawn");

            const string fondueCheckDemo = "[Variant \"Fondue\"]\n[SetUp \"1\"]\n[FEN \"7k/6n1/8/8/8/8/8/K5R1 b - - 0 1\"]\n[Result \"*\"]\n\n*";
            Check(root.ImportGameText(fondueCheckDemo, ".pgn"),
                "real king-melting check position imports");
            var fondueStatus = root.GetNode<SidePanelView>("SidePanel")
                .GetNode<Label>("Margin/Content/Status");
            Check(root.CurrentGame!.Status == GameStatus.Check &&
                  fondueStatus.Text == "JAQUE POR FONDUE" &&
                  fondueStatus.TooltipText.Contains("g7"),
                "a real non-pawn-capture threat still explains which square endangers the king");
            await Capture("fondue-check");

            // The original heat-overlay asset persists after a melt for
            // exactly one opposing move; landing on it destroys just that piece.
            const string fondueHeatDemo = "[Variant \"Fondue\"]\n[SetUp \"1\"]\n[FEN \"7k/3r4/8/3n4/8/8/8/K2R4 w - - 0 1\"]\n[Result \"*\"]\n\n*";
            Check(root.ImportGameText(fondueHeatDemo, ".pgn"),
                "Fondue heat fixture imports");
            Play(root, "d1d5");
            await Frames();
            var hotBoard = root.GetNode<BoardView>("Board");
            var hotStatus = root.GetNode<SidePanelView>("SidePanel")
                .GetNode<Label>("Margin/Content/Status");
            Check(root.CurrentGame!.Board.HotSquare == Square("d5") &&
                  hotBoard.VisibleHotSquare == Square("d5") &&
                  ResourceLoader.Exists("res://assets/ui/overlays/heat_overlay.png"),
                "melt creates visible hot d5 and uses supplied heat pixel-art asset");
            Check(hotStatus.Text.Contains("CALIENTE") &&
                  hotStatus.TooltipText.Contains("d5"),
                "player receives a warning and knows when the tile cools");
            await Capture("fondue-hot");
            Press(root, "History/Undo");
            await Frames();
            Check(root.CurrentGame!.Board.HotSquare is null &&
                  hotBoard.VisibleHotSquare is null,
                "undo removes the hot effect along with the capture");
            Press(root, "History/Redo");
            await Frames();
            Check(root.CurrentGame!.Board.HotSquare == Square("d5"),
                "redo restores heat without replaying visual particles");
            Play(root, "d7d5");
            await Frames();
            Check(root.CurrentGame!.Board.HotSquare is null &&
                  root.CurrentGame.Board.At(Square("d5")) is null &&
                  root.CurrentGame.History.Count == 2 &&
                  hotBoard.VisibleHotSquare is null,
                "landing on heat burns the rook alone and cools the square");
            Check(hotBoard.ShowsFondueMeltFx,
                "landing on heat plays the melt and steam particles");
            Check(hotBoard.CoolingHotSquare == Square("d5"),
                "expired heat enters a visual cooldown, not an instant disappearance");
            await Capture("fondue-hot-burn");
            await ToSignal(GetTree().CreateTimer(BoardView.FondueHeatFadeSeconds + 0.15),
                SceneTreeTimer.SignalName.Timeout);
            Check(hotBoard.CoolingHotSquare is null,
                "heat overlay fades completely after cooling without remaining dangerous");

            Check(root.ImportGameText(fondueHeatDemo, ".pgn"),
                "Fondue heat crossing fixture can restart");
            Play(root, "d1d5");
            await Frames();
            Play(root, "d7d3");
            await Frames();
            Check(root.CurrentGame!.Board.At(Square("d7")) is null &&
                  root.CurrentGame.Board.At(Square("d3")) is null &&
                  root.CurrentGame.Board.HotSquare is null &&
                  hotBoard.CoolingHotSquare == Square("d5"),
                "rook can move through hot tile but dies on d5 before reaching d3");
            Check(hotBoard.ShowsFondueMeltFx,
                "burning mid-path plays melt effects at the hazardous square");
            await Capture("fondue-hot-cross");

            Check(root.ImportGameText(fondueHeatDemo, ".pgn"),
                "Fondue heat fixture can restart for expiry test");
            Play(root, "d1d5", "h8h7");
            await Frames();
            Check(root.CurrentGame!.Board.HotSquare is null &&
                  root.CurrentGame.Board.At(Square("d7")) ==
                      new Piece(PieceType.Rook, PlayerColor.Black),
                "one ordinary reply cools the hot square without melting other pieces");

            const string fondueVictory = "[Variant \"Fondue\"]\n[SetUp \"1\"]\n[FEN \"7k/6n1/5p2/8/8/8/8/K5R1 w - - 0 1\"]\n[Result \"*\"]\n\n*";
            Check(root.ImportGameText(fondueVictory, ".pgn"),
                "Fondue victory demonstration imports");
            Play(root, "g1g7");
            await Frames();
            var fondueFx = root.GetNode<BoardView>("Board");
            Check(root.CurrentGame!.Status == GameStatus.KingMelted &&
                  root.CurrentGame.Winner == PlayerColor.White &&
                  root.CurrentGame.Board.At(new Square(7, 7)) is null,
                "capture melts its surrounding chain, including the enemy king");
            Check(fondueFx.ShowsFondueMeltFx &&
                  fondueFx.GetNodeOrNull<Node2D>("FondueMeltFx") is { } melt &&
                  melt.GetChildren().OfType<Sprite2D>().Any(sprite => sprite.Name.ToString().StartsWith("MeltSquare")),
                "Fondue renders original melting and steam sprites from domain events");
            Check(!root.GetNode<GameOverOverlay>("GameOverOverlay").Visible,
                "Fondue victory waits for the visual cascade before opening its modal");
            await Capture("fondue-melt");
            await WaitUntil(() => root.GetNode<GameOverOverlay>("GameOverOverlay").Visible);
            Check(root.GetNode<Label>("GameOverOverlay/Overlay/Center/Panel/Content/Reason").Text == "REY DERRETIDO" &&
                  root.GetNode<Label>("GameOverOverlay/Overlay/Center/Panel/Content/Title").Text == "GANARON BLANCAS",
                "Fondue victory modal identifies the melted king and winner");
            await Capture("fondue-result");
            Press(root, "History/Undo");
            await Frames();
            Check(root.CurrentGame.Status == GameStatus.Ongoing &&
                  !root.GetNode<GameOverOverlay>("GameOverOverlay").Visible &&
                  !fondueFx.ShowsFondueMeltFx,
                "undo restores the king, clears effects and cancels the result");
            Press(root, "History/Redo");
            await WaitUntil(() => root.GetNode<GameOverOverlay>("GameOverOverlay").Visible);
            Check(root.CurrentGame.Status == GameStatus.KingMelted,
                "redo restores Fondue victory");
            Press(root, "NewGame");
            await Frames();
            Check(root.CurrentGame!.Mode == GameMode.Fondue &&
                  root.CurrentGame.Status == GameStatus.Ongoing &&
                  root.CurrentGame.History.Count == 0 &&
                  !fondueFx.ShowsFondueMeltFx,
                "Fondue rematch resets to a clean playable board");

            GD.Print($"PASS: release, {_checks} checks.");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--screenshots")) return;
        if (name == "picada-result")
            await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng("/tmp/cheesse-" + name + ".png");
    }

    private async Task WaitUntil(Func<bool> condition)
    {
        var end = Time.GetTicksMsec() + 15000;
        while (!condition())
        {
            if (Time.GetTicksMsec() > end) throw new TimeoutException("UI/Stockfish did not finish.");
            await Frames();
        }
    }

    private async Task Frames()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void ClickControl(Node root, string path)
    {
        var position = root.GetNode<Control>(path).GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position,
            ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position,
            ButtonIndex = MouseButton.Left, Pressed = false }, true);
    }

    private static void Press(Node root, string name) => root.GetNode<Button>((name.StartsWith("EndActions/") ? Settings : Content) + name).EmitSignal(BaseButton.SignalName.Pressed);
    private static Square Square(string name) => new(name[0] - 'a', name[1] - '1');
    private void Play(Node root, params string[] moves)
    {
        foreach (var move in moves) { Click(root, Square(move[..2])); Click(root, Square(move[2..4])); }
    }
    private void Click(Node root, Square square)
    {
        var local = new Vector2(square.File * 96 + 48, (7 - square.Rank) * 96 + 48);
        var position = root.GetNode<BoardView>("Board").GetGlobalTransformWithCanvas() * local;
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position,
            ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position,
            ButtonIndex = MouseButton.Left, Pressed = false }, true);
    }
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        _checks++;
    }
}
#endif
