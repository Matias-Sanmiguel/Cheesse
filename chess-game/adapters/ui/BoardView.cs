
using Chess.Core.Events;
using Chess.Core.Model;
using Chess.Core.Rules;
using Chess.Core.Services;
using Cheesse.Game.Adapters.Infra;
using Godot;
using Cheesse.Game.Adapters.Appearance;

namespace Cheesse.Game.Adapters.Ui;

public partial class BoardView : Node2D
{
    private const int SquareSize = 96;

    // Paleta queso del modo clasico: casillas lisas, sin PNGs ni texturas.
    // Los temas personalizados siguen usando sus propios light_tile/dark_tile.
    internal static readonly Color LightSquare = new("e9deb7");
    internal static readonly Color DarkSquare = new("c9aa63");

    private ChessGame? _game;
    private GodotEventBus? _bus;

    private Square? _selectedSquare;
    private Square? _fondueHoverSquare;
    private readonly HashSet<Square> _legalDestinations = new();
    private Move? _lastMove;

    // Visual-only sliding piece animation; the chess board state remains authoritative.
    private const double PieceSlideDuration = 0.20;
    private Board? _observedBoard;
    private Move? _observedLastMove;
    private int _observedHistoryCount;
    private Tween? _pieceSlideTween;
    private readonly List<Sprite2D> _slidingSprites = new();
    private readonly HashSet<Square> _slidingDestinations = new();
    private int _slideSpriteSerial;

    public bool IsMoveAnimating => _slidingSprites.Count > 0;

    private readonly Dictionary<(PieceType, PlayerColor), Texture2D>
        _pieceTextures = new();

    private Texture2D? _lightTile;
    private Texture2D? _darkTile;
    private Texture2D? _selectedOverlay;
    private Texture2D? _lastFromOverlay;
    private Texture2D? _lastToOverlay;
    private Texture2D? _legalMoveOverlay;
    private Texture2D? _captureOverlay;
    private Texture2D? _checkOverlay;
    private Texture2D? _goalOverlay;
    private Texture2D? _holeOverlay;
    private Texture2D? _heatOverlay;
    private Texture2D? _steamTexture;
    private Texture2D? _fondueDropTexture;
    private Texture2D? _picadaLightTile;
    private Texture2D? _picadaDarkTile;
    private double _goalPulseTime;
    private double _goalRedrawClock;
    private double _fondueHeatTime;
    private double _fondueHeatRedrawClock;
    private Square? _fondueCoolingSquare;
    private double _fondueCoolingElapsed;
    private Node2D? _picadaVictoryFx;
    private Node2D? _fondueMeltFx;

    // The celebration should be visible before its result dialog covers the board.
    public const double PicadaVictoryEffectDurationSeconds = 1.8;
    public const double FondueMeltEffectDurationSeconds = 1.45;
    public const double FondueHeatFadeSeconds = 0.82;
    private VisualTheme _visualTheme = VisualTheme.Classic;

    public VisualTheme CurrentTheme => _visualTheme;
    public bool ShowsPicadaGoals => _game?.Mode == GameMode.ReyDeLaPicada;
    public bool ShowsSwissHoles => _game?.Mode == GameMode.QuesoSuizo;
    public bool ShowsFondueMeltFx => _fondueMeltFx is not null && GodotObject.IsInstanceValid(_fondueMeltFx);
    public Square? VisibleHotSquare => _game?.Mode == GameMode.Fondue ? _game.Board.HotSquare : null;
    public Square? CoolingHotSquare => _fondueCoolingSquare;
    internal bool UsesFlatTiles => _lightTile is null && _darkTile is null;

    public override void _Ready()
    {
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        LoadTextures();
        LoadUiOverlays();
    }

    private void LoadUiOverlays()
    {
        const string root = "res://assets/ui/overlays/";
        _selectedOverlay = LoadTextureIfExists(root + "selected_square.png");
        _lastFromOverlay = LoadTextureIfExists(root + "last_move_from.png");
        _lastToOverlay = LoadTextureIfExists(root + "last_move_to.png");
        _legalMoveOverlay = LoadTextureIfExists(root + "legal_move_dot.png");
        _captureOverlay = LoadTextureIfExists(root + "capture_marker.png");
        _checkOverlay = LoadTextureIfExists(root + "check_highlight.png");
        _goalOverlay = LoadTextureIfExists(root + "goal_square.png");
        _holeOverlay = LoadTextureIfExists(root + "hole_overlay.png");
        _heatOverlay = LoadTextureIfExists(root + "heat_overlay.png");
        _steamTexture = LoadTextureIfExists("res://assets/fx/steam_16x16.png");
        _fondueDropTexture = LoadTextureIfExists("res://assets/fx/fondue_drop_16x16.png");
        // Dedicated 48x48 pixel-art goals; only used for the four center squares in Picada.
        const string picadaTiles = "res://assets/ui/picada_goals/";
        _picadaLightTile = LoadTextureIfExists(picadaTiles + "light_tile.png");
        _picadaDarkTile = LoadTextureIfExists(picadaTiles + "dark_tile.png");
    }

