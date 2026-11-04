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
    DrawByFivefoldRepetition,
    DrawBySeventyFiveMove,
    DrawByAgreement,
    Resigned,
    Timeout,
    DrawByTimeout,
    DrawByResignation,
    KingMelted,
}
