namespace Chess.Core.Model;

public enum GameStatus
{
    Ongoing,
    Check,
    Checkmate,
    PicadaVictory,
    Stalemate,
    DrawByFiftyMove,
    DrawByRepetition,
    DrawByInsufficientMaterial,
}
