#if DEBUG
using System.Text.Json.Nodes;
using Chess.Core.Model;
using Chess.Core.Services;
using Cheesse.Game.Adapters.Infra;
using Cheesse.Game.Adapters.Input;
using Cheesse.Game.Adapters.Ui;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Cheesse.Game.Tests;

public partial class MenuTests : Node
{
    private const string SavePath = "user://cheesse-save.txt";
    private const string Options = "Menu/Center/Panel/Margin/Options/";
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
            Check(!root.GetNode<BoardView>("Board").Visible, "startup hides board");
            Check(!root.GetNode<SidePanelView>("SidePanel").Visible, "startup hides HUD");
            Check(root.CurrentGame is null, "no game before choosing");
            Check(root.GetNode<Button>(Options + "Continue").Disabled, "Continue disabled without save");
            Check(!root.GetNode<InputAdapter>("Input").IsProcessingUnhandledInput(), "input disabled in menu");

            Press(root, Options + "Human");
            await Frames();
            var human = root.CurrentGame!;
            Check(!root.AgainstRandom, "human mode selected");
            Check(FenSerializer.Write(human.Board) == FenSerializer.InitialPosition, "human starts with initial pieces");
            Check(!root.GetNode<Control>("Menu").Visible && root.GetNode<BoardView>("Board").Visible,
                "human displays board and hides menu");
            Move(root, "e2", "e4");
            await Frames();
            Check(human.History.Count == 1 && human.Turn == PlayerColor.Black, "human mode waits for black player");
            Move(root, "e7", "e5");
            Check(human.History.Count == 2, "human can play both colors");
            Press(root, "SidePanel/Margin/Content/Files/Save");
            Check(FileAccess.FileExists(SavePath), "panel saves without a path");
            Press(root, "SidePanel/Margin/Content/Menu");
            Check(!root.GetNode<Button>(Options + "Continue").Disabled, "Continue enabled after saving");

            Press(root, Options + "Random");
            await Frames();
            var random = root.CurrentGame!;
            Check(!ReferenceEquals(random, human) && root.AgainstRandom, "random creates a different game");
            Check(random.History.Count == 0, "random starts a fresh history");
            Move(root, "e2", "e4");
            Check(random.History.Count == 1, "white can move against random");
            Move(root, "e7", "e5");
            Check(random.History.Count == 1, "human cannot move the random opponent");
            await Frames();
            Check(random.History.Count == 2 && random.Turn == PlayerColor.White, "random replies with one legal black move");
            Press(root, "SidePanel/Margin/Content/Files/Save");
            var expectedFen = FenSerializer.Write(random.Board);
            var expectedHistory = random.History.ToArray();
            string saved;
            using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read))
                saved = file.GetAsText();

            Press(root, "SidePanel/Margin/Content/Menu");
            await Frames();
            Check(random.History.Count == 2, "AI pauses in menu");
            Press(root, Options + "Continue");
            Check(root.CurrentGame is not null && !ReferenceEquals(root.CurrentGame, random), "Continue reconstructs save");
            Check(FenSerializer.Write(root.CurrentGame!.Board) == expectedFen, "Continue restores position and turn");
            Check(root.CurrentGame.History.SequenceEqual(expectedHistory), "Continue restores history");
            Check(root.AgainstRandom, "Continue restores random mode");
            Check(root.CurrentGame.Undo(), "Continue retains Undo");

            Press(root, "SidePanel/Margin/Content/Menu");
            using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write))
                file.StoreString("{");
            var before = root.CurrentGame;
            Press(root, Options + "Continue");
            Check(root.GetNode<Control>("Menu").Visible, "invalid save keeps menu open");
            Check(ReferenceEquals(before, root.CurrentGame), "invalid save keeps current game");
            Check(root.GetNode<Label>(Options + "Message").Text.Length > 0, "invalid save shows a message");

            // Existing saves predate the optional opponent metadata and open in human mode.
            var legacy = JsonNode.Parse(saved)!;
            legacy.AsObject().Remove("AgainstRandom");
            using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write))
                file.StoreString(legacy.ToJsonString());
            root.Free();
            root = scene.Instantiate<GameCompositionRoot>();
            AddChild(root);
            await Frames();
            Check(root.GetNode<Control>("Menu").Visible && root.CurrentGame is null,
                "restart with save still starts in menu");
            Check(!root.GetNode<Button>(Options + "Continue").Disabled, "restart enables Continue");
            Press(root, Options + "Continue");
            Check(!root.AgainstRandom, "legacy save opens in human mode");
            Check(FenSerializer.Write(root.CurrentGame!.Board) == expectedFen, "legacy save position preserved");

            Press(root, "SidePanel/Margin/Content/Menu");
            RemoveSave();
            root.ShowMenu();
            Check(root.GetNode<Button>(Options + "Continue").Disabled, "removed save disables Continue");
            Press(root, Options + "Human");
            Check(root.CurrentGame!.History.Count == 0, "new human game resets saved history");
            Check(FenSerializer.Write(root.CurrentGame.Board) == FenSerializer.InitialPosition,
                "new human game resets position");

            GD.Print($"PASS: menu, {_checks} checks.");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
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
        var local = new Vector2((square[0] - 'a') * 80 + 40, (7 - (square[1] - '1')) * 80 + 40);
        var position = root.GetNode<BoardView>("Board").GetGlobalTransformWithCanvas() * local;
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left,
            Pressed = true, ButtonMask = MouseButtonMask.Left,
        });
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false,
        });
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
