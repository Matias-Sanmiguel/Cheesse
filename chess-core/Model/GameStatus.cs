namespace Chess.Core.Model;

public enum GameStatus
{
    Ongoing,
    Check,
    Checkmate,
    Stalemate,
    DrawByFiftyMove,
    DrawByRepetition,
    DrawByInsufficientMaterial,
}
