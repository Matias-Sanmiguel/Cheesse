#if DEBUG
using Chess.Core.Model;
using Chess.Core.Services;
using Cheesse.Game.Adapters.Infra;
using Cheesse.Game.Adapters.Persist;
using Cheesse.Game.Adapters.Ui;
using Godot;

namespace Cheesse.Game.Tests;

public partial class MvpTests : Node
{
    private const string Panel = "SidePanel/Margin/Content/";
    private readonly List<string> _failures = [];
    private int _checks;

    public override async void _Ready()
    {
        try
        {
            Check(OS.GetUserDataDir().Contains("Cheesse Mvp Tests"), "isolated save directory");
            var root = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameCompositionRoot>();
            AddChild(root);
            root.StartHumanGame();
            await Frames();
            Click(root, "e2");
            ClickAt(new Vector2(10, 10));
            Click(root, "e4");
            Check(root.CurrentGame!.History.Count == 0, "off-board click clears selection");

            root.StartHumanGame();
            Click(root, "e2");
            Click(root, "d2");
            Click(root, "d4");
            Check(root.CurrentGame!.History.Count == 0, "illegal destination clears selection even on another own piece");
            root.StartHumanGame();
            Move(root, "e2", "e5");
            Check(root.CurrentGame!.History.Count == 0, "illegal mouse move preserves position");
            Move(root, "e2", "e4");
            Move(root, "e7", "e5");
            Check(root.CurrentGame.History.Count == 2, "mouse plays both sides through commands");
            Keypress(Key.Z, ctrl: true);
            Check(root.CurrentGame.History.Count == 1, "Ctrl+Z undoes a move");
            Press(root, Panel + "History/Redo");
            Check(root.CurrentGame.History.Count == 2, "panel redoes keyboard undo");

            root.StartHumanGame();
            Move(root, "f2", "f3");
            Move(root, "e7", "e5");
            Move(root, "g2", "g4");
            Move(root, "d8", "h4");
            var overlay = root.GetNode<GameOverOverlay>("GameOverOverlay");
            Check(root.CurrentGame!.Status == GameStatus.Checkmate && overlay.Visible, "mouse checkmate opens overlay");
            Check(root.GetNode<Label>("GameOverOverlay/Overlay/Center/Panel/Content/Title").Text == "BLACK WINS", "overlay identifies winner");
            Move(root, "a2", "a3");
            Check(root.CurrentGame.History.Count == 4, "overlay blocks board input");
            await Frames();
            ClickAt(root.GetNode<Button>(Panel + "History/Undo").GetGlobalRect().GetCenter());
            Check(root.CurrentGame.Status == GameStatus.Ongoing && !overlay.Visible, "panel undo dismisses mate overlay");
            Press(root, Panel + "History/Redo");
            Check(overlay.Visible, "redo reopens mate overlay");
            Keypress(Key.Z, ctrl: true);
            Check(root.CurrentGame.Status == GameStatus.Ongoing && !overlay.Visible, "keyboard undo works through overlay");
            Press(root, Panel + "History/Redo");
            Press(root, "GameOverOverlay/Overlay/Center/Panel/Content/Rematch");
            Check(!overlay.Visible && FenSerializer.Write(root.CurrentGame.Board) == FenSerializer.InitialPosition, "rematch restores 32 initial pieces");

            LoadPosition(root, "7k/P7/8/8/8/8/8/7K w - - 0 1");
            Move(root, "a7", "a8");
            var promotion = root.GetNode<PromotionDialog>("PromotionDialog");
            Check(promotion.Visible, "promotion opens choice modal");
            Keypress(Key.Z, ctrl: true);
            Check(promotion.Visible && root.CurrentGame!.History.Count == 0, "undo shortcut does not bypass promotion modal");
            Keypress(Key.Escape);
            Check(!promotion.Visible && root.CurrentGame!.Board.At(Square("a7"))?.Type == PieceType.Pawn, "Escape cancels promotion");
            Move(root, "a7", "a8");
            promotion.GetNode<PromotionWheel>("Overlay/Center/PromotionWheel")
                .EmitSignal(PromotionWheel.SignalName.PieceSelected, (int)PieceType.Knight);
            Check(root.CurrentGame!.Board.At(Square("a8"))?.Type == PieceType.Knight && !promotion.Visible, "underpromotion updates piece");
            Check(overlay.Visible, "insufficient material opens draw overlay");
            Keypress(Key.Z, ctrl: true);
            Check(!overlay.Visible && root.CurrentGame.Board.At(Square("a7"))?.Type == PieceType.Pawn, "undo reopens draw and restores pawn");

            foreach (var fen in new[] {
                "7k/8/5KQ1/8/8/8/8/8 b - - 0 1",
                "7k/8/8/8/8/8/8/R6K w - - 100 50" })
            {
                LoadPosition(root, fen);
                Check(overlay.Visible, "loaded terminal position opens overlay");
            }

            root.StartHumanGame();
            GetWindow().Size = new Vector2I(1280, 720);
            await Frames();
            var panelEnd = GetViewport().GetFinalTransform() * root.GetNode<SidePanelView>("SidePanel").GetGlobalRect().End;
            Check(panelEnd.X <= 1280 && panelEnd.Y <= 720, "panel fits a 1280x720 window after stretching");
            Move(root, "e2", "e4");
            Check(root.CurrentGame!.History.Count == 1, "board input still works after resizing");

            if (_failures.Count > 0) throw new InvalidOperationException(string.Join("\n", _failures));
            GD.Print($"PASS: mvp, {_checks} checks.");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private static void LoadPosition(GameCompositionRoot root, string fen)
    {
        root.StartHumanGame();
        var repository = new FileSaveRepository();
        repository.Save(new ChessGame(root.CurrentGame!.Events, repository, root.CurrentGame.Clock, FenSerializer.Load(fen)));
        Press(root, Panel + "Files/Load");
    }

    private async Task Frames()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Keypress(Key key, bool ctrl = false)
    {
        GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true, CtrlPressed = ctrl });
        GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false, CtrlPressed = ctrl });
    }

    private static Square Square(string name) => new(name[0] - 'a', name[1] - '1');
    private static void Press(Node root, string path) => root.GetNode<Button>(path).EmitSignal(BaseButton.SignalName.Pressed);
    private void Move(Node root, string from, string to) { Click(root, from); Click(root, to); }
    private void Click(Node root, string name)
    {
        var square = Square(name);
        var local = new Vector2(square.File * 96 + 48, (7 - square.Rank) * 96 + 48);
        ClickAt(root.GetNode<BoardView>("Board").GetGlobalTransformWithCanvas() * local);
    }

    private void ClickAt(Vector2 position)
    {
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position,
            ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position,
            ButtonIndex = MouseButton.Left, Pressed = false }, true);
    }

    private void Check(bool condition, string description)
    {
        _checks++;
        if (!condition) _failures.Add("FAIL: " + description);
    }
}
#endif
