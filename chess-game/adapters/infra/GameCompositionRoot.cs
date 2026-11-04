using System.IO;
using Chess.Core.Model;
using Chess.Core.Services;
using Cheesse.Game.Adapters.Ai;
using Cheesse.Game.Adapters.Input;
using Cheesse.Game.Adapters.Persist;
using Cheesse.Game.Adapters.Ui;
using Godot;
using Cheesse.Game.Adapters.Appearance;

namespace Cheesse.Game.Adapters.Infra;

public partial class GameCompositionRoot : Node
{
    private readonly FileSaveRepository _repository = new();
    private GodotEventBus _bus = null!;
    private GodotClock _clock = null!;
    private MenuView _menu = null!;
    private BoardView _board = null!;
    private SidePanelView _panel = null!;
    private InGamePresentationView _ingame = null!;
    private InputAdapter _input = null!;
    private GameCaretaker? _caretaker;
    private RandomAI? _ai;
    private PromotionDialog _promotionDialog = null!;
    private GameOverOverlay _gameOver = null!;

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

        // Background, frame, player cards and utilities live behind BoardView.
        // BoardView itself and all chess rules remain unchanged.
        _ingame = new InGamePresentationView
        {
            Name = "InGamePresentation",
            Visible = false
        };
        AddChild(_ingame);
        MoveChild(_ingame, 0);
        _ingame.Configure(_board, _panel, _menu);
        // Utility popups are above the sidebar but below the main menu/game-over.
        _ingame.InstallForeground(this, _menu);

        _input = GetNode<InputAdapter>("Input");
        _promotionDialog = GetNode<PromotionDialog>("PromotionDialog");
        _menu.HumanRequested += StartHumanGame;
        _menu.RandomRequested += StartRandomGame;
        _menu.ContinueRequested += ContinueGame;
        _panel.UndoRequested += Undo;
        _panel.RedoRequested += Redo;
        _panel.SaveRequested += SaveGame;
        _panel.LoadRequested += LoadGame;
        _panel.NewGameRequested +=
            () => StartNewGame(AgainstRandom, _repository.Theme, CurrentGame?.Mode ?? GameMode.Classic);
        _panel.MenuRequested += ShowMenu;
        _panel.RandomChanged += SetRandomOpponent;
        _gameOver = GetNode<GameOverOverlay>("GameOverOverlay");
        _gameOver.RematchRequested += () => StartNewGame(
            AgainstRandom, _repository.Theme, CurrentGame?.Mode ?? GameMode.Classic);
        _gameOver.MainMenuRequested += ShowMenu;
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

    public override void _Input(InputEvent input)
    {
        if (_menu.Visible || _promotionDialog.Visible) return;
        if (input is InputEventKey { Pressed: true, Echo: false, CtrlPressed: true } key &&
            (key.Keycode == Key.Z || key.PhysicalKeycode == Key.Z))
        {
            Undo();
            GetViewport().SetInputAsHandled();
        }
    }

    
    public void StartHumanGame()
    {
        StartNewGame(false, _menu.CreateVisualTheme(), _menu.SelectedMode);
    }

    public void StartRandomGame()
    {
        StartNewGame(true, _menu.CreateVisualTheme(), _menu.SelectedMode);
    }

    private void StartNewGame(
        bool againstRandom,
        VisualTheme theme,
        GameMode mode)
    {
        _repository.AgainstRandom = againstRandom;
        _repository.Theme = VisualThemeCatalog.Normalize(theme);

        EnterGame(new ChessGame(
            _bus,
            _repository,
            _clock,
            mode
        ));
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
        _caretaker = game.Caretaker;
        _ai = _repository.AgainstRandom ? new RandomAI() : null;
        _board.Attach(game, _bus);
        _board.SetVisualTheme(_repository.Theme);   
        _panel.Attach(game, _caretaker, _bus, AgainstRandom, () => _repository.HasSave);
        _ingame.Attach(game);
        _input.Attach(game, _board, _promotionDialog, ApplyMove, () => game.State.AcceptsMoves && !_gameOver.Visible && (!AgainstRandom || game.Turn == PlayerColor.White));
        _menu.Hide();
        _ingame.Show();
        _ingame.ShowUtilities(true);
        _board.Show();
        _panel.Show();
        _bus.NotifyStateChanged();
        UpdateGameOver();
    }

    private void ApplyMove(Move move)
    {
        if (_caretaker?.Execute(move) == true)
        {
            _panel.SetMessage("");
            _bus.NotifyStateChanged();
            if (CurrentGame is { Status: GameStatus.PicadaVictory } winner &&
                winner.History.Count > 0)
                _board.CelebratePicadaVictory(winner.History[^1].To);
            UpdateGameOver();
        }
    }

    private void Undo()
    {
        if (_caretaker?.Undo() != true) return;
        _input.ClearSelection();
        _panel.SetMessage("");
        _bus.NotifyStateChanged();
        UpdateGameOver();
    }

    private void Redo()
    {
        if (_caretaker?.Redo() != true) return;
        _input.ClearSelection();
        _panel.SetMessage("");
        _bus.NotifyStateChanged();
        UpdateGameOver();
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
        _gameOver.Hide();
        _input.SetProcessUnhandledInput(false);
        _input.ClearSelection();
        _board.Hide();
        _panel.Hide();
        _ingame.Hide();
        _ingame.ShowUtilities(false);
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

    private void UpdateGameOver()
    {
        if (CurrentGame is null || CurrentGame.Status is GameStatus.Ongoing or GameStatus.Check)
        {
            _gameOver.Hide();
            return;
        }

        _gameOver.ShowResult(CurrentGame);
    }
}
