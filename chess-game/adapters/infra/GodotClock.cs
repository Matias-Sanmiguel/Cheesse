using Chess.Core.Ports;
using Godot;

namespace Cheesse.Game.Adapters.Infra;

public partial class GodotClock : Node, IClock
{
    public double ElapsedSeconds => Time.GetTicksMsec() / 1000.0;
}
