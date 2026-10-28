using Chess.Core.Model;
using Chess.Core.Services;
using Cheesse.Game.Adapters.Infra;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

public partial class SidePanelView : PanelContainer
{
    public event Action? UndoRequested;
    public event Action? RedoRequested;
    public event Action? SaveRequested;
    public event Action? LoadRequested;
    public event Action? NewGameRequested;
    public event Action? MenuRequested;
    public event Action<bool>? RandomChanged;

    private ChessGame? _game;
    private GameCaretaker? _caretaker;
    private GodotEventBus? _bus;
    private Func<bool>? _hasSave;
    private Label _turn = null!;
    private Label _status = null!;
    private Label _message = null!;
    private Button _undo = null!;
    private Button _redo = null!;
    private Button _load = null!;
    private CheckButton _random = null!;

    public override void _Ready()
    {
        const string content = "Margin/Content/";
        _turn = GetNode<Label>(content + "Turn");
        _status = GetNode<Label>(content + "Status");
        _message = GetNode<Label>(content + "Message");
        _undo = GetNode<Button>(content + "History/Undo");
        _redo = GetNode<Button>(content + "History/Redo");
        _load = GetNode<Button>(content + "Files/Load");
        _random = GetNode<CheckButton>(content + "Random");
        _undo.Pressed += () => UndoRequested?.Invoke();
        _redo.Pressed += () => RedoRequested?.Invoke();
        _load.Pressed += () => LoadRequested?.Invoke();
        GetNode<Button>(content + "Files/Save").Pressed += () => SaveRequested?.Invoke();
        GetNode<Button>(content + "NewGame").Pressed += () => NewGameRequested?.Invoke();
        GetNode<Button>(content + "Menu").Pressed += () => MenuRequested?.Invoke();
        _random.Toggled += enabled => RandomChanged?.Invoke(enabled);
    }

    public void Attach(ChessGame game, GameCaretaker caretaker, GodotEventBus bus,
        bool againstRandom, Func<bool> hasSave)
    {
        if (_bus is not null) _bus.StateChanged -= Refresh;
        _game = game;
        _caretaker = caretaker;
        _bus = bus;
        _hasSave = hasSave;
        _bus.StateChanged += Refresh;
        SetRandomOpponent(againstRandom);
        _message.Text = "";
        Refresh();
    }

    public override void _ExitTree()
    {
        if (_bus is not null) _bus.StateChanged -= Refresh;
    }

    public void SetRandomOpponent(bool enabled) => _random.SetPressedNoSignal(enabled);

    public void SetMessage(string message) => _message.Text = message;

    private void Refresh()
    {
        if (_game is null || _caretaker is null) return;
        _turn.Text = _game.Turn == PlayerColor.White ? "Turno: blancas" : "Turno: negras";
        _status.Text = _game.Status switch
        {
            GameStatus.Ongoing => "En juego",
            GameStatus.Check => "Jaque",
            GameStatus.Checkmate => "Jaque mate",
            GameStatus.Stalemate => "Tablas por ahogado",
            GameStatus.DrawByFiftyMove => "Tablas por 50 movimientos",
            GameStatus.DrawByRepetition => "Tablas por repetici\u00f3n",
            GameStatus.DrawByInsufficientMaterial => "Tablas por material insuficiente",
            _ => _game.Status.ToString(),
        };
        _undo.Disabled = !_caretaker.CanUndo;
        _redo.Disabled = !_caretaker.CanRedo;
        _load.Disabled = _hasSave?.Invoke() != true;
    }
}