    public void Attach(ChessGame game, GodotEventBus bus)
    {
        // Evitar suscripciones duplicadas.
        if (_bus is not null)
        {
            _bus.StateChanged -= OnStateChanged;
            _bus.PiecesMeltedPublished -= OnPiecesMelted;
        }

        ClearPicadaVictoryFx();
        ClearFondueMeltFx();
        CancelPieceSlide();
        _game = game;
        _bus = bus;
        // Re-entering a loaded or fresh game must show its exact saved position.
        _observedBoard = game.Board;
        _observedHistoryCount = game.History.Count;
        _observedLastMove = game.History.Count == 0 ? null : game.History[^1];
        _goalPulseTime = 0;
        _goalRedrawClock = 0;
        _fondueHeatTime = 0;
        _fondueHeatRedrawClock = 0;
        _fondueCoolingSquare = null;
        _fondueCoolingElapsed = 0;
        _fondueHoverSquare = null;

        bus.StateChanged += OnStateChanged;
        bus.PiecesMeltedPublished += OnPiecesMelted;

        RefreshFromGame();
    }

    public void SetVisualTheme(VisualTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        CancelPieceSlide();
        _visualTheme = VisualThemeCatalog.Normalize(theme);

        LoadTextures();
        QueueRedraw();
    }
    public override void _ExitTree()
    {
        if (_bus is not null)
        {
            _bus.StateChanged -= OnStateChanged;
            _bus.PiecesMeltedPublished -= OnPiecesMelted;
        }
        ClearFondueMeltFx();
        _fondueCoolingSquare = null;
        CancelPieceSlide();
    }

    public override void _Process(double delta)
    {
        // Only ask for a repaint when the hovered square changes; no
        // additional input handlers or assumptions about the game engine.
        Square? hover = null;
        if (Visible && _game?.Mode == GameMode.Fondue && _selectedSquare is not null &&
            TryGetSquare(GetLocalMousePosition(), out Square square))
            hover = square;
        if (hover != _fondueHoverSquare)
        {
            _fondueHoverSquare = hover;
            QueueRedraw();
        }

        // Residual heat pulses slowly on the opponent's response turn.
        if (Visible && _game?.Mode == GameMode.Fondue &&
            _game.Board.HotSquare is not null)
        {
            _fondueHeatTime += delta;
            _fondueHeatRedrawClock += delta;
            if (_fondueHeatRedrawClock >= 0.125)
            {
                _fondueHeatRedrawClock %= 0.125;
                QueueRedraw();
            }
        }

        // The board stops being dangerous immediately, but the heat art cools
        // smoothly for a short moment with a fading glow and rising steam.
        if (_fondueCoolingSquare is not null)
        {
            _fondueCoolingElapsed += delta;
            if (_fondueCoolingElapsed >= FondueHeatFadeSeconds)
                _fondueCoolingSquare = null;
            QueueRedraw();
        }

        // Pixel-art pulse at 8 fps, only while a Picada match is active.
        if (!Visible || _game?.Mode != GameMode.ReyDeLaPicada ||
            _game.Status is not (GameStatus.Ongoing or GameStatus.Check))
            return;
        _goalPulseTime += delta;
        _goalRedrawClock += delta;
        if (_goalRedrawClock >= 0.125)
        {
            _goalRedrawClock %= 0.125;
            QueueRedraw();
        }
    }

