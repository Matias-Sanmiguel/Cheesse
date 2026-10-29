namespace Chess.Core.Model;

// Encapsula las reglas de disponibilidad de acciones según el estado.
internal abstract class GameState
{
    private static readonly GameState Active = new ActiveGameState();
    private static readonly GameState Finished = new FinishedGameState();

    public abstract bool CanMove { get; }

    public static GameState For(GameStatus status) =>
        status is GameStatus.Ongoing or GameStatus.Check ? Active : Finished;

    private sealed class ActiveGameState : GameState
    {
        public override bool CanMove => true;
    }

    private sealed class FinishedGameState : GameState
    {
        public override bool CanMove => false;
    }
}
