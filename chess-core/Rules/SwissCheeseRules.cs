using Chess.Core.Model;

namespace Chess.Core.Rules;

/// <summary>
/// Queso Suizo: fixed, symmetric holes. A hole is impassable for every piece;
/// sliders cannot cross it, while knights may jump over it but never land on it.
/// Keeping the pattern deterministic makes undo, saves and PGN replay exact.
/// </summary>
public static class SwissCheeseRules
{
    public static IReadOnlyList<Square> Holes { get; } = Array.AsReadOnly(new[]
    {
        new Square(2, 2), // c3
        new Square(5, 2), // f3
        new Square(3, 3), // d4
        new Square(4, 4), // e5
        new Square(2, 5), // c6
        new Square(5, 5), // f6
    });

    private static readonly HashSet<Square> HoleSet = new(Holes);

    public static bool IsHole(Square square) => HoleSet.Contains(square);

    public static void ValidatePosition(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        foreach (var hole in Holes)
        {
            if (board.At(hole) is not null)
                throw new ArgumentException($"La casilla {hole} es un agujero y no puede contener piezas.", nameof(board));
        }
    }
}
