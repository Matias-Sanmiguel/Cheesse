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
    private Label _moves = null!;
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

        PixelUiSkin.ApplySidePanel(this);
        _moves = GetNode<Label>(content + "MovesScroll/MovesText");
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
        bool whiteTurn = _game.Turn == PlayerColor.White;
        _turn.Text = whiteTurn ? "TURNO: BLANCAS" : "TURNO: NEGRAS";
        _status.Text = _game.Status switch
        {
            GameStatus.Ongoing => "En juego",
            GameStatus.Check => "Jaque",
            GameStatus.Checkmate => "Jaque mate",
            GameStatus.Stalemate => "Tablas por ahogado",
            GameStatus.DrawByFiftyMove => "Tablas por 50 movimientos",
            GameStatus.DrawByRepetition => "Tablas por repeticion",
            GameStatus.DrawByInsufficientMaterial => "Tablas por material insuficiente",
            _ => _game.Status.ToString(),
        };
        var turnIcon = _turn.GetNodeOrNull<TextureRect>("TurnIcon");
        if (turnIcon is not null)
            turnIcon.Texture = PixelUiSkin.Texture(whiteTurn ? "hud/turn_white_24x24.png" : "hud/turn_black_24x24.png", 2);
        var statusIcon = _status.GetNodeOrNull<TextureRect>("StatusIcon");
        if (statusIcon is not null)
        {
            string icon = _game.Status switch
            {
                GameStatus.Check => "status_check",
                GameStatus.Checkmate => "status_checkmate",
                GameStatus.Stalemate or GameStatus.DrawByFiftyMove or GameStatus.DrawByRepetition or
                    GameStatus.DrawByInsufficientMaterial => "status_stalemate",
                _ => "status_ongoing"
            };
            statusIcon.Texture = PixelUiSkin.Texture("hud/" + icon + "_24x24.png", 2);
        }

        _undo.Disabled = !_caretaker.CanUndo;
        _redo.Disabled = !_caretaker.CanRedo;
        _load.Disabled = _hasSave?.Invoke() != true;
        _moves.Text = MovesText(_game.History);
    }

    private static string MovesText(IReadOnlyList<Move> history)
    {
        if (history.Count == 0) return "TODAVIA NO HAY JUGADAS";
        var rows = new List<string>();
        int first = Math.Max(0, history.Count - 14);
        first -= first % 2;
        for (int i = first; i < history.Count; i += 2)
        {
            var white = history[i];
            string black = i + 1 < history.Count ? $"    {Print(history[i + 1])}" : "";
            rows.Add($"{i / 2 + 1,2}.  {Print(white)}{black}");
        }
        return string.Join("\n", rows);
    }

    private static string Print(Move move) =>
        $"{(char)('a' + move.From.File)}{move.From.Rank + 1}-{(char)('a' + move.To.File)}{move.To.Rank + 1}";
}