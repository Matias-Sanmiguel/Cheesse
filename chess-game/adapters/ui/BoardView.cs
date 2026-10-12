using Chess.Core.Model;
using Cheesse.Game.Adapters.Infra;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

public partial class BoardView : Node2D
{
    private const int SquareSize = 80;

    private ChessGame? _game;

    public void Attach(ChessGame game, GodotEventBus bus)
    {
        _game = game;
        bus.PieceMoved += OnPieceMoved;
        QueueRedraw();
    }

    public override void _Draw()
    {
        for (var rank = 0; rank < 8; rank++)
        {
            for (var file = 0; file < 8; file++)
            {
                var dark = (file + rank) % 2 == 1;
                var color = dark ? new Color("769656") : new Color("eeeed2");
                DrawRect(new Rect2(file * SquareSize, (7 - rank) * SquareSize, SquareSize, SquareSize), color);
            }
        }
    }

    private void OnPieceMoved() => QueueRedraw();
}
