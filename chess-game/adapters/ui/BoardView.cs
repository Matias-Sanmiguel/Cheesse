
using Chess.Core.Model;
using Cheesse.Game.Adapters.Infra;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

public partial class BoardView : Node2D
{
    private const int SquareSize = 80;

    private static readonly Color LightSquare = new("eeeed2");
    private static readonly Color DarkSquare = new("769656");

    private ChessGame? _game;
    private Square? _selectedSquare;
    private readonly HashSet<Square> _legalDestinations = new();
    private Move? _lastMove;
    private readonly Dictionary<(PieceType, PlayerColor), Texture2D>
        _pieceTextures = new();

    private Texture2D? _lightTile;
    private Texture2D? _darkTile;

    public void Attach(ChessGame game, GodotEventBus bus)
    {
        _game = game;
        bus.PieceMoved += OnPieceMoved;
        QueueRedraw();
    }

    public void SetSelection(
        Square? selected,
        IEnumerable<Square> legalDestinations)
    {
        _selectedSquare = selected;

        _legalDestinations.Clear();

        foreach (var square in legalDestinations)
            _legalDestinations.Add(square);

        QueueRedraw();
    }

    public void ClearSelection()
    {
        _selectedSquare = null;
        _legalDestinations.Clear();
        QueueRedraw();
    }

    public void RefreshFromGame()
    {
        if (_game is not null && _game.History.Count > 0)
            _lastMove = _game.History[^1];
        else
            _lastMove = null;

        ClearSelection();
    }

    public override void _Draw()
    {
        DrawBoard();
        DrawIndicators();
        DrawPieces();
        DrawCoordinates();
    }

    
    private void DrawBoard()
    {
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                // Determinamos si la casilla es clara u oscura.
                // a1 debe ser oscura.
                bool dark = (file + rank) % 2 == 0;

                // Calculamos la posicion y el tamaño.
                var rect = new Rect2(
                    file * SquareSize,
                    (7 - rank) * SquareSize,
                    SquareSize,
                    SquareSize
                );

                // Buscamos la textura correspondiente.
                Texture2D? tile = dark
                    ? _darkTile
                    : _lightTile;

                if (tile is not null)
                {
                    // Si existe un PNG, dibujamos la imagen.
                    DrawTextureRect(
                        tile,
                        rect,
                        false
                    );
                }
                else
                {
                    // Si no existe PNG, usamos el color original.
                    Color color = dark
                        ? DarkSquare
                        : LightSquare;

                    DrawRect(rect, color);
                }
            }
        }
    }


    
    private void DrawCoordinates()
    {
        Font font = ThemeDB.FallbackFont;
        const int fontSize = 16;

        Color labelColor = new("E9E3D6");

        // Letras a-h debajo del tablero.
        for (int file = 0; file < 8; file++)
        {
            string label = ((char)('a' + file)).ToString();

            float textWidth = font.GetStringSize(
                label,
                HorizontalAlignment.Left,
                -1,
                fontSize
            ).X;

            float x = file * SquareSize
                + SquareSize / 2f
                - textWidth / 2f;

            float y = 8 * SquareSize + 22f;

            DrawString(
                font,
                new Vector2(x, y),
                label,
                HorizontalAlignment.Left,
                -1,
                fontSize,
                labelColor
            );
        }

        // Numeros 1-8 a la izquierda del tablero.
        for (int rank = 0; rank < 8; rank++)
        {
            string label = (rank + 1).ToString();

            float textWidth = font.GetStringSize(
                label,
                HorizontalAlignment.Left,
                -1,
                fontSize
            ).X;

            float x = -12f - textWidth;

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
                labelColor
            );
        }
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
                // Obtenemos la pieza de esta casilla.
                var square = new Square(file, rank);
                Piece? piece = _game.Board.At(square);

                // Si no hay pieza, seguimos.
                if (piece is not Piece value)
                    continue;

                // Buscamos si existe un PNG para esta pieza.
                if (_pieceTextures.TryGetValue(
                    (value.Type, value.Color),
                    out Texture2D? texture))
                {
                    // Posicion y tamaño de la imagen.
                    var rect = new Rect2(
                        GetSquarePosition(square),
                        new Vector2(SquareSize, SquareSize)
                    );

                    // Dibujamos el PNG.
                    DrawTextureRect(texture, rect, false);
                }
                else
                {
                    // Si no existe PNG, usamos el circulo con letra.
                    DrawPlaceholderPiece(
                        file,
                        rank,
                        value,
                        font
                    );
                }
            }
        }
    }


    
    private void DrawIndicators()
    {
        // Ultimo movimiento: resaltado dorado.
        if (_lastMove is Move last)
        {
            Color lastMoveColor =
                new Color(0.78f, 0.58f, 0.25f, 0.35f);

            DrawSquareHighlight(last.From, lastMoveColor);
            DrawSquareHighlight(last.To, lastMoveColor);
        }

        // Casilla seleccionada: borde bordó.
        if (_selectedSquare is Square selected)
        {
            var rect = new Rect2(
                GetSquarePosition(selected),
                new Vector2(SquareSize, SquareSize)
            );

            DrawRect(
                rect,
                new Color("78142C"),
                false,
                4f
            );
        }

        // Destinos legales.
        foreach (Square destination in _legalDestinations)
        {
            Vector2 center =
                GetSquarePosition(destination)
                + new Vector2(SquareSize / 2f, SquareSize / 2f);

            // Si hay una pieza, mostramos un anillo de captura.
            if (_game?.Board.At(destination) is Piece)
            {
                DrawArc(
                    center,
                    34f,
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
                // Casilla vacia: punto central.
                DrawCircle(
                    center,
                    10f,
                    new Color(0.04f, 0.25f, 0.23f, 0.60f)
                );
            }
        }
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

        // Representacion temporal de cada pieza.
        DrawCircle(center, 30f, outline);
        DrawCircle(center, 26f, fill);

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

        const int fontSize = 28;

        float textWidth = font.GetStringSize(
            symbol,
            HorizontalAlignment.Left,
            -1,
            fontSize
        ).X;

        var textPosition = new Vector2(
            center.X - textWidth / 2f,
            center.Y + 9f
        );

        DrawString(
            font,
            textPosition,
            symbol,
            HorizontalAlignment.Left,
            -1,
            fontSize,
            textColor
        );
    }

    
    public override void _Ready()
    {
        // Evita suavizar los bordes de los sprites pixel art.
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

        LoadTextures();
    }

    private void LoadTextures()
    {
        _lightTile = LoadTextureIfExists(
            "res://assets/board/light_tile.png"
        );

        _darkTile = LoadTextureIfExists(
            "res://assets/board/dark_tile.png"
        );

        foreach (PieceType type in Enum.GetValues<PieceType>())
        {
            foreach (PlayerColor color in Enum.GetValues<PlayerColor>())
            {
                string colorName = color.ToString().ToLowerInvariant();
                string pieceName = type.ToString().ToLowerInvariant();

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
        if (!ResourceLoader.Exists(path))
            return null;

        return GD.Load<Texture2D>(path);
    }


    private void OnPieceMoved()
    {
        RefreshFromGame();
    }
}