    /// <summary>
    /// Long, unmistakable visual feedback when the king conquers the central squares.
    /// The particles are independent Godot sprites and animate even after the match ends.
    /// </summary>
    public void CelebratePicadaVictory(Square square)
    {
        if (_game?.Mode != GameMode.ReyDeLaPicada ||
            _game.Status != GameStatus.PicadaVictory)
            return;

        ClearPicadaVictoryFx();
        var sparkle = LoadTextureIfExists("res://assets/fx/sparkle_16x16.png");
        var crumb = LoadTextureIfExists("res://assets/fx/cheese_crumb_8x8.png");
        var crownTexture = LoadTextureIfExists("res://assets/ui/icons/crown_16x16.png");

        Vector2 center = GetSquarePosition(square) + new Vector2(SquareSize / 2f, SquareSize / 2f);
        var effect = new Node2D
        {
            Name = "PicadaVictoryFx",
            Position = center,
            ZIndex = 20,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        AddChild(effect);
        _picadaVictoryFx = effect;

        // A bright enlargement of the same target art already used on the board.
        if (_goalOverlay is not null)
        {
            var burst = new Sprite2D
            {
                Name = "GoalBurst",
                Texture = _goalOverlay,
                Scale = new Vector2(2f, 2f),
                Modulate = new Color(1f, 0.9f, 0.48f, 0.88f)
            };
            effect.AddChild(burst);
            var ringTween = effect.CreateTween().SetParallel(true);
            ringTween.TweenProperty(burst, "scale", new Vector2(4.2f, 4.2f), 0.85)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            ringTween.TweenProperty(burst, "modulate:a", 0f, 0.9f).SetDelay(0.28);
        }

        // The crown rises above the winning king instead of covering the piece.
        if (crownTexture is not null)
        {
            var crown = new Sprite2D
            {
                Name = "WinningCrown",
                Texture = crownTexture,
                Position = new Vector2(0f, -62f),
                Scale = new Vector2(2.5f, 2.5f),
                Modulate = new Color(1f, 1f, 1f, 0f)
            };
            effect.AddChild(crown);
            var crownTween = effect.CreateTween().SetParallel(true);
            crownTween.TweenProperty(crown, "position", new Vector2(0f, -112f), 0.85)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            crownTween.TweenProperty(crown, "scale", new Vector2(4f, 4f), 0.85)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            crownTween.TweenProperty(crown, "modulate:a", 1f, 0.18);
        }

        // The supplied sparkle and cheese pixel-art sprites form a broad burst.
        // They stay bright for a full second before fading, unlike the old 0.44s FX.
        for (int i = 0; i < 24; i++)
        {
            Texture2D? texture = i % 4 == 0 ? crumb ?? sparkle : sparkle ?? crumb;
            if (texture is null) continue;
            float angle = Mathf.Tau * i / 24f;
            float distance = 115f + (i % 5) * 19f;
            var startPosition = new Vector2(Mathf.Cos(angle) * 10f, Mathf.Sin(angle) * 10f);
            var destination = new Vector2(Mathf.Cos(angle) * distance,
                Mathf.Sin(angle) * (distance * 0.82f));
            float initialScale = i % 4 == 0 ? 3f : 2.75f;
            var particle = new Sprite2D
            {
                Name = "PicadaParticle" + i,
                Texture = texture,
                Position = startPosition,
                Scale = Vector2.One * initialScale,
                Modulate = i % 3 == 0 ? new Color("ffe19a") : Colors.White
            };
            effect.AddChild(particle);
            var particleTween = effect.CreateTween().SetParallel(true);
            particleTween.TweenProperty(particle, "position", destination, 1.48)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            particleTween.TweenProperty(particle, "modulate:a", 0f, 0.78).SetDelay(1.00);
            particleTween.TweenProperty(particle, "rotation", (i % 2 == 0 ? 1f : -1f) * 0.55f, 1.48);
        }

        // Text is rendered using the project's font, never baked into the sprites.
        var plaque = PixelUiSkin.MakePanel("VictoryPlaque", "dark",
            new Vector2(-224f, -194f), new Vector2(448f, 58f));
        plaque.MouseFilter = Control.MouseFilterEnum.Ignore;
        plaque.Modulate = new Color(1f, 1f, 1f, 0f);
        var caption = PixelUiSkin.Label("VictoryCaption", "CENTRO CONQUISTADO",
            Vector2.Zero, new Vector2(448f, 58f), 32,
            pixel: true, light: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.Bold);
        caption.AddThemeColorOverride("font_color", PixelUiSkin.Gold);
        plaque.AddChild(caption);
        effect.AddChild(plaque);
        var plaqueTween = effect.CreateTween().SetParallel(true);
        plaqueTween.TweenProperty(plaque, "modulate:a", 1f, 0.3).SetDelay(0.18);
        plaqueTween.TweenProperty(plaque, "position:y", -215f, 0.72)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        // Keep the FX alive until the modal starts; no particles linger after.
        var lifetime = effect.CreateTween();
        lifetime.TweenInterval(PicadaVictoryEffectDurationSeconds + 0.36);
        lifetime.TweenCallback(Callable.From(() =>
        {
            if (ReferenceEquals(_picadaVictoryFx, effect))
                _picadaVictoryFx = null;
            effect.QueueFree();
        }));
    }

    private void ClearPicadaVictoryFx()
    {
        if (_picadaVictoryFx is not null && GodotObject.IsInstanceValid(_picadaVictoryFx))
            _picadaVictoryFx.QueueFree();
        _picadaVictoryFx = null;
    }

    /// <summary>
    /// Render-only feedback for the authoritative Core PiecesMelted event.
    /// The game has already removed the pieces; the view never simulates
    /// captures or changes board state.
    /// </summary>
    private void OnPiecesMelted(PiecesMelted melted)
    {
        if (_game?.Mode != GameMode.Fondue || !Visible) return;

        ClearFondueMeltFx();
        var removedSquares = melted.Pieces.Select(piece => piece.Square).Distinct().ToArray();
        if (removedSquares.Length == 0) return;
        // A single burned piece can die on the hot square before reaching
        // move.To. Multi-piece Fondue captures still originate at move.To.
        Square impact = melted.Pieces.Count == 1 ? melted.Pieces[0].Square : melted.Move.To;

        var meltedTile = LoadTextureIfExists("res://assets/ui/overlays/melted_square.png");
        var steam = _steamTexture;
        var drop = _fondueDropTexture;
        var fx = new Node2D
        {
            Name = "FondueMeltFx",
            ZIndex = 20,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        AddChild(fx);
        _fondueMeltFx = fx;

        // Keep a ghost of each removed piece while it dissolves; the attacker
        // is already animated separately by the regular piece-slide tween.
        var mover = _game.Turn == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
        foreach (var item in melted.Pieces.GroupBy(piece => piece.Square).Select(group => group.First()))
        {
            if (item.Piece.Color == mover &&
                (item.Square == melted.Move.To || melted.Pieces.Count == 1)) continue;
            if (!_pieceTextures.TryGetValue((item.Piece.Type, item.Piece.Color), out var texture)) continue;
            var ghost = new Sprite2D
            {
                Name = "MeltingPiece" + item.Square,
                Texture = texture,
                Position = SquareCenter(item.Square),
                Scale = new Vector2(SquareSize / (float)texture.GetWidth(),
                    SquareSize / (float)texture.GetHeight()),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Modulate = Colors.White
            };
            fx.AddChild(ghost);
            var dissolve = fx.CreateTween();
            dissolve.TweenInterval(0.18);
            dissolve.TweenProperty(ghost, "modulate:a", 0f, 0.44);
        }

        for (int i = 0; i < removedSquares.Length; i++)
        {
            Square square = removedSquares[i];
            float delay = 0.19f + 0.065f * Math.Max(
                Math.Abs(square.File - impact.File),
                Math.Abs(square.Rank - impact.Rank));
            Vector2 center = SquareCenter(square);

            if (meltedTile is not null)
            {
                var overlay = new Sprite2D
                {
                    Name = "MeltSquare" + square,
                    Texture = meltedTile,
                    Position = center,
                    Scale = new Vector2(SquareSize / (float)meltedTile.GetWidth(),
                        SquareSize / (float)meltedTile.GetHeight()),
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                    Modulate = new Color(1f, 1f, 1f, 0f)
                };
                fx.AddChild(overlay);
                var flash = fx.CreateTween();
                flash.TweenInterval(delay);
                flash.TweenProperty(overlay, "modulate:a", 0.95f, 0.13);
                flash.TweenInterval(0.12);
                flash.TweenProperty(overlay, "modulate:a", 0f, 0.40);
            }

            Texture2D? particleTexture = i % 2 == 0 ? steam ?? drop : drop ?? steam;
            if (particleTexture is null) continue;
            var particle = new Sprite2D
            {
                Name = "FondueSteam" + i,
                Texture = particleTexture,
                Position = center + new Vector2((i % 3 - 1) * 11f, -10f),
                Scale = Vector2.One * 2.5f,
                Modulate = new Color(1f, 0.95f, 0.7f, 0f),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest
            };
            fx.AddChild(particle);
            var rise = fx.CreateTween();
            rise.TweenInterval(delay);
            rise.TweenProperty(particle, "modulate:a", 0.96f, 0.12);
            rise.TweenProperty(particle, "position:y", particle.Position.Y - 39f, 0.50)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            rise.Parallel().TweenProperty(particle, "modulate:a", 0f, 0.50);
        }

        var lifetime = fx.CreateTween();
        lifetime.TweenInterval(FondueMeltEffectDurationSeconds);
        lifetime.TweenCallback(Callable.From(() =>
        {
            if (ReferenceEquals(_fondueMeltFx, fx)) _fondueMeltFx = null;
            fx.QueueFree();
        }));
    }

    private void ClearFondueMeltFx()
    {
        if (_fondueMeltFx is not null && GodotObject.IsInstanceValid(_fondueMeltFx))
            _fondueMeltFx.QueueFree();
        _fondueMeltFx = null;
    }

    // Compatibilidad con InputAdapter de main.
    public void SelectSquare(Square? square)
    {
        if (_game is null || square is null)
        {
            ClearSelection();
            return;
        }

        // Consultamos los movimientos del dominio.
        // BoardView no implementa reglas de ajedrez.
        var destinations = _game.LegalMoves()
            .Where(move => move.From == square.Value)
            .Select(move => move.To);

        SetSelection(square, destinations);
    }

    // Nuestra API visual original.
    public void SetSelection(
        Square? selected,
        IEnumerable<Square> legalDestinations)
    {
        _selectedSquare = selected;
        _legalDestinations.Clear();

        if (selected is not null)
        {
            foreach (var destination in legalDestinations)
                _legalDestinations.Add(destination);
        }

        QueueRedraw();
    }

    public void ClearSelection()
    {
        _selectedSquare = null;
        _legalDestinations.Clear();
        QueueRedraw();
    }

    // Compatibilidad con el mouse de main.
    public bool TryGetSquare(
        Vector2 localPosition,
        out Square square)
    {
        square = default;

        if (localPosition.X < 0 ||
            localPosition.Y < 0 ||
            localPosition.X >= SquareSize * 8 ||
            localPosition.Y >= SquareSize * 8)
        {
            return false;
        }

        int file = (int)localPosition.X / SquareSize;
        int rank = 7 - (int)localPosition.Y / SquareSize;

        square = new Square(file, rank);
        return true;
    }

    public void RefreshFromGame()
    {
        if (_game?.Status != GameStatus.PicadaVictory)
            ClearPicadaVictoryFx();
        _lastMove = _game is not null && _game.History.Count > 0
            ? _game.History[^1]
            : null;

        ClearSelection();
    }

    // Slide the existing themed piece sprite across the board, then reveal the
    // already-updated destination. Captures, promotion and castling stay in sync.
    private void BeginPieceSlide(Move move, Piece movingPiece, bool backwards)
    {
        CancelPieceSlide();
        if (!Visible) return;

        Square from = backwards ? move.To : move.From;
        Square to = backwards ? move.From : move.To;
        // The piece animates to its actual melting point, not to a destination
        // it could never reach after crossing the hot square.
        if (!backwards && _observedBoard is Board prior &&
            GameRules.HeatContact(prior, move) is Square hot &&
            GameRules.HeatBurnsMover(prior, move))
            to = hot;
        var movingSprite = MakeSlidingSprite(movingPiece, from);
        if (movingSprite is null) return;

        _slidingDestinations.Add(to);
        var tween = CreateTween().SetParallel(true)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(movingSprite, "position", SquareCenter(to), PieceSlideDuration);

        // In castling, move the rook at the same time as the king.
        if (movingPiece.Type == PieceType.King &&
            move.From.Rank == move.To.Rank && Math.Abs(move.To.File - move.From.File) == 2)
        {
            bool kingside = move.To.File > move.From.File;
            int rank = move.From.Rank;
            Square rookFrom = new(kingside ? 7 : 0, rank);
            Square rookTo = new(kingside ? 5 : 3, rank);
            if (backwards) (rookFrom, rookTo) = (rookTo, rookFrom);

            var rook = MakeSlidingSprite(new Piece(PieceType.Rook, movingPiece.Color), rookFrom);
            if (rook is not null)
            {
                _slidingDestinations.Add(rookTo);
                Square rookVisualTo = rookTo;
                if (!backwards && _observedBoard is Board old &&
                    GameRules.HeatContact(old, move) is Square rookHeat &&
                    !GameRules.HeatBurnsMover(old, move))
                    rookVisualTo = rookHeat;
                tween.TweenProperty(rook, "position", SquareCenter(rookVisualTo), PieceSlideDuration);
            }
        }

        _pieceSlideTween = tween;
        tween.Finished += FinishPieceSlide;
        QueueRedraw();
    }

    private Sprite2D? MakeSlidingSprite(Piece piece, Square source)
    {
        if (!_pieceTextures.TryGetValue((piece.Type, piece.Color), out var texture))
            return null;

        var sprite = new Sprite2D
        {
            Name = "SlidingPiece" + _slideSpriteSerial++,
            Texture = texture,
            Position = SquareCenter(source),
            Scale = new Vector2((float)SquareSize / texture.GetWidth(),
                                (float)SquareSize / texture.GetHeight()),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ZIndex = 10
        };
        AddChild(sprite);
        _slidingSprites.Add(sprite);
        return sprite;
    }

    private static Vector2 SquareCenter(Square square) =>
        GetSquarePosition(square) + new Vector2(SquareSize / 2f, SquareSize / 2f);

    private void CancelPieceSlide()
    {
        _pieceSlideTween?.Kill();
        FinishPieceSlide();
    }

    private void FinishPieceSlide()
    {
        _pieceSlideTween = null;
        foreach (var sprite in _slidingSprites)
        {
            if (!GodotObject.IsInstanceValid(sprite)) continue;
            sprite.Visible = false;
            sprite.QueueFree();
        }
        _slidingSprites.Clear();
        _slidingDestinations.Clear();
        QueueRedraw();
    }

    // Orden de capas.
    public override void _Draw()
    {
        DrawBoard();
        DrawLastMove();
        DrawFondueHeat();
        DrawFondueCooling();
        DrawPicadaGoals();
        DrawSwissHoles();
        DrawCheckOverlay();
        DrawFondueCheckThreats();
        DrawPieces();
        DrawSelectionIndicators();
        DrawFonduePreview();
        DrawCoordinates();
    }

    private void DrawBoard()
    {
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                var square = new Square(file, rank);
                bool dark = (file + rank) % 2 == 0;

                var rect = new Rect2(
                    GetSquarePosition(square),
                    new Vector2(SquareSize, SquareSize)
                );

                Texture2D? tile = dark ? _darkTile : _lightTile;
                if (ShowsPicadaGoals && square.File is 3 or 4 && square.Rank is 3 or 4)
                    tile = (dark ? _picadaDarkTile : _picadaLightTile) ?? tile;

                if (tile is not null)
                    DrawTextureRect(tile, rect, false);
                else
                    DrawRect(rect, dark ? DarkSquare : LightSquare);
            }
        }
    }

