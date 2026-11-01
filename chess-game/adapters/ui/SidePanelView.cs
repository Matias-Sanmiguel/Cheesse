using System.Globalization;
using System.Text;
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
    public event Action? ImportRequested;
    public event Action? ExportRequested;
    public event Action? ResignRequested;
    public event Action? DrawRequested;
    public event Action? DeclineDrawRequested;
    public event Action? ClaimDrawRequested;
    public event Action<bool>? RandomChanged;
    public event Action<int>? DifficultyChanged;

    private ChessGame? _game;
    private GameCaretaker? _caretaker;
    private GodotEventBus? _bus;
    private Func<bool>? _hasSave;
    private Label _turn = null!, _status = null!, _message = null!, _whiteClock = null!, _blackClock = null!;
    private Button _undo = null!, _redo = null!, _load = null!, _resign = null!, _draw = null!, _decline = null!, _claim = null!;
    private CheckButton _random = null!;
    private OptionButton _difficulty = null!;
    private RichTextLabel _moves = null!;
    private int _renderedCount = -1;
    private bool _againstStockfish, _thinking;

    public override void _Ready()
    {
        const string content = "Margin/Content/";
        _turn = GetNode<Label>(content + "Turn");
        _status = GetNode<Label>(content + "Status");
        _message = GetNode<Label>(content + "Message");
        _whiteClock = GetNode<Label>(content + "Clocks/White");
        _blackClock = GetNode<Label>(content + "Clocks/Black");
        _undo = GetNode<Button>(content + "History/Undo");
        _redo = GetNode<Button>(content + "History/Redo");
        _load = GetNode<Button>(content + "Files/Load");
        _random = GetNode<CheckButton>(content + "Random");
        _difficulty = GetNode<OptionButton>(content + "Difficulty");
        _moves = GetNode<RichTextLabel>(content + "Moves");
        _resign = GetNode<Button>(content + "EndActions/Resign");
        _draw = GetNode<Button>(content + "EndActions/Draw");
        _decline = GetNode<Button>(content + "EndActions/Decline");
        _claim = GetNode<Button>(content + "Claim");
        _undo.Pressed += () => UndoRequested?.Invoke();
        _redo.Pressed += () => RedoRequested?.Invoke();
        _load.Pressed += () => LoadRequested?.Invoke();
        GetNode<Button>(content + "Files/Save").Pressed += () => SaveRequested?.Invoke();
        GetNode<Button>(content + "TextFiles/Import").Pressed += () => ImportRequested?.Invoke();
        GetNode<Button>(content + "TextFiles/Export").Pressed += () => ExportRequested?.Invoke();
        GetNode<Button>(content + "NewGame").Pressed += () => NewGameRequested?.Invoke();
        GetNode<Button>(content + "Menu").Pressed += () => MenuRequested?.Invoke();
        _resign.Pressed += () => ResignRequested?.Invoke();
        _draw.Pressed += () => DrawRequested?.Invoke();
        _decline.Pressed += () => DeclineDrawRequested?.Invoke();
        _claim.Pressed += () => ClaimDrawRequested?.Invoke();
        _random.Toggled += enabled => RandomChanged?.Invoke(enabled);
        foreach (var label in MenuView.EngineLabels) _difficulty.AddItem(label);
        _difficulty.ItemSelected += index => DifficultyChanged?.Invoke(MenuView.EngineElos[(int)index]);
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
        _renderedCount = -1;
        SetRandomOpponent(againstRandom);
        _message.Text = "";
        Refresh();
    }
    public override void _ExitTree() { if (_bus is not null) _bus.StateChanged -= Refresh; }
    public void SetRandomOpponent(bool enabled)
    {
        _againstStockfish = enabled;
        _random.SetPressedNoSignal(enabled);
        _difficulty.Disabled = !enabled;
        Refresh();
    }
    public void SetDifficulty(int elo) => _difficulty.Select(Math.Max(0, Array.IndexOf(MenuView.EngineElos, elo)));
    public void SetThinking(bool thinking) { _thinking = thinking; Refresh(); }
    public void SetMessage(string message) => _message.Text = message;

    public void RefreshClock()
    {
        if (_game is null) return;
        var state = _game.ClockState;
        _whiteClock.Text = "Blancas  " + FormatTime(_game.TimeControl, state.WhiteSeconds);
        _blackClock.Text = "Negras  " + FormatTime(_game.TimeControl, state.BlackSeconds);
        _whiteClock.Modulate = _game.Turn == PlayerColor.White ? Colors.White : new Color(0.72f, 0.8f, 0.76f);
        _blackClock.Modulate = _game.Turn == PlayerColor.Black ? Colors.White : new Color(0.72f, 0.8f, 0.76f);
    }

    private static string FormatTime(TimeControl control, double seconds)
    {
        if (control.InitialSeconds == 0) return "Sin reloj";
        var whole = Math.Max(0, (int)Math.Ceiling(seconds));
        return (whole / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (whole % 60).ToString("00", CultureInfo.InvariantCulture);
    }

    private void Refresh()
    {
        if (_game is null || _caretaker is null) return;
        _turn.Text = _game.Turn == PlayerColor.White ? "Turno: blancas" : "Turno: negras";
        _status.Text = _thinking ? "Stockfish está pensando…" : StatusText(_game.Status);
        _undo.Disabled = !_caretaker.CanUndo;
        _redo.Disabled = !_caretaker.CanRedo;
        _load.Disabled = _hasSave?.Invoke() != true;
        _resign.Disabled = !_game.State.AcceptsMoves;
        var offered = _game.DrawOfferedBy;
        _draw.Text = offered is null ? "Ofrecer tablas" : offered == _game.Turn ? "Tablas ofrecidas" : "Aceptar tablas";
        _draw.Disabled = !_game.State.AcceptsMoves || _againstStockfish || offered == _game.Turn;
        _draw.TooltipText = _againstStockfish ? "Contra Stockfish podés reclamar tablas reglamentarias." : "El rival puede aceptar o rechazar la oferta.";
        _decline.Visible = !_againstStockfish && offered is not null && offered != _game.Turn;
        var canClaim = _game.State.AcceptsMoves && (!_againstStockfish || _game.Turn == PlayerColor.White) &&
            (_game.CanClaimDraw || _game.ClaimableDrawMoves().Count > 0);
        _claim.Disabled = !canClaim;
        _claim.TooltipText = "Triple repetición o 50 movimientos sin capturas ni movimientos de peón.";
        RefreshClock();
        if (_renderedCount != _game.History.Count)
        {
            _renderedCount = _game.History.Count;
            var text = new StringBuilder();
            var board = _game.InitialBoard;
            foreach (var move in _game.History)
            {
                if (board.Turn == PlayerColor.White)
                {
                    if (text.Length > 0) text.Append('\n');
                    text.Append(board.FullmoveNumber).Append(". ");
                }
                else if (text.Length == 0) text.Append(board.FullmoveNumber).Append("... ");
                else text.Append("  ");
                text.Append(SanNotation.ToSan(board, move));
                board = board.WithMove(move);
            }
            _moves.Text = text.Length == 0 ? "Todavía no hay jugadas." : text.ToString();
        }
    }

    public static string StatusText(GameStatus status) => status switch
    {
        GameStatus.Ongoing => "En juego", GameStatus.Check => "Jaque", GameStatus.Checkmate => "Jaque mate",
        GameStatus.Stalemate => "Tablas por ahogado", GameStatus.DrawByFiftyMove => "Tablas por 50 movimientos",
        GameStatus.DrawByRepetition => "Tablas por repetición", GameStatus.DrawByInsufficientMaterial => "Tablas por material insuficiente",
        GameStatus.DrawByFivefoldRepetition => "Tablas por cinco repeticiones", GameStatus.DrawBySeventyFiveMove => "Tablas por 75 movimientos",
        GameStatus.DrawByAgreement => "Tablas por acuerdo", GameStatus.Resigned => "Abandono", GameStatus.Timeout => "Tiempo agotado",
        GameStatus.DrawByTimeout => "Tiempo agotado · tablas", GameStatus.DrawByResignation => "Abandono · tablas", _ => status.ToString(),
    };
}
