namespace Chess.Core.Model;

// Encapsula las reglas de disponibilidad de acciones según el estado.
internal abstract class GameState : IGameState
{
    private static readonly GameState Active = new ActiveGameState();
    private static readonly GameState Finished = new FinishedGameState();

    public abstract bool AcceptsMoves { get; }

    public static GameState For(GameStatus status) =>
        status is GameStatus.Ongoing or GameStatus.Check ? Active : Finished;

    private sealed class ActiveGameState : GameState
    {
        public override bool AcceptsMoves => true;
    }

    private sealed class FinishedGameState : GameState
    {
        public override bool AcceptsMoves => false;
    }
}
