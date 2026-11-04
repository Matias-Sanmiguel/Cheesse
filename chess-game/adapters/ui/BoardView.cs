
using Chess.Core.Model;
using Chess.Core.Rules;
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
    private Texture2D? _picadaLightTile;
    private Texture2D? _picadaDarkTile;
    private double _goalPulseTime;
    private double _goalRedrawClock;
    private Node2D? _picadaVictoryFx;

    // The celebration should be visible before its result dialog covers the board.
    public const double PicadaVictoryEffectDurationSeconds = 1.8;
    private VisualTheme _visualTheme = VisualTheme.Classic;

    public VisualTheme CurrentTheme => _visualTheme;
    public bool ShowsPicadaGoals => _game?.Mode == GameMode.ReyDeLaPicada;
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
        // Dedicated 48x48 pixel-art goals; only used for the four center squares in Picada.
        const string picadaTiles = "res://assets/ui/picada_goals/";
        _picadaLightTile = LoadTextureIfExists(picadaTiles + "light_tile.png");
        _picadaDarkTile = LoadTextureIfExists(picadaTiles + "dark_tile.png");
    }

    public void Attach(ChessGame game, GodotEventBus bus)
    {
        // Evitar suscripciones duplicadas.
        if (_bus is not null)
            _bus.StateChanged -= OnStateChanged;

        ClearPicadaVictoryFx();
        CancelPieceSlide();
        _game = game;
        _bus = bus;
        // Re-entering a loaded or fresh game must show its exact saved position.
        _observedBoard = game.Board;
        _observedHistoryCount = game.History.Count;
        _observedLastMove = game.History.Count == 0 ? null : game.History[^1];
        _goalPulseTime = 0;
        _goalRedrawClock = 0;

        bus.StateChanged += OnStateChanged;

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
            _bus.StateChanged -= OnStateChanged;
        CancelPieceSlide();
    }

    public override void _Process(double delta)
    {
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
                tween.TweenProperty(rook, "position", SquareCenter(rookTo), PieceSlideDuration);
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
        DrawPicadaGoals();
        DrawCheckOverlay();
        DrawPieces();
        DrawSelectionIndicators();
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
