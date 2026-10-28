using System.IO;
using Chess.Core.Model;
using Chess.Core.Services;
using Cheesse.Game.Adapters.Ai;
using Cheesse.Game.Adapters.Input;
using Cheesse.Game.Adapters.Persist;
using Cheesse.Game.Adapters.Ui;
using Godot;

namespace Cheesse.Game.Adapters.Infra;

public partial class GameCompositionRoot : Node
{
    private readonly FileSaveRepository _repository = new();
    private GodotEventBus _bus = null!;
    private GodotClock _clock = null!;
    private MenuView _menu = null!;
    private BoardView _board = null!;
    private SidePanelView _panel = null!;
    private InputAdapter _input = null!;
    private GameCaretaker? _caretaker;
    private RandomAI? _ai;
    private PromotionDialog _promotionDialog = null!;

    public ChessGame? CurrentGame { get; private set; }
    public bool AgainstRandom => _ai is not null;

    public override void _Ready()
    {
        _bus = new GodotEventBus();
        _clock = new GodotClock();
        AddChild(_bus);
        AddChild(_clock);

        _menu = GetNode<MenuView>("Menu");
        _board = GetNode<BoardView>("Board");
        _panel = GetNode<SidePanelView>("SidePanel");
        _input = GetNode<InputAdapter>("Input");
        _promotionDialog = GetNode<PromotionDialog>("PromotionDialog");
        _menu.HumanRequested += StartHumanGame;
        _menu.RandomRequested += StartRandomGame;
        _menu.ContinueRequested += ContinueGame;
        _panel.UndoRequested += Undo;
        _panel.RedoRequested += Redo;
        _panel.SaveRequested += SaveGame;
        _panel.LoadRequested += LoadGame;
        _panel.NewGameRequested += () => StartNewGame(AgainstRandom);
        _panel.MenuRequested += ShowMenu;
        _panel.RandomChanged += SetRandomOpponent;
        ShowMenu();
    }

    public override void _Process(double delta)
    {
        // Reviewing history must not immediately replay an undone random move.
        if (_menu.Visible || _ai is null || _caretaker?.CanRedo == true ||
            CurrentGame is not { Turn: PlayerColor.Black } game)
            return;
        if (game.Status is GameStatus.Ongoing or GameStatus.Check)
            ApplyMove(_ai.ChooseMove(game));
    }

    public void StartHumanGame() => StartNewGame(false);
    public void StartRandomGame() => StartNewGame(true);

    private void StartNewGame(bool againstRandom)
    {
        _repository.AgainstRandom = againstRandom;
        EnterGame(new ChessGame(_bus, _repository, _clock));
    }

    private void ContinueGame()
    {
        var game = new ChessGame(_bus, _repository, _clock);
        if (!_repository.Load(game))
        {
            _menu.SetContinueAvailable(_repository.HasSave);
            _menu.SetMessage("No se pudo cargar la partida guardada.");
            return;
        }
        EnterGame(game);
    }

    private void EnterGame(ChessGame game)
    {
        CurrentGame = game;
        _caretaker = new GameCaretaker(game);
        _ai = _repository.AgainstRandom ? new RandomAI() : null;
        _board.Attach(game, _bus);
        _panel.Attach(game, _caretaker, _bus, AgainstRandom, () => _repository.HasSave);
        _input.Attach(game, _board, _promotionDialog, ApplyMove, () => !AgainstRandom || game.Turn == PlayerColor.White);
        _menu.Hide();
        _board.Show();
        _panel.Show();
        _bus.NotifyStateChanged();
    }

    private void ApplyMove(Move move)
    {
        if (_caretaker?.Execute(move) == true)
        {
            _panel.SetMessage("");
            _bus.NotifyStateChanged();
        }
    }

    private void Undo()
    {
        if (_caretaker?.Undo() != true) return;
        _input.ClearSelection();
        _panel.SetMessage("");
        _bus.NotifyStateChanged();
    }

    private void Redo()
    {
        if (_caretaker?.Redo() != true) return;
        _input.ClearSelection();
        _panel.SetMessage("");
        _bus.NotifyStateChanged();
    }

    private void SetRandomOpponent(bool enabled)
    {
        _repository.AgainstRandom = enabled;
        _ai = enabled ? new RandomAI() : null;
        _input.ClearSelection();
        _panel.SetRandomOpponent(enabled);
        _bus.NotifyStateChanged();
    }

    public void ShowMenu()
    {
        _input.SetProcessUnhandledInput(false);
        _input.ClearSelection();
        _board.Hide();
        _panel.Hide();
        _menu.SetMessage("");
        _menu.SetContinueAvailable(_repository.HasSave);
        _menu.Show();
        _menu.FocusFirstOption();
    }

    private void SaveGame()
    {
        if (CurrentGame is null) return;
        try
        {
            _repository.Save(CurrentGame);
            _panel.SetMessage("Partida guardada.");
            _bus.NotifyStateChanged();
        }
        catch (IOException)
        {
            _panel.SetMessage("No se pudo guardar la partida.");
        }
    }

    private void LoadGame()
    {
        if (CurrentGame is null) return;
        if (_repository.Load(CurrentGame))
        {
            EnterGame(CurrentGame);
            _panel.SetMessage("Partida cargada.");
        }
        else
        {
            _panel.SetMessage("No se pudo cargar. La partida actual se conserva.");
            _bus.NotifyStateChanged();
        }
    }
}
