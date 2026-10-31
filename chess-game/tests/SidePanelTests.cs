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
            Press(root, "Menu/Center/Panel/Margin/Options/Human");
            await Frames();

            var board = root.GetNode<BoardView>("Board");
            var panel = root.GetNode<SidePanelView>("SidePanel");
            var boardRect = new Rect2(board.GlobalPosition, new Vector2(768, 768));
            var panelRect = panel.GetGlobalRect();
            Check(boardRect.End.X < panelRect.Position.X, "board stays to the left of the panel");
            Check(boardRect.Position == new Vector2(64, 40), "board centered in left area");
            Check(panelRect.Position.X >= 0 && panelRect.Position.Y >= 0 &&
                panelRect.End.X <= 1440 && panelRect.End.Y <= 900, "panel fits 1440x900");
            foreach (var name in new[] { "History/Undo", "History/Redo", "Files/Save", "Files/Load", "NewGame", "Random", "Menu" })
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

            Check(Text(root, "Turn") == "Turno: blancas", "initial turn text");
            Check(Text(root, "Status") == "En juego", "initial status text");
            Check(Disabled(root, "History/Undo") && Disabled(root, "History/Redo"), "empty history disables both buttons");
            Check(Disabled(root, "Files/Load"), "missing save disables Load");

            var bus = root.GetChildren().OfType<GodotEventBus>().Single();
            var notifications = 0;
            bus.StateChanged += () => notifications++;
            Move(root, "e2", "e4");
            Check(notifications > 0, "move notifies the bus");
            Check(Text(root, "Turn") == "Turno: negras", "turn changes through bus");
            Check(!Disabled(root, "History/Undo") && Disabled(root, "History/Redo"), "move updates history buttons");
            PressPanel(root, "History/Undo");
            Check(root.CurrentGame!.Board.At(Square("e2"))?.Type == PieceType.Pawn &&
                root.CurrentGame.Board.At(Square("e4")) is null, "Undo button moves pawn back");
            Check(Text(root, "Turn") == "Turno: blancas", "Undo updates turn");
            Check(Disabled(root, "History/Undo") && !Disabled(root, "History/Redo"), "Undo enables Redo");
            PressPanel(root, "History/Redo");
            Check(root.CurrentGame.Board.At(Square("e4"))?.Type == PieceType.Pawn, "Redo button moves pawn forward");
            Check(Text(root, "Turn") == "Turno: negras", "Redo updates turn");
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
            Check(Text(root, "Turn") == "Turno: negras", "Load updates turn");
            Check(!Disabled(root, "History/Undo") && Disabled(root, "History/Redo"), "loaded history can Undo with clean Redo");
            PressPanel(root, "NewGame");
            Check(FenSerializer.Write(root.CurrentGame!.Board) == FenSerializer.InitialPosition, "New game resets position");
            Check(Disabled(root, "History/Undo") && Disabled(root, "History/Redo"), "New game clears both stacks");

            SavePosition(root, "k2r4/8/8/8/8/8/8/4K3 b - - 0 1");
            PressPanel(root, "Files/Load");
            Check(Text(root, "Status") == "En juego" && Text(root, "Turn") == "Turno: negras", "loaded position updates both labels");
            Move(root, "d8", "e8");
            Check(Text(root, "Status") == "Jaque" && Text(root, "Turn") == "Turno: blancas", "giving check updates both labels");
            root.GetNode<Label>(Content + "Status").Text = "pending event";
            bus.Publish(new Chess.Core.Events.CheckRaised(PlayerColor.White));
            Check(Text(root, "Status") == "Jaque", "domain check event refreshes panel through bus");
            Move(root, "e1", "d1");
            Check(Text(root, "Status") == "En juego" && Text(root, "Turn") == "Turno: negras", "escaping check updates both labels");
            PressPanel(root, "History/Undo");
            Check(Text(root, "Status") == "Jaque" && Text(root, "Turn") == "Turno: blancas", "Undo restores check text");
            PressPanel(root, "History/Redo");
            Check(Text(root, "Status") == "En juego", "Redo clears check text");

            PressPanel(root, "NewGame");
            var toggle = root.GetNode<CheckButton>(Content + "Random");
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
            Check(Text(root, "Status") == "Jaque mate", "mate appears on panel");
            PressPanel(root, "History/Undo");
            Check(root.CurrentGame!.Status == GameStatus.Ongoing, "Undo reopens finished game");
            PressPanel(root, "History/Redo");
            Check(root.CurrentGame.Status == GameStatus.Checkmate && Text(root, "Status") == "Jaque mate", "Redo restores mate");
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
