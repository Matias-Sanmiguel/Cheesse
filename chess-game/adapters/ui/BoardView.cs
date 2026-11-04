
using Chess.Core.Model;
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
    private VisualTheme _visualTheme = VisualTheme.Classic;

    public VisualTheme CurrentTheme => _visualTheme;
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
    }

    public void Attach(ChessGame game, GodotEventBus bus)
    {
        // Evitar suscripciones duplicadas.
        if (_bus is not null)
            _bus.StateChanged -= OnStateChanged;

        _game = game;
        _bus = bus;

        bus.StateChanged += OnStateChanged;

        RefreshFromGame();
    }

    public void SetVisualTheme(VisualTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _visualTheme = VisualThemeCatalog.Normalize(theme);

        LoadTextures();
        QueueRedraw();
    }
    public override void _ExitTree()
    {
        if (_bus is not null)
            _bus.StateChanged -= OnStateChanged;
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
        _lastMove = _game is not null && _game.History.Count > 0
            ? _game.History[^1]
            : null;

        ClearSelection();
    }

    // Orden de capas.
    public override void _Draw()
    {
        DrawBoard();
        DrawLastMove();
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

                if (_game.Board.At(square) is not Piece piece)
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

    // Evento integrado en main: movimientos, undo, redo y carga.
    private void OnStateChanged()
    {
        RefreshFromGame();
    }
}
