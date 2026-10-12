using Chess.Core.Model;
using Cheesse.Game.Adapters.Infra;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

public partial class HudView : Control
{
    private ChessGame? _game;
    private Label? _status;

    public override void _Ready()
    {
        _status = GetNode<Label>("Status");
    }

    public void Attach(ChessGame game, GodotEventBus bus)
    {
        _game = game;
        bus.CheckRaised += OnStateChanged;
        bus.GameEnded += OnStateChanged;
        Refresh();
    }

    private void OnStateChanged() => Refresh();

    private void Refresh()
    {
        if (_status is null || _game is null)
            return;

        _status.Text = $"{_game.Turn} · {_game.Status}";
    }
}
