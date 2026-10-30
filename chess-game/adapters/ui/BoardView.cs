
using Chess.Core.Model;
using Cheesse.Game.Adapters.Infra;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

public partial class BoardView : Node2D
{
    // Mantenemos 80 temporalmente.
    // Pasaremos a 96 despues de completar el merge.
    private const int SquareSize = 96;

    private static readonly Color LightSquare = new("eeeed2");
    private static readonly Color DarkSquare = new("769656");

    private ChessGame? _game;
    private GodotEventBus? _bus;

    private Square? _selectedSquare;
    private readonly HashSet<Square> _legalDestinations = new();
    private Move? _lastMove;

    private readonly Dictionary<(PieceType, PlayerColor), Texture2D>
        _pieceTextures = new();

    private Texture2D? _lightTile;
    private Texture2D? _darkTile;

    public override void _Ready()
    {
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        LoadTextures();
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
        if (_lastMove is not Move move)
            return;

        Color color = new(0.78f, 0.58f, 0.25f, 0.35f);

        DrawSquareHighlight(move.From, color);
        DrawSquareHighlight(move.To, color);
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
        // Seleccion: borde bordo.
        if (_selectedSquare is Square selected)
        {
            var rect = new Rect2(
                GetSquarePosition(selected),
                new Vector2(SquareSize, SquareSize)
            );

            DrawRect(rect, new Color("78142C"), false, 4f);
        }

        // Destinos legales.
        foreach (Square destination in _legalDestinations)
        {
            Vector2 center = GetSquarePosition(destination)
                + new Vector2(SquareSize / 2f, SquareSize / 2f);

            if (_game?.Board.At(destination) is Piece)
            {
                // Anillo de captura.
                DrawArc(
                    center,
                    SquareSize * 0.42f,
                    0f,
                    Mathf.Tau,
                    48,
                    new Color("78142C"),
                    5f,
                    true
                );
            }
            else
            {
                // Punto para casilla vacia.
                DrawCircle(
                    center,
                    SquareSize * 0.12f,
                    new Color(0.04f, 0.25f, 0.23f, 0.60f)
                );
            }
        }
    }

    private void DrawCoordinates()
    {
        Font font = ThemeDB.FallbackFont;
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
        _lightTile = LoadTextureIfExists(
            "res://assets/board/light_tile.png");

        _darkTile = LoadTextureIfExists(
            "res://assets/board/dark_tile.png");

        _pieceTextures.Clear();

        foreach (PieceType type in Enum.GetValues<PieceType>())
        {
            foreach (PlayerColor color in Enum.GetValues<PlayerColor>())
            {
                string colorName =
                    color.ToString().ToLowerInvariant();

                string pieceName =
                    type.ToString().ToLowerInvariant();

                string path =
                    $"res://assets/pieces/{colorName}/{pieceName}.png";

                Texture2D? texture = LoadTextureIfExists(path);

                if (texture is not null)
                    _pieceTextures[(type, color)] = texture;
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