    private void DrawLastMove()
    {
        if (_lastMove is not Move move) return;
        DrawOverlay(move.From, _lastFromOverlay, new Color(0.78f, 0.58f, 0.25f, 0.35f));
        DrawOverlay(move.To, _lastToOverlay, new Color(0.78f, 0.58f, 0.25f, 0.35f));
    }

    // Original heat_overlay.png at native pixel-art scaling. The orange
    // glow and steam are purely decorative: the Board alone owns hot timing.
    private void DrawFondueHeat()
    {
        if (VisibleHotSquare is not Square square) return;
        var rect = new Rect2(GetSquarePosition(square), new Vector2(SquareSize, SquareSize));
        float pulse = 0.5f + 0.5f * Mathf.Sin((float)_fondueHeatTime * 6f);
        DrawRect(rect, new Color(1f, 0.35f, 0.06f, 0.11f + 0.09f * pulse));
        if (_heatOverlay is not null)
            DrawTextureRect(_heatOverlay, rect, false,
                new Color(1f, 1f, 1f, 0.70f + 0.27f * pulse));
        DrawRect(rect.Grow(-4f), new Color(1f, 0.40f, 0.12f, 0.66f + 0.27f * pulse),
            false, 3f);

        if (_steamTexture is not null)
        {
            float lift = 9f * pulse;
            var smokeRect = new Rect2(GetSquarePosition(square) +
                new Vector2(SquareSize * 0.33f, -12f - lift),
                new Vector2(SquareSize * 0.34f, SquareSize * 0.34f));
            DrawTextureRect(_steamTexture, smokeRect, false,
                new Color(1f, 0.92f, 0.77f, 0.48f + 0.27f * pulse));
        }
    }

