using Chess.Core.Model;
using Cheesse.Game.Adapters.Infra;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

public partial class HudView : Control
{
    public event Action? SaveRequested;
    public event Action? MenuRequested;

    private ChessGame? _game;
    private GodotEventBus? _bus;
    private Label _status = null!;
    private Label _mode = null!;
    private Label _message = null!;

    public override void _Ready()
    {
        _status = GetNode<Label>("Status");
        _mode = GetNode<Label>("Mode");
        _message = GetNode<Label>("Message");
        GetNode<Button>("Save").Pressed += () => SaveRequested?.Invoke();
        GetNode<Button>("Menu").Pressed += () => MenuRequested?.Invoke();
    }

    public void Attach(ChessGame game, GodotEventBus bus, bool againstRandom = false)
    {
        if (_bus is not null)
        {
            _bus.CheckRaised -= Refresh;
            _bus.GameEnded -= Refresh;
        }
        _game = game;
        _bus = bus;
        bus.CheckRaised += Refresh;
        bus.GameEnded += Refresh;
        _mode.Text = againstRandom ? "Contra el aleatorio\nJugás con blancas" : "Humano contra humano";
        _message.Text = "";
        Refresh();
    }

    public void SetMessage(string message) => _message.Text = message;

    public void Refresh()
    {
        if (_game is null) return;
        var turn = _game.Turn == PlayerColor.White ? "Blancas" : "Negras";
        var status = _game.Status switch
        {
            GameStatus.Ongoing => "En juego",
            GameStatus.Check => "Jaque",
            GameStatus.Checkmate => "Jaque mate",
            GameStatus.Stalemate => "Tablas por ahogado",
            GameStatus.DrawByFiftyMove => "Tablas por 50 movimientos",
            GameStatus.DrawByRepetition => "Tablas por repetición",
            GameStatus.DrawByInsufficientMaterial => "Material insuficiente",
            _ => _game.Status.ToString(),
        };
        _status.Text = $"{turn} · {status}";
    }
}
