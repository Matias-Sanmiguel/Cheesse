using System.IO;
using Chess.Core.Model;
using Chess.Core.Ports;
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
    private PromotionDialog _promotionDialog = null!;
    private GameOverOverlay _gameOver = null!;
    private ConfirmationDialog _resignDialog = null!, _claimDialog = null!;
    private FileDialog _importDialog = null!, _exportDialog = null!;
    private CancellationTokenSource? _searchCancellation;
    private int _searchGeneration;
    private int _gameOverRevision;
    private Board? _picadaResultBoard;
    private bool _engineEnabled, _engineFailed;
    private Move? _announcedClaim;
    private PlayerColor _resigningSide;
    public ChessGame? CurrentGame { get; private set; }
    public bool AgainstStockfish => _engineEnabled;
    public bool AgainstRandom => AgainstStockfish;
    public bool IsThinking { get; private set; }
    private bool HasModal => _promotionDialog.Visible || _resignDialog.Visible || _claimDialog.Visible || _importDialog.Visible || _exportDialog.Visible;

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
        _gameOver = GetNode<GameOverOverlay>("GameOverOverlay");
        CreateDialogs();
        _menu.HumanRequested += StartHumanGame;
        _menu.RandomRequested += StartStockfishGame;
        _menu.ContinueRequested += ContinueGame;
        _panel.UndoRequested += Undo;
        _panel.RedoRequested += Redo;
        _panel.SaveRequested += SaveGame;
        _panel.LoadRequested += LoadGame;
        _panel.NewGameRequested += () => StartNewGame(AgainstStockfish);
        _panel.MenuRequested += ShowMenu;
        _panel.RandomChanged += SetStockfishOpponent;
        _panel.DifficultyChanged += ChangeDifficulty;
        _panel.ImportRequested += OpenImport;
        _panel.ExportRequested += OpenExport;
        _panel.ResignRequested += RequestResignation;
        _panel.DrawRequested += OfferOrAcceptDraw;
        _panel.DeclineDrawRequested += DeclineDraw;
        _panel.ClaimDrawRequested += ClaimDraw;
        _gameOver.RematchRequested += () => StartNewGame(AgainstStockfish);
        _gameOver.MainMenuRequested += ShowMenu;
        _bus.StateChanged += UpdateGameOver;
        ShowMenu();
    }

    public override void _Process(double delta)
    {
        if (CurrentGame is not { } game) return;
        if (_menu.Visible || HasModal || game.Caretaker.CanRedo || _engineFailed)
        {
            game.PauseClock();
            _ingame.RefreshClocks();
            return;
        }
        game.ResumeClock();
        game.Tick();
        _ingame.RefreshClocks();
        if (!game.State.AcceptsMoves)
        {
            if (IsThinking) CancelSearch();
            UpdateGameOver();
            return;
        }
        if (AgainstStockfish && !_engineFailed && !IsThinking && !_board.IsMoveAnimating && game.Turn == PlayerColor.Black)
            RequestComputerMove();
    }

    public override void _Input(InputEvent input)
    {
        if (_menu.Visible || HasModal) return;
        if (input is InputEventKey { Pressed: true, Echo: false, CtrlPressed: true } key &&
            (key.Keycode == Key.Z || key.PhysicalKeycode == Key.Z))
        {
            Undo();
            GetViewport().SetInputAsHandled();
        }
    }

    public void StartHumanGame() => StartNewGame(false, true);
    public void StartStockfishGame() => StartNewGame(true, true);
    public void StartRandomGame() => StartStockfishGame();

    private void StartNewGame(bool againstStockfish, bool fromMenu = false)
    {
        if (fromMenu || CurrentGame is null) _repository.Theme = _menu.CreateVisualTheme();
        var mode = fromMenu || CurrentGame is null ? _menu.SelectedMode : CurrentGame.Mode;
        var game = new ChessGame(_bus, _repository, _clock, mode);
        againstStockfish &= mode == GameMode.Standard;
        game.ConfigureClock(fromMenu || CurrentGame is null ? _menu.SelectedTimeControl : CurrentGame.TimeControl);
        _repository.AgainstStockfish = againstStockfish;
        if (fromMenu || CurrentGame is null) _repository.EngineElo = _menu.SelectedEngineElo;
        EnterGame(game);
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
        CancelSearch();
        CurrentGame?.PauseClock();
        CurrentGame = game;
        _engineEnabled = _repository.AgainstStockfish;
        _engineFailed = false;
        _board.Attach(game, _bus);
        _board.SetVisualTheme(_repository.Theme);
        _ingame.Attach(game);
        _panel.Attach(game, game.Caretaker, _bus, AgainstStockfish, () => _repository.HasSave);
        _panel.SetDifficulty(_repository.EngineElo);
        _input.Attach(game, _board, _promotionDialog, ApplyMove,
            () => game.State.AcceptsMoves && !HasModal && !IsThinking && (!AgainstStockfish || game.Turn == PlayerColor.White));
        _menu.Hide();
        _ingame.Show();
        _ingame.ShowUtilities(true);
        _board.Show();
        _panel.Show();
        game.ResumeClock();
        _bus.NotifyStateChanged();
        UpdateGameOver();
    }

    private async void RequestComputerMove()
    {
        if (CurrentGame is not { } game) return;
        if (game.Board.Mode != GameMode.Standard)
        {
            SetStockfishOpponent(false);
            _panel.SetMessage("Stockfish estándar no admite este modo. Podés seguir entre humanos.");
            return;
        }
        if (game.CanClaimDraw && game.ClaimDraw(PlayerColor.Black)) return;
        var generation = ++_searchGeneration;
        var expectedBoard = game.Board;
        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        IsThinking = true;
        _panel.SetThinking(true);
        try
        {
            var remaining = game.TimeControl.InitialSeconds == 0 ? 500 : Math.Clamp((int)(game.ClockState.BlackSeconds * 1000 / 30), 20, 700);
            var ai = new StockfishAI(StockfishPath(), _repository.EngineElo, remaining);
            var move = await ai.ChooseMoveAsync(game, cancellation.Token);
            if (generation != _searchGeneration || !ReferenceEquals(CurrentGame, game) ||
                !ReferenceEquals(game.Board, expectedBoard) || !game.State.AcceptsMoves || !AgainstStockfish || _menu.Visible)
                return;
            IsThinking = false;
            _panel.SetThinking(false);
            if (!game.ClaimDraw(PlayerColor.Black, move)) ApplyMove(move);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is IOException or InvalidOperationException or TimeoutException or System.ComponentModel.Win32Exception)
        {
            if (generation == _searchGeneration && ReferenceEquals(CurrentGame, game))
            {
                _engineFailed = true;
                game.PauseClock();
                _panel.SetMessage("Stockfish no pudo responder. Desactivá el motor para seguir entre humanos o volvé a intentar.");
                GD.PushWarning("Stockfish: " + error.Message);
            }
        }
        finally
        {
            if (generation == _searchGeneration)
            {
                IsThinking = false;
                _searchCancellation = null;
                _panel.SetThinking(false);
            }
            cancellation.Dispose();
        }
    }

    private static string StockfishPath()
    {
        var configured = System.Environment.GetEnvironmentVariable("CHEESSE_STOCKFISH_PATH");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        var name = OperatingSystem.IsWindows() ? "stockfish.exe" : "stockfish";
        var packaged = Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath())!, "engines", name);
        if (File.Exists(packaged)) return packaged;
        return ProjectSettings.GlobalizePath("res://engines/" + name);
    }

    private void CancelSearch()
    {
        _searchGeneration++;
        _searchCancellation?.Cancel();
        _searchCancellation = null;
        IsThinking = false;
        if (_panel is not null && _panel.IsNodeReady()) _panel.SetThinking(false);
    }

    private void ApplyMove(Move move)
    {
        if (CurrentGame is not { } game || HasModal) return;
        game.ResumeClock();
        if (game.Caretaker.Execute(move))
        {
            _panel.SetMessage("");
            _bus.NotifyStateChanged();
        }
    }

    private void Undo()
    {
        CancelSearch();
        if (CurrentGame?.Caretaker.Undo() != true) return;
        CurrentGame.PauseClock();
        _input.ClearSelection();
        _panel.SetMessage("Historial en revisión. Rehacé o jugá una alternativa para seguir.");
        _bus.NotifyStateChanged();
    }

    private void Redo()
    {
        CancelSearch();
        if (CurrentGame?.Caretaker.Redo() != true) return;
        _input.ClearSelection();
        _panel.SetMessage("");
        if (!CurrentGame.Caretaker.CanRedo) CurrentGame.ResumeClock();
        _bus.NotifyStateChanged();
    }

    private void SetStockfishOpponent(bool enabled)
    {
        CancelSearch();
        _repository.AgainstStockfish = _engineEnabled = enabled;
        _engineFailed = false;
        _input.ClearSelection();
        _panel.SetRandomOpponent(enabled);
        _bus.NotifyStateChanged();
    }

    private void ChangeDifficulty(int elo)
    {
        CancelSearch();
        _repository.EngineElo = elo;
        _engineFailed = false;
        _panel.SetMessage("Nivel de Stockfish actualizado.");
    }

    public void ShowMenu()
    {
        _gameOverRevision++;
        _picadaResultBoard = null;
        CancelSearch();
        CurrentGame?.PauseClock();
        _gameOver.Hide();
        _resignDialog.Hide();
        _claimDialog.Hide();
        _importDialog.Hide();
        _exportDialog.Hide();
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
            _panel.SetMessage("Partida y relojes guardados.");
            _bus.NotifyStateChanged();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { _panel.SetMessage("No se pudo guardar la partida. Revisá el espacio y los permisos."); }
    }

    private void LoadGame()
    {
        if (CurrentGame is null) return;
        if (_repository.Load(CurrentGame))
        {
            EnterGame(CurrentGame);
            _panel.SetMessage("Partida y relojes recuperados.");
        }
        else _panel.SetMessage("No se pudo cargar. La partida actual se conserva.");
        _bus.NotifyStateChanged();
    }

    private void RequestResignation()
    {
        if (CurrentGame?.State.AcceptsMoves != true) return;
        CancelSearch();
        _resigningSide = AgainstStockfish ? PlayerColor.White : CurrentGame.Turn;
        _resignDialog.DialogText = (_resigningSide == PlayerColor.White ? "Las blancas" : "Las negras") + " abandonan la partida. ¿Confirmás?";
        _resignDialog.PopupCentered();
    }

    private void OfferOrAcceptDraw()
    {
        if (CurrentGame is not { } game || AgainstStockfish) return;
        if (game.DrawOfferedBy is PlayerColor side && side != game.Turn) game.AcceptDraw(game.Turn);
        else if (game.OfferDraw(game.Turn)) _panel.SetMessage("Tablas ofrecidas. El rival puede aceptarlas o seguir jugando.");
        _bus.NotifyStateChanged();
    }

    private void DeclineDraw()
    {
        if (CurrentGame is not { } game) return;
        game.DeclineDraw(game.Turn);
        _panel.SetMessage("Oferta de tablas rechazada.");
        _bus.NotifyStateChanged();
    }

    private void ClaimDraw()
    {
        if (CurrentGame is not { } game || (AgainstStockfish && game.Turn != PlayerColor.White)) return;
        if (game.CanClaimDraw) { game.ClaimDraw(game.Turn); _bus.NotifyStateChanged(); return; }
        var moves = game.ClaimableDrawMoves();
        if (moves.Count == 0) return;
        CancelSearch();
        _announcedClaim = moves[0];
        _claimDialog.DialogText = "Reclamar tablas anunciando " + SanNotation.ToSan(game.Board, moves[0]) +
            ".\nLa partida termina sin ejecutar esa jugada.";
        _claimDialog.PopupCentered();
    }

    private void CreateDialogs()
    {
        _resignDialog = new ConfirmationDialog { Name = "ResignDialog", Title = "Abandonar partida", OkButtonText = "Abandonar", CancelButtonText = "Seguir jugando" };
        _resignDialog.Confirmed += () => { _resignDialog.Hide(); CurrentGame?.Resign(_resigningSide); _bus.NotifyStateChanged(); };
        AddChild(_resignDialog);
        _claimDialog = new ConfirmationDialog { Name = "DrawClaimDialog", Title = "Reclamar tablas", OkButtonText = "Reclamar", CancelButtonText = "Cancelar" };
        _claimDialog.Confirmed += () => { _claimDialog.Hide(); if (CurrentGame is { } game) game.ClaimDraw(game.Turn, _announcedClaim); _bus.NotifyStateChanged(); };
        AddChild(_claimDialog);
        _importDialog = new FileDialog { Name = "ImportDialog", Title = "Importar posición o partida", Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenFile, Filters = ["*.fen ; Posición FEN", "*.pgn ; Partida PGN"], Size = new Vector2I(850, 560) };
        _importDialog.FileSelected += path =>
        {
            _importDialog.Hide();
            try { ImportGameText(GameTextFiles.Read(path), Path.GetExtension(path)); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
            { _panel.SetMessage("No se pudo leer el archivo. La partida actual se conserva."); }
        };
        AddChild(_importDialog);
        _exportDialog = new FileDialog { Name = "ExportDialog", Title = "Exportar posición o partida", Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.SaveFile, Filters = ["*.pgn ; Partida PGN", "*.fen ; Posición FEN"], Size = new Vector2I(850, 560) };
        _exportDialog.FileSelected += path =>
        {
            _exportDialog.Hide();
            try
            {
                if (string.IsNullOrEmpty(Path.GetExtension(path))) path += ".pgn";
                GameTextFiles.Write(path, ExportGameText(Path.GetExtension(path)));
                _panel.SetMessage("Archivo exportado.");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
            { _panel.SetMessage("No se pudo exportar. Revisá la ubicación y los permisos."); }
        };
        AddChild(_exportDialog);
    }

    private void OpenImport()
    {
        CancelSearch();
        CurrentGame?.PauseClock();
        _input.ClearSelection();
        _importDialog.PopupCentered();
    }
    private void OpenExport()
    {
        CancelSearch();
        CurrentGame?.PauseClock();
        _input.ClearSelection();
        _exportDialog.CurrentFile = "partida.pgn";
        _exportDialog.PopupCentered();
    }

    public bool ImportGameText(string text, string extension)
    {
        try
        {
            var imported = GameTextFiles.Import(text, extension, new SilentEvents(), _repository, _clock);
            var live = new ChessGame(_bus, _repository, _clock, imported.InitialBoard);
            live.RestoreFrom(imported);
            _repository.AgainstStockfish = false;
            EnterGame(live);
            _panel.SetMessage("Importado en modo humano, sin reloj.");
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException)
        { _panel.SetMessage("FEN o PGN inválido. La partida actual se conserva."); return false; }
    }
    public string ExportGameText(string extension) => GameTextFiles.Export(CurrentGame ?? throw new InvalidOperationException("No hay una partida abierta."), extension);

    private sealed class SilentEvents : IEventPublisher { public void Publish<T>(T domainEvent) { } }
    private async void UpdateGameOver()
    {
        var game = CurrentGame;
        if (game is null || game.State.AcceptsMoves || _menu.Visible)
        {
            _gameOverRevision++;
            _picadaResultBoard = null;
            _gameOver.Hide();
            return;
        }
        if (game.Status == GameStatus.PicadaVictory)
        {
            // Several domain events can describe the same finished move.
            if (ReferenceEquals(_picadaResultBoard, game.Board)) return;
            _picadaResultBoard = game.Board;
            int revision = ++_gameOverRevision;
            _gameOver.Hide();
            if (game.History.Count > 0) _board.CelebratePicadaVictory(game.History[^1].To);
            await ToSignal(GetTree().CreateTimer(BoardView.PicadaVictoryEffectDurationSeconds + 0.25), SceneTreeTimer.SignalName.Timeout);
            if (!GodotObject.IsInstanceValid(this) || revision != _gameOverRevision ||
                !ReferenceEquals(CurrentGame, game) || _menu.Visible ||
                game.Status != GameStatus.PicadaVictory) return;
        }
        else
        {
            _gameOverRevision++;
            _picadaResultBoard = null;
        }
        _gameOver.ShowResult(game, _ingame.TotalElapsedSeconds);
    }
    public override void _ExitTree() { _gameOverRevision++; CancelSearch(); CurrentGame?.PauseClock(); }
}
