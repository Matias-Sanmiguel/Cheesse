#if DEBUG
using Chess.Core.Model;
using Chess.Core.Services;
using Cheesse.Game.Adapters.Infra;
using Cheesse.Game.Adapters.Persist;
using Cheesse.Game.Adapters.Ui;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Cheesse.Game.Tests;

public partial class SidePanelTests : Node
{
    private const string SavePath = "user://cheesse-save.txt";
    private const string Content = "SidePanel/Margin/Content/";
    private int _checks;

    public override async void _Ready()
    {
        try
        {
            Check(OS.GetUserDataDir().Contains("Cheesse SidePanel Tests"), "isolated user directory");
            if (FileAccess.FileExists(SavePath)) DirAccess.RemoveAbsolute(SavePath);
            var root = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameCompositionRoot>();
            AddChild(root);
            Press(root, "Menu/Stage/HomePage/Navigation/NewGame");
            Press(root, "Menu/Stage/SetupPage/Options/Human");
            await Frames();

            var board = root.GetNode<BoardView>("Board");
            var panel = root.GetNode<SidePanelView>("SidePanel");
            var viewport = GetViewport().GetVisibleRect().Size;
            Vector2 offset = new(
                Mathf.Max(0f, (viewport.X - 1440f) * 0.5f),
                Mathf.Max(0f, (viewport.Y - 900f) * 0.5f));
            var boardRect = new Rect2(board.GlobalPosition, new Vector2(768, 768));
            var panelRect = panel.GetGlobalRect();
            Check(boardRect.End.X < panelRect.Position.X, "board stays to the left of the panel");
            Check(board.GlobalPosition.DistanceTo(offset + new Vector2(244, 62)) < 1.5f,
                "board sits inside the doubled frame at its original position");
            Check(board.Scale.IsEqualApprox(Vector2.One),
                "board node is NEVER scaled; all 96px squares remain 96px");
            Check(boardRect.Size.IsEqualApprox(new Vector2(768, 768)),
                "8x8 board keeps exact 768x768 native resolution");
            var frame = root.GetNode<TextureRect>("InGamePresentation/ArtworkStage/BoardFrame");
            Check(frame.Size.IsEqualApprox(new Vector2(844, 844)),
                "frame expands 2x to fit the original board, not vice versa");
            Check(frame.ZIndex > board.ZIndex &&
                  frame.MouseFilter == Control.MouseFilterEnum.Ignore,
                "wooden border renders over board but never intercepts clicks");
            var framePixels = frame.Texture?.GetImage();
            Check(framePixels is not null &&
                  framePixels.GetPixel(211, 211).A == 0f &&
                  framePixels.GetPixel(18, 18).A > 0.99f,
                "front frame has a fully transparent board opening and opaque rim");
            var presentation = root.GetNode<InGamePresentationView>("InGamePresentation");
            Check(presentation.Visible && presentation.HasNode("ArtworkStage/BoardFrame"),
                "in-game presentation and wooden frame are rendered");
            Check(ResourceLoader.Exists("res://assets/ingame/01_background/ingame_picada_1440x900.png"),
                "1440x900 background is imported");
            Check(ResourceLoader.Exists("res://assets/ingame/01_background/ingame_picada_960x540.png"),
                "16:9 background is imported");
            Check(ResourceLoader.Exists("res://assets/ingame/04_buttons/cheese_hover_9slice_48x32.png"),
                "pixel button hover state is imported");
            Check(panel.HasNode("Margin/Content/Parchment"),
                "sidebar uses independent parchment background");
            Check(panel.GetNode<Button>("Margin/Content/NewGame").GetThemeStylebox("normal") is StyleBoxTexture,
                "real gameplay button has pixel-art nine-slice style");
            Check(root.GetNode<Button>("InGameUtilities/UtilityStage/HelpButton").Visible &&
                  root.GetNode<Button>("InGameUtilities/UtilityStage/SoundButton").Visible,
                "top utility buttons are interactive and layered above the sidebar");
            // Regression: visual resources must never be stretched along different axes.
            // The frame is exactly x2, while the interactive board stays scale (1,1).
            var stagePath = "InGamePresentation/ArtworkStage/";
            var whiteCard = root.GetNode<NinePatchRect>(stagePath + "WhiteCard");
            var blackCard = root.GetNode<NinePatchRect>(stagePath + "BlackCard");
            Check(whiteCard.Size.IsEqualApprox(new Vector2(188, 66)) &&
                  blackCard.Size.IsEqualApprox(new Vector2(188, 66)),
                "team cards retain their 66px native height");
            Check(blackCard.Position.Y < whiteCard.Position.Y,
                "black team card is above and white team card is below");
            Check(blackCard.Position.Y < board.GlobalPosition.Y + 300 &&
                  whiteCard.Position.Y > board.GlobalPosition.Y + 500,
                "cards follow the black-top white-bottom chess orientation");
            Check(whiteCard.PatchMarginLeft == 58 && whiteCard.PatchMarginTop == 10,
                "nine-patch preserves the artwork around player icons and borders");
            foreach (var side in new[] { "White", "Black" })
            {
                var card = side == "White" ? whiteCard : blackCard;
                var badge = root.GetNode<TextureRect>(stagePath + side + "Badge");
                var name = root.GetNode<Label>(stagePath + side + "Name");
                var timerIcon = root.GetNode<TextureRect>(stagePath + side + "TimerIcon");
                var timer = root.GetNode<Label>(stagePath + side + "Clock");

                Check(badge.Size.IsEqualApprox(new Vector2(32, 32)) &&
                      (badge.Position - card.Position).IsEqualApprox(new Vector2(14, 16)),
                    side + " icon follows the square four pixels to the right");
                string expectedArt = "res://assets/ingame/02_board_ui/player_card_" +
                    side.ToLowerInvariant() + "_aligned_128x66.png";
                Check(ResourceLoader.Exists(expectedArt) &&
                      card.Texture?.ResourcePath == expectedArt,
                    side + " card uses its shifted-square artwork rather than the original");
                Check(name.HorizontalAlignment == HorizontalAlignment.Center &&
                      (name.Position - card.Position).IsEqualApprox(new Vector2(56, 13)),
                    side + " title is aligned within the text area, clear of borders");
                // Godot can increase Label.Size to accommodate the selected font,
                // even when a smaller initial Size is supplied. Never assume that
                // the rendered control's auto-minimum height equals 20 px.
                Check((timerIcon.Position - card.Position).IsEqualApprox(new Vector2(76, 35)) &&
                      (timer.Position - card.Position).IsEqualApprox(new Vector2(99, 34)),
                    side + " clock icon and value share a compact row");
                Check(Mathf.Abs(timer.Position.Y - name.Position.Y - 21) < 1 &&
                      timer.Position.Y + 22 <= card.Position.Y + card.Size.Y - 4,
                    side + " title and clock origins have consistent vertical padding");
            }
            foreach (string art in new[] { "ModeBar", "TurnBar" })
            {
                var image = panel.GetNode<TextureRect>("Margin/Content/" + art);
                Check(image.Size.IsEqualApprox(new Vector2(270, 28)),
                    art + " remains a native-size 270x28 status bar");
            }
            foreach (string art in new[] { "CrownBadge", "TurnBadge" })
            {
                var image = panel.GetNode<TextureRect>("Margin/Content/" + art);
                Check(image.Size.IsEqualApprox(new Vector2(24, 24)),
                    art + " retains native square pixels");
            }
            const string settingsTogglePath = "InGameUtilities/UtilityStage/SettingsPopup/Random";
            var toggle = root.GetNode<CheckButton>(settingsTogglePath);
            Check(!panel.HasNode("Margin/Content/Random") &&
                  toggle.GetParent().Name == "SettingsPopup",
                "random opponent switch lives in game settings, not the turn bar");
            Check(!panel.HasNode("Margin/Content/StatusBar"),
                "game status uses the existing dot with no third status rectangle");
            var statusDot = panel.GetNode<TextureRect>("Margin/Content/StatusDot");
            Check(statusDot.Modulate.G > statusDot.Modulate.R,
                "ongoing status is represented by a green dot");
            Check(toggle.Text == "NEGRAS ALEATORIAS",
                "settings explain the opponent control");
            Check(toggle.GetThemeIcon("on") == InGameUiSkin.Texture("05_icons/random_16.png") &&
                  toggle.GetThemeIcon("off") == InGameUiSkin.Texture("05_icons/switch_16.png"),
                "random toggle uses real CheckButton on/off icons");
            var coordsFont = PixelUiSkin.FontFor(UiFontWeight.Medium) ?? ThemeDB.FallbackFont;
            float rankGlyphWidth = coordsFont.GetStringSize("8", HorizontalAlignment.Left, -1, 16).X;
            float rankGlyphCenter = board.GlobalPosition.X - 12f - rankGlyphWidth / 2f;
            var rank8 = root.GetNode<TextureRect>(stagePath + "RankBadge8");
            Check(Mathf.Abs(rank8.GlobalPosition.X + rank8.Size.X / 2f - rankGlyphCenter) < 1f,
                "number 8 is centered horizontally on its badge");
            // Regression: when the wooden frame is drawn above the board it
            // must NEVER cover the original rank/file badge art or lettering.
            // All sixteen badges and their text remain foreground, visible and
            // mouse-transparent. A sample of only four missed this regression.
            for (int i = 0; i < 8; i++)
            {
                string rank = (8 - i).ToString();
                string fileLabel = ((char)('a' + i)).ToString();
                foreach (var coordinate in new[]
                {
                    (name: "RankBadge" + rank, label: rank),
                    (name: "FileBadge" + i, label: fileLabel)
                })
                {
                    var image = root.GetNode<TextureRect>(stagePath + coordinate.name);
                    var text = root.GetNode<Label>(stagePath + coordinate.name + "Text");
                    Check(image.Size.IsEqualApprox(new Vector2(44, 44)) &&
                          image.Visible && image.ZIndex > frame.ZIndex,
                        coordinate.name + " remains visible above wooden frame");
                    Check(text.Text == coordinate.label && text.Visible &&
                          text.ZIndex > image.ZIndex && text.Position == image.Position,
                        coordinate.name + " shows the centered coordinate above badge");
                    Check(image.MouseFilter == Control.MouseFilterEnum.Ignore &&
                          text.MouseFilter == Control.MouseFilterEnum.Ignore,
                        coordinate.name + " never captures chessboard mouse input");
                }
            }
            // A single bad TextureRect dimension is enough to damage pixel art.
            // Check all supplied sprites, not only the ones reported in screenshots.
            void CheckIntegerSpriteScaling(Node parent)
            {
                foreach (Node child in parent.GetChildren())
                {
                    if (child is TextureRect image && image.Texture is { } source)
                    {
                        float scaleX = image.Size.X / source.GetWidth();
                        float scaleY = image.Size.Y / source.GetHeight();
                        Check(Mathf.Abs(scaleX - scaleY) < 0.001f &&
                              Mathf.Abs(scaleX - Mathf.Round(scaleX)) < 0.001f,
                            child.Name + " keeps square pixels at an integer scale");
                    }
                    CheckIntegerSpriteScaling(child);
                }
            }
            CheckIntegerSpriteScaling(presentation.GetNode<Control>("ArtworkStage"));
            CheckIntegerSpriteScaling(panel.GetNode<Control>("Margin/Content"));
            CheckIntegerSpriteScaling(root.GetNode<Control>("InGameUtilities/UtilityStage"));

            var whiteClock = root.GetNode<Label>(stagePath + "WhiteClock");
            var blackClock = root.GetNode<Label>(stagePath + "BlackClock");
            Check(blackClock.GlobalPosition.Y < whiteClock.GlobalPosition.Y,
                "black time displays above white time in the left column");
            Check(Mathf.Abs(blackClock.Position.Y - blackCard.Position.Y - 34) < 0.1f &&
                  Mathf.Abs(whiteClock.Position.Y - whiteCard.Position.Y - 34) < 0.1f,
                "both timers share consistent padding inside their cards");
            Check(whiteClock.Text == "00:00" && blackClock.Text == "00:00",
                "session timers start at zero instead of displaying fake chess clocks");
            presentation._Process(2.2);
            Check(whiteClock.Text == "00:02" && blackClock.Text == "00:00",
                "white elapsed-time clock advances while white has the move");
            Check(panelRect.Position.X >= 0 && panelRect.Position.Y >= 0 &&
                panelRect.End.X <= 1440 && panelRect.End.Y <= 900, "panel fits 1440x900");
            foreach (var name in new[] { "History/Undo", "History/Redo", "Files/Save", "Files/Load", "NewGame", "Menu" })
            {
                var rect = root.GetNode<Control>(Content + name).GetGlobalRect();
                Check(!rect.Intersects(boardRect), name + " does not overlap board");
                Check(rect.Position.X >= panelRect.Position.X && rect.End.X <= panelRect.End.X &&
                    rect.Position.Y >= panelRect.Position.Y && rect.End.Y <= panelRect.End.Y, name + " fits panel");
            }

            using (var image = GD.Load<Texture2D>("res://assets/pieces.svg").GetImage())
            {
                var whitePawn = image.GetPixel(40, 23);
                var blackPawn = image.GetPixel(40, 103);
                Check(whitePawn.A > 0.9f && whitePawn.R > 0.9f && whitePawn.G > 0.9f, "white pawn has white fill");
                Check(blackPawn.A > 0.9f && blackPawn.R < 0.2f && blackPawn.G < 0.2f, "black pawn has black fill");
                Check(image.GetWidth() == 480 && image.GetHeight() == 160, "six piece types in each color");
                image.SavePng("user://pieces-preview.png");
            }

            Check(Text(root, "Turn") == "TURNO: BLANCAS", "initial turn text");
            Check(Text(root, "Status") == "EN JUEGO", "initial status text");
            Check(Disabled(root, "History/Undo") && Disabled(root, "History/Redo"), "empty history disables both buttons");
            Check(Disabled(root, "Files/Load"), "missing save disables Load");

            var bus = root.GetChildren().OfType<GodotEventBus>().Single();
            var notifications = 0;
            bus.StateChanged += () => notifications++;
            Move(root, "e2", "e4");
            Check(notifications > 0, "move notifies the bus");
            presentation._Process(3.2);
            Check(blackClock.Text == "00:03" && whiteClock.Text == "00:02",
                "elapsed time switches to black after white moves");
            Check(Text(root, "Turn") == "TURNO: NEGRAS", "turn changes through bus");
            Check(!Disabled(root, "History/Undo") && Disabled(root, "History/Redo"), "move updates history buttons");
            PressPanel(root, "History/Undo");
            Check(root.CurrentGame!.Board.At(Square("e2"))?.Type == PieceType.Pawn &&
                root.CurrentGame.Board.At(Square("e4")) is null, "Undo button moves pawn back");
            Check(Text(root, "Turn") == "TURNO: BLANCAS", "Undo updates turn");
            Check(Disabled(root, "History/Undo") && !Disabled(root, "History/Redo"), "Undo enables Redo");
            PressPanel(root, "History/Redo");
            Check(root.CurrentGame.Board.At(Square("e4"))?.Type == PieceType.Pawn, "Redo button moves pawn forward");
            Check(Text(root, "Turn") == "TURNO: NEGRAS", "Redo updates turn");
            PressPanel(root, "History/Undo");
            Move(root, "d2", "d4");
            Check(Disabled(root, "History/Redo"), "new branch clears Redo");

            PressPanel(root, "Files/Save");
            Check(!Disabled(root, "Files/Load"), "saving enables Load");
            var savedFen = FenSerializer.Write(root.CurrentGame.Board);
            Move(root, "d7", "d5");
            var openGame = root.CurrentGame;
            PressPanel(root, "Files/Load");
            Check(ReferenceEquals(root.CurrentGame, openGame), "Load keeps open game instance");
            Check(FenSerializer.Write(root.CurrentGame.Board) == savedFen, "Load button restores saved position");
            Check(Text(root, "Turn") == "TURNO: NEGRAS", "Load updates turn");
            Check(!Disabled(root, "History/Undo") && Disabled(root, "History/Redo"), "loaded history can Undo with clean Redo");
            PressPanel(root, "NewGame");
            Check(FenSerializer.Write(root.CurrentGame!.Board) == FenSerializer.InitialPosition, "New game resets position");
            Check(Disabled(root, "History/Undo") && Disabled(root, "History/Redo"), "New game clears both stacks");

            SavePosition(root, "k2r4/8/8/8/8/8/8/4K3 b - - 0 1");
            PressPanel(root, "Files/Load");
            Check(Text(root, "Status") == "EN JUEGO" && Text(root, "Turn") == "TURNO: NEGRAS", "loaded position updates both labels");
            Move(root, "d8", "e8");
            Check(Text(root, "Status") == "JAQUE" && Text(root, "Turn") == "TURNO: BLANCAS", "giving check updates both labels");
            root.GetNode<Label>(Content + "Status").Text = "pending event";
            bus.Publish(new Chess.Core.Events.CheckRaised(PlayerColor.White));
            Check(Text(root, "Status") == "JAQUE", "domain check event refreshes panel through bus");
            Move(root, "e1", "d1");
            Check(Text(root, "Status") == "EN JUEGO" && Text(root, "Turn") == "TURNO: NEGRAS", "escaping check updates both labels");
            PressPanel(root, "History/Undo");
            Check(Text(root, "Status") == "JAQUE" && Text(root, "Turn") == "TURNO: BLANCAS", "Undo restores check text");
            PressPanel(root, "History/Redo");
            Check(Text(root, "Status") == "EN JUEGO", "Redo clears check text");

            PressPanel(root, "NewGame");
            toggle = root.GetNode<CheckButton>(settingsTogglePath);
            toggle.ButtonPressed = true;
            Check(root.AgainstRandom, "toggle enables RandomAI");
            Move(root, "e2", "e4");
            await Frames();
            Check(root.CurrentGame!.History.Count == 2 && root.CurrentGame.Turn == PlayerColor.White, "random black replies");
            PressPanel(root, "History/Undo");
            Check(root.CurrentGame.History.Count == 1, "Undo removes random black move");
            await Frames();
            Check(root.CurrentGame.History.Count == 1, "random waits while reviewing history");
            PressPanel(root, "History/Redo");
            Check(root.CurrentGame.History.Count == 2, "Redo restores random black move");
            PressPanel(root, "History/Undo");
            toggle.ButtonPressed = false;
            Check(!root.AgainstRandom, "toggle disables RandomAI");
            Move(root, "e7", "e5");
            await Frames();
            Check(root.CurrentGame.History.Count == 2 && Disabled(root, "History/Redo"), "manual black can branch after toggle");

            PressPanel(root, "NewGame");
            Move(root, "e2", "e4");
            toggle.ButtonPressed = true;
            await Frames();
            Check(root.CurrentGame!.History.Count == 2, "enabling random on black turn makes a legal reply");
            PressPanel(root, "Files/Save");
            toggle.ButtonPressed = false;
            PressPanel(root, "Files/Load");
            Check(root.AgainstRandom && toggle.ButtonPressed, "Load restores opponent and toggle");

            SavePosition(root, FenSerializer.InitialPosition, "f2f3", "e7e5", "g2g4", "d8h4");
            PressPanel(root, "Files/Load");
            Check(Text(root, "Status") == "JAQUE MATE", "mate appears on panel");
            PressPanel(root, "History/Undo");
            Check(root.CurrentGame!.Status == GameStatus.Ongoing, "Undo reopens finished game");
            PressPanel(root, "History/Redo");
            Check(root.CurrentGame.Status == GameStatus.Checkmate && Text(root, "Status") == "JAQUE MATE", "Redo restores mate");
            PressPanel(root, "History/Undo");
            var beforeFailedLoad = FenSerializer.Write(root.CurrentGame.Board);
            using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write)) file.StoreString("{");
            PressPanel(root, "Files/Load");
            Check(FenSerializer.Write(root.CurrentGame.Board) == beforeFailedLoad, "invalid Load keeps position");
            Check(!Disabled(root, "History/Redo"), "invalid Load keeps redo history");
            Check(Text(root, "Message").Length > 0, "invalid Load shows feedback");

