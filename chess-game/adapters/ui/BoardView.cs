using Chess.Core.Model;
using Cheesse.Game.Adapters.Infra;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

public partial class BoardView : Node2D
{
    private const int SquareSize = 80;
    private ChessGame? _game;
    private GodotEventBus? _bus;
    private Square? _selected;
    private Texture2D _pieces = null!;

    public override void _Ready() =>
        _pieces = GD.Load<Texture2D>("res://assets/pieces.svg");

    public void Attach(ChessGame game, GodotEventBus bus)
    {
        if (_bus is not null) _bus.StateChanged -= OnStateChanged;
        _game = game;
        _bus = bus;
        _selected = null;
        bus.StateChanged += OnStateChanged;
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        if (_bus is not null) _bus.StateChanged -= OnStateChanged;
    }

    public void SelectSquare(Square? square)
    {
        _selected = square;
        QueueRedraw();
    }

    public bool TryGetSquare(Vector2 localPosition, out Square square)
    {
        square = default;
        if (localPosition.X < 0 || localPosition.Y < 0 ||
            localPosition.X >= SquareSize * 8 || localPosition.Y >= SquareSize * 8)
            return false;
        square = new Square((int)localPosition.X / SquareSize, 7 - (int)localPosition.Y / SquareSize);
        return true;
    }

    public override void _Draw()
    {
        var targets = _selected is Square selected && _game is not null
            ? _game.LegalMoves().Where(move => move.From == selected).Select(move => move.To).ToHashSet()
            : new HashSet<Square>();
        var lastMove = _game?.History.LastOrDefault();
        var hasLastMove = _game?.History.Count > 0;
        for (var rank = 0; rank < 8; rank++)
        {
            for (var file = 0; file < 8; file++)
            {
                var square = new Square(file, rank);
                var origin = new Vector2(file * SquareSize, (7 - rank) * SquareSize);
                var destination = new Rect2(origin, new Vector2(SquareSize, SquareSize));
                var color = (file + rank) % 2 == 1 ? new Color("769656") : new Color("eeeed2");
                DrawRect(destination, color);
                if (hasLastMove && lastMove is Move move && (square == move.From || square == move.To))
                    DrawRect(destination, new Color("C9B458", 0.55f));
                DrawCoordinates(file, rank, origin, color);
                if (_selected == square)
                    DrawRect(destination, new Color(0.95f, 0.73f, 0.2f, 0.55f));
                if (targets.Contains(square))
                    DrawCircle(origin + new Vector2(40, 40), 9, new Color(0, 0, 0, 0.25f));

                if (_game?.Board.At(square) is not Piece piece)
                    continue;
                var row = piece.Color == PlayerColor.White ? 0 : 1;
                var source = new Rect2((int)piece.Type * SquareSize, row * SquareSize, SquareSize, SquareSize);
                DrawTextureRectRegion(_pieces, destination, source);
            }
        }
    }

    private void DrawCoordinates(int file, int rank, Vector2 origin, Color squareColor)
    {
        var textColor = squareColor == new Color("769656") ? new Color("eeeed2") : new Color("769656");

        var font = ThemeDB.FallbackFont;
        const int fontSize = 15;

        if (file == 0)
        {
            DrawString(font, origin + new Vector2(5, 16), (rank + 1).ToString(), HorizontalAlignment.Left, -1, fontSize, textColor);
        }

        if (rank == 0)
        {
            DrawString(font, origin + new Vector2(65, 76), ((char)('a' + file)).ToString(), HorizontalAlignment.Left, -1, fontSize, textColor);
        }
    }

    private void OnStateChanged()
    {
        _selected = null;
        QueueRedraw();
    }
}