    private void DrawFondueCooling()
    {
        if (_fondueCoolingSquare is not Square square) return;
        float t = Mathf.Clamp((float)(_fondueCoolingElapsed / FondueHeatFadeSeconds), 0f, 1f);
        float fade = (1f - t) * (1f - t);
        Vector2 origin = GetSquarePosition(square);
        float inset = 2f + 13f * t;
        var tile = new Rect2(origin, new Vector2(SquareSize, SquareSize));

        // Last warm glow contracts while the steam lifts and thins out.
        DrawRect(tile.Grow(-inset),
            new Color(1f, 0.34f, 0.08f, 0.15f * fade));
        if (_heatOverlay is not null)
            DrawTextureRect(_heatOverlay, tile.Grow(-inset), false,
                new Color(1f, 1f, 1f, 0.8f * fade));
        if (_steamTexture is not null)
        {
            var smoke = new Rect2(origin +
                new Vector2(SquareSize * 0.33f, -13f - 33f * t),
                new Vector2(SquareSize * 0.34f, SquareSize * 0.34f));
            DrawTextureRect(_steamTexture, smoke, false,
                new Color(1f, 0.92f, 0.75f, 0.74f * fade));
        }
    }

    private void DrawPicadaGoals()
    {
        if (!ShowsPicadaGoals) return;
        float alpha = _game!.Status == GameStatus.PicadaVictory
            ? 1f : 0.85f + 0.15f * Mathf.Sin((float)_goalPulseTime * 2.8f);
        foreach (Square square in PicadaModeRules.GoalSquares)
        {
            var rect = new Rect2(GetSquarePosition(square), new Vector2(SquareSize, SquareSize));
            // A thin breathing outline keeps the target visible without obscuring the tile art.
            bool dark = (square.File + square.Rank) % 2 == 0;
            if ((dark ? _picadaDarkTile : _picadaLightTile) is not null)
                DrawRect(rect.Grow(-3f), new Color(1f, 0.76f, 0.38f, 0.14f + 0.20f * alpha), false, 2f);
            else if (_goalOverlay is not null)
                DrawTextureRect(_goalOverlay, rect, false, new Color(1, 1, 1, alpha));
            else
            {
                DrawRect(rect, new Color(0.97f, 0.73f, 0.16f, 0.13f + 0.11f * alpha));
                DrawRect(rect, new Color(0.96f, 0.66f, 0.11f, alpha), false, 4f);
            }
        }
    }