            DirAccess.RemoveAbsolute(SavePath);
            GD.Print($"PASS: sidepanel, {_checks} checks.");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private static void SavePosition(GameCompositionRoot root, string fen, params string[] moves)
    {
        var repository = new FileSaveRepository();
        var game = new ChessGame(root.CurrentGame!.Events, repository, root.CurrentGame.Clock, FenSerializer.Load(fen));
        foreach (var move in moves)
        {
            if (!game.TryMove(new Move(Square(move[..2]), Square(move[2..4]))))
                throw new InvalidOperationException("Invalid test move: " + move);
        }
        repository.Save(game);
    }

    private static Square Square(string name) => new(name[0] - 'a', name[1] - '1');

    private static string Text(Node root, string name) => root.GetNode<Label>(Content + name).Text;

    private static bool Disabled(Node root, string name) => root.GetNode<Button>(Content + name).Disabled;

    private static void Press(Node root, string path) => root.GetNode<Button>(path).EmitSignal(BaseButton.SignalName.Pressed);

    private static void PressPanel(Node root, string name) => Press(root, Content + name);

    private void Move(Node root, string from, string to)
    {
        Click(root, from);
        Click(root, to);
    }

    private void Click(Node root, string name)
    {
        var square = Square(name);
        var local = new Vector2(square.File * 96 + 48, (7 - square.Rank) * 96 + 48);
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

    private async Task Frames()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + description);
        _checks++;
    }
}
#endif
