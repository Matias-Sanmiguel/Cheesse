using Chess.Core.Model;
using Cheesse.Game.Adapters.Ai;
using Cheesse.Game.Adapters.Input;
using Cheesse.Game.Adapters.Persist;
using Cheesse.Game.Adapters.Ui;
using Godot;

namespace Cheesse.Game.Adapters.Infra;

public partial class GameCompositionRoot : Node
{
    private MinimaxAI? _ai;

    public override void _Ready()
    {
        var bus = new GodotEventBus();
        var clock = new GodotClock();
        AddChild(bus);
        AddChild(clock);

        var game = new ChessGame(bus, new FileSaveRepository(), clock);
        _ai = new MinimaxAI();

        GetNode<BoardView>("Board").Attach(game, bus);
        GetNode<HudView>("Hud").Attach(game, bus);
        GetNode<InputAdapter>("Input").Attach(game);
    }
}