    // Swiss Cheese holes are painted above the tiles and below all pieces and
    // interactive highlights, using the untouched supplied 48x48 native sprite.
    private void DrawSwissHoles()
    {
        if (!ShowsSwissHoles) return;
        foreach (var square in _game!.Board.SwissHoles)
        {
            var rect = new Rect2(GetSquarePosition(square), new Vector2(SquareSize, SquareSize));
            if (_holeOverlay is not null)
                DrawTextureRect(_holeOverlay, rect, false);
            else
            {
                DrawRect(rect, new Color("aa7739"));
                DrawCircle(rect.GetCenter(), SquareSize * 0.31f, new Color("281a20"));
            }
        }
    }

    // In Fondue a king can be checked by capturing a nearby piece rather
    // than attacking the king directly. Outline those actual capture squares
    // so players can see what they must protect (e.g., f7 in the demo).
    private void DrawFondueCheckThreats()
    {
        if (_game?.Mode != GameMode.Fondue || _game.Status != GameStatus.Check)
            return;

        foreach (var target in GameRules.KingMeltingThreats(_game.Board, _game.Turn)
                     .Select(move => move.To).Distinct())
        {
            var square = new Rect2(GetSquarePosition(target),
                new Vector2(SquareSize, SquareSize)).Grow(-4f);
            DrawRect(square, new Color(1f, 0.36f, 0.21f, 0.95f), false, 4f);
        }
    }

    // Preview precisely the pieces that will disappear on this legal capture.
    // If the target is a pawn, there is no explosion to preview.
    private void DrawFonduePreview()
    {
        if (_game?.Mode != GameMode.Fondue || _selectedSquare is not Square from ||
            _fondueHoverSquare is not Square to || !_legalDestinations.Contains(to))
            return;
        var moves = _game.LegalMoves().Where(candidate =>
            candidate.From == from && candidate.To == to).ToArray();
        if (moves.Length == 0) return;
        if (GameRules.HeatContact(_game.Board, moves[0]) is Square heated)
        {
            // For non-knights, danger can lie anywhere on the path. The
            // marker is on the ACTUAL burn square, even past the cursor.
            var danger = new Rect2(GetSquarePosition(heated),
                new Vector2(SquareSize, SquareSize)).Grow(-8f);
            DrawRect(danger, new Color(0.98f, 0.18f, 0.10f, 0.95f), false, 5f);
            return;
        }
        foreach (Square square in GameRules.MeltingSquares(_game.Board, moves[0]))
        {
            bool king = _game.Board.At(square) is Piece { Type: PieceType.King };
            var rect = new Rect2(GetSquarePosition(square),
                new Vector2(SquareSize, SquareSize)).Grow(-5f);
            var color = king ? new Color(0.95f, 0.23f, 0.23f, 0.95f)
                             : new Color(1f, 0.65f, 0.16f, 0.9f);
            DrawRect(rect, new Color(color.R, color.G, color.B, 0.17f));
            DrawRect(rect, color, false, 4f);
        }
    }

