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
            root.GetNode<OptionButton>("Menu/Center/Panel/Margin/Options/TimeControl").Select(2);
            root.StartHumanGame();
            Check(root.CurrentGame!.TimeControl == new TimeControl(300, 3), "menu selects 5+3 clock");
            Check(root.GetNode<Label>(Content + "Clocks/White").Text.Contains("05:00"), "white clock visible");
            await WaitUntil(() => root.CurrentGame!.ClockState.WhiteSeconds < 299.9);
            Check(root.CurrentGame.ClockState.WhiteSeconds < 299.9, "live frames consume thinking time");
            Play(root, "e2e4", "e7e5");
            Check(root.GetNode<RichTextLabel>(Content + "Moves").Text.Contains("e4") && root.GetNode<RichTextLabel>(Content + "Moves").Text.Contains("e5"), "SAN history rendered");
            await Capture("panel");
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
            Press(root, "EndActions/Resign");
            var confirmation = root.GetNode<ConfirmationDialog>("ResignDialog");
            Check(confirmation.Visible, "resignation requires confirmation");
            confirmation.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            Check(root.CurrentGame!.Status == GameStatus.Resigned && root.CurrentGame.Winner == PlayerColor.Black, "resignation ends game");

            root.GetNode<OptionButton>("Menu/Center/Panel/Margin/Options/TimeControl").Select(0);
            root.StartHumanGame();
            root.CurrentGame!.ConfigureClock(new TimeControl(600, 5));
            Press(root, "NewGame");
            Check(root.CurrentGame.TimeControl == new TimeControl(600, 5), "new game preserves active time control");
            root.StartStockfishGame();
            var difficulty = root.GetNode<OptionButton>(Content + "Difficulty");
            difficulty.Select(2);
            difficulty.EmitSignal(OptionButton.SignalName.ItemSelected, 2L);
            Press(root, "NewGame");
            Check(difficulty.Selected == 2, "new game preserves side-panel Stockfish strength");
            Check(root.AgainstStockfish, "Stockfish mode selected");
            Play(root, "e2e4");
            await Frames();
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

    private static void Press(Node root, string name) => root.GetNode<Button>(Content + name).EmitSignal(BaseButton.SignalName.Pressed);
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
