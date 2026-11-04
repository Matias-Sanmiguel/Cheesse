using Chess.Core.Model;

namespace Chess.Core.Rules;

/// <summary>Strategy: cada modo puede agregar una victoria y definir sus reglas de tablas.</summary>
public interface IGameModeRules
{
    GameStatus? SpecialOutcome(Board board, PlayerColor lastMover);
    bool InsufficientMaterialIsDraw { get; }
}

public static class GameModeRuleCatalog
{
    private static readonly IGameModeRules Classic = new ClassicModeRules();
    private static readonly IGameModeRules Picada = new PicadaModeRules();

    public static IGameModeRules For(GameMode mode) => mode switch
    {
        GameMode.Classic or GameMode.Fondue => Classic,
        GameMode.ReyDeLaPicada => Picada,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private sealed class ClassicModeRules : IGameModeRules
    {
        public GameStatus? SpecialOutcome(Board board, PlayerColor lastMover) => null;
        public bool InsufficientMaterialIsDraw => true;
    }
}

/// <summary>
/// Rey de la Picada (King of the Hill): gana quien entra legalmente con su rey
/// a d4, e4, d5 o e5. Las demás jugadas conservan las reglas de ajedrez.
/// </summary>
public sealed class PicadaModeRules : IGameModeRules
{
    public static IReadOnlyList<Square> GoalSquares { get; } = Array.AsReadOnly(new[]
    {
        new Square(3, 3), new Square(4, 3),
        new Square(3, 4), new Square(4, 4)
    });

    // ¡Dos reyes solos también pueden llegar al centro! No son tablas automáticas.
    public bool InsufficientMaterialIsDraw => false;

    public GameStatus? SpecialOutcome(Board board, PlayerColor lastMover)
    {
        foreach (var square in GoalSquares)
        {
            if (board.At(square) is Piece piece &&
                piece.Type == PieceType.King && piece.Color == lastMover)
                return GameStatus.PicadaVictory;
        }
        return null;
    }
}