    private void DrawCheckOverlay()
    {
        if (_game is null || _game.Status is not (GameStatus.Check or GameStatus.Checkmate))
            return;

        // The king in check is always the king of the side whose turn it is.
        for (int rank = 0; rank < 8; rank++)
        for (int file = 0; file < 8; file++)
        {
            var square = new Square(file, rank);
            if (_game.Board.At(square) is Piece piece &&
                piece.Type == PieceType.King && piece.Color == _game.Turn)
            {
                DrawOverlay(square, _checkOverlay, new Color(0.9f, 0.1f, 0.2f, 0.4f));
                return;
            }
        }
    }

    private void DrawOverlay(Square square, Texture2D? texture, Color fallback)
    {
        if (texture is not null)
            DrawTextureRect(texture, new Rect2(GetSquarePosition(square), new Vector2(SquareSize, SquareSize)), false);
        else
            DrawSquareHighlight(square, fallback);
    }

    private void DrawPieces()
    {
        if (_game is null)
            return;

        Font font = ThemeDB.FallbackFont;

        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                var square = new Square(file, rank);

                if (_game.Board.At(square) is not Piece piece ||
                    _slidingDestinations.Contains(square))
                    continue;

                if (_pieceTextures.TryGetValue(
                    (piece.Type, piece.Color),
                    out Texture2D? texture))
                {
                    var rect = new Rect2(
                        GetSquarePosition(square),
                        new Vector2(SquareSize, SquareSize)
                    );

                    DrawTextureRect(texture, rect, false);
                }
                else
                {
                    DrawPlaceholderPiece(file, rank, piece, font);
                }
            }
        }
    }

    private void DrawSelectionIndicators()
    {
        if (_selectedSquare is Square selected)
        {
            if (_selectedOverlay is not null)
                DrawTextureRect(_selectedOverlay,
                    new Rect2(GetSquarePosition(selected), new Vector2(SquareSize, SquareSize)), false);
            else
                DrawRect(new Rect2(GetSquarePosition(selected), new Vector2(SquareSize, SquareSize)),
                    new Color("78142C"), false, 4f);
        }

        foreach (Square destination in _legalDestinations)
        {
            Vector2 center = GetSquarePosition(destination) + new Vector2(SquareSize / 2f, SquareSize / 2f);
            if (_game?.Board.At(destination) is Piece)
            {
                if (_captureOverlay is not null)
                    DrawTextureRect(_captureOverlay,
                        new Rect2(GetSquarePosition(destination), new Vector2(SquareSize, SquareSize)), false);
                else
                    DrawArc(center, SquareSize * 0.42f, 0f, Mathf.Tau, 48,
                        new Color("78142C"), 5f, true);
            }
            else if (_legalMoveOverlay is not null)
            {
                Vector2 size = new(_legalMoveOverlay.GetWidth() * 2, _legalMoveOverlay.GetHeight() * 2);
                DrawTextureRect(_legalMoveOverlay, new Rect2(center - size / 2f, size), false);
            }
            else
                DrawCircle(center, SquareSize * 0.12f,
                    new Color(0.04f, 0.25f, 0.23f, 0.60f));
        }
    }

    private void DrawCoordinates()
    {
        Font font = PixelUiSkin.FontFor(UiFontWeight.Medium) ?? ThemeDB.FallbackFont;
        const int fontSize = 16;

        Color color = new("E9E3D6");

        // Letras a-h debajo.
        for (int file = 0; file < 8; file++)
        {
            string label = ((char)('a' + file)).ToString();

            float width = font.GetStringSize(
                label,
                HorizontalAlignment.Left,
                -1,
                fontSize
            ).X;

            float x = file * SquareSize
                + SquareSize / 2f
                - width / 2f;

            float y = 8 * SquareSize + 22f;

            DrawString(
                font,
                new Vector2(x, y),
                label,
                HorizontalAlignment.Left,
                -1,
                fontSize,
                color
            );
        }

        // Numeros 1-8 a la izquierda.
        for (int rank = 0; rank < 8; rank++)
        {
            string label = (rank + 1).ToString();

            float width = font.GetStringSize(
                label,
                HorizontalAlignment.Left,
                -1,
                fontSize
            ).X;

            float x = -12f - width;
            float y = (7 - rank) * SquareSize
                + SquareSize / 2f
                + fontSize * 0.35f;

            DrawString(
                font,
                new Vector2(x, y),
                label,
                HorizontalAlignment.Left,
                -1,
                fontSize,
                color
            );
        }
    }

    private void DrawPlaceholderPiece(
        int file,
        int rank,
        Piece piece,
        Font font)
    {
        var center = new Vector2(
            file * SquareSize + SquareSize / 2f,
            (7 - rank) * SquareSize + SquareSize / 2f
        );

        bool white = piece.Color == PlayerColor.White;

        Color fill = white
            ? new Color("F9F7F5")
            : new Color("1D1D1E");

        Color outline = white
            ? new Color("1D1D1E")
            : new Color("E9E3D6");

        Color textColor = white
            ? new Color("1D1D1E")
            : new Color("F9F7F5");

        DrawCircle(center, SquareSize * 0.375f, outline);
        DrawCircle(center, SquareSize * 0.325f, fill);

        string symbol = piece.Type switch
        {
            PieceType.King => "K",
            PieceType.Queen => "Q",
            PieceType.Rook => "R",
            PieceType.Bishop => "B",
            PieceType.Knight => "N",
            PieceType.Pawn => "P",
            _ => "?"
        };

        int fontSize = (int)(SquareSize * 0.35f);

        float width = font.GetStringSize(
            symbol,
            HorizontalAlignment.Left,
            -1,
            fontSize
        ).X;

        DrawString(
            font,
            new Vector2(
                center.X - width / 2f,
                center.Y + fontSize * 0.32f
            ),
            symbol,
            HorizontalAlignment.Left,
            -1,
            fontSize,
            textColor
        );
    }

    
    private void LoadTextures()
    {
        // En modo clasico ambos campos son null: DrawBoard dibuja los colores
        // lisos definidos arriba. No cargar PNGs de respaldo para este modo.
        // Los tableros de apariencia personalizada mantienen sus texturas.
        string? boardRoot = _visualTheme.BoardSkin is null
            ? null
            : "res://assets/skins/boards/" + _visualTheme.BoardSkin;

        _lightTile = boardRoot is null
            ? null
            : LoadTextureIfExists(boardRoot + "/light_tile.png");

        _darkTile = boardRoot is null
            ? null
            : LoadTextureIfExists(boardRoot + "/dark_tile.png");

        // PIEZAS
        _pieceTextures.Clear();

        foreach (PieceType type in Enum.GetValues<PieceType>())
        {
            foreach (PlayerColor color in Enum.GetValues<PlayerColor>())
            {
                string pieceName =
                    type.ToString().ToLowerInvariant();

                // En los packs las carpetas se llaman White y Black.
                string skinColor = color.ToString();

                string? pack = color == PlayerColor.White
                    ? _visualTheme.WhitePack
                    : _visualTheme.BlackPack;

                Texture2D? texture = null;

                if (pack is not null)
                {
                    string path =
                        $"res://assets/skins/pieces/{pack}/" +
                        $"{skinColor}/{pieceName}.png";

                    texture = LoadTextureIfExists(path);
                }

                // Si no existe el sprite temático, usar el clásico.
                texture ??= LoadTextureIfExists(
                    $"res://assets/pieces/" +
                    $"{skinColor.ToLowerInvariant()}/{pieceName}.png");

                if (texture is not null)
                {
                    _pieceTextures[(type, color)] = texture;
                }
            }
        }
    }


    private static Texture2D? LoadTextureIfExists(string path)
    {
        return ResourceLoader.Exists(path)
            ? GD.Load<Texture2D>(path)
            : null;
    }

    private static Vector2 GetSquarePosition(Square square)
    {
        return new Vector2(
            square.File * SquareSize,
            (7 - square.Rank) * SquareSize
        );
    }

    private void DrawSquareHighlight(Square square, Color color)
    {
        DrawRect(
            new Rect2(
                GetSquarePosition(square),
                new Vector2(SquareSize, SquareSize)
            ),
            color
        );
    }

    // Domain events can refresh the screen more than once per move; animate each
    // committed position transition only once and snap on loads or history jumps.
    private void OnStateChanged()
    {
        if (_game is null) return;

        int count = _game.History.Count;
        bool committedMove = count == _observedHistoryCount + 1 &&
            _observedBoard is not null;
        if (!ReferenceEquals(_observedBoard, _game.Board))
        {
            ClearFondueMeltFx();
            Square? previousHeat = _observedBoard?.Mode == GameMode.Fondue
                ? _observedBoard.HotSquare : null;
            Square? currentHeat = _game.Board.Mode == GameMode.Fondue
                ? _game.Board.HotSquare : null;
            if (committedMove && previousHeat is Square oldHot &&
                previousHeat != currentHeat)
            {
                _fondueCoolingSquare = oldHot;
                _fondueCoolingElapsed = 0;
            }
            else if (!committedMove)
            {
                // Undo, redo, import and new game update immediately and must
                // not resurrect visual heat from an unrelated position.
                _fondueCoolingSquare = null;
                _fondueCoolingElapsed = 0;
            }
        } // A new move, undo, redo or load invalidates the old overlay.
        if (count == _observedHistoryCount + 1 && _observedBoard is not null)
        {
            Move move = _game.History[^1];
            if (_observedBoard.At(move.From) is Piece piece)
                BeginPieceSlide(move, piece, backwards: false);
            else
                CancelPieceSlide();
        }
        else if (count == _observedHistoryCount - 1 && _observedBoard is not null &&
                 _observedLastMove is Move undone)
        {
            if (_observedBoard.At(undone.To) is Piece piece)
                BeginPieceSlide(undone, piece, backwards: true);
            else
                CancelPieceSlide();
        }
        else if (!ReferenceEquals(_observedBoard, _game.Board))
        {
            CancelPieceSlide();
        }

        _observedBoard = _game.Board;
        _observedHistoryCount = count;
        _observedLastMove = count == 0 ? null : _game.History[^1];
        RefreshFromGame();
    }
}
