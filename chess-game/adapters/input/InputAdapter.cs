using Chess.Core.Model;
using Godot;

namespace Cheesse.Game.Adapters.Input;

public partial class InputAdapter : Node
{
    private ChessGame? _game;

    public void Attach(ChessGame game) => _game = game;
}
