using System.Security.Cryptography;
using Chess.Core.Model;
using Chess.Core.Services;

namespace Chess.Core.Rules;

/// <summary>
/// Queso Suizo: six independently generated, 180-degree symmetric holes.
/// A hole blocks movement and sliding attacks, but a knight can jump over it.
/// The layout is part of the immutable Board, not global mutable match state.
/// </summary>
public static class SwissCheeseRules
{
    public const int HoleCount = 6;

    // Backward compatibility for old Swiss PGNs saved before randomized layouts.
    public static IReadOnlyList<Square> LegacyHoles { get; } = Array.AsReadOnly(new[]
    {
        new Square(2, 2), new Square(5, 2),
        new Square(3, 3), new Square(4, 4),
        new Square(2, 5), new Square(5, 5)
    });

    private static readonly object GenerationLock = new();
    private static ulong? _previousGeneratedMask;

    // One representative from each of 16 symmetric pairs in ranks 3 to 6.
    private static readonly Square[] PairStarts =
        (from rank in new[] { 2, 3 }
         from file in Enumerable.Range(0, 8)
         select new Square(file, rank)).ToArray();

    /// <summary>
    /// Generate a new layout; never repeat the immediately preceding generation.
    /// Only central ranks may contain holes. Reject patterns that block opening
    /// pawn development, both knights, or too many full-file corridors.
    /// </summary>
    public static IReadOnlyList<Square> Generate(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        var possible = new List<ulong>();
        bool isStartingPosition = FenSerializer.Write(board) == FenSerializer.InitialPosition;

        for (int i = 0; i < PairStarts.Length - 2; i++)
        for (int j = i + 1; j < PairStarts.Length - 1; j++)
        for (int k = j + 1; k < PairStarts.Length; k++)
        {
            var holes = new[]
            {
                PairStarts[i], Opposite(PairStarts[i]),
                PairStarts[j], Opposite(PairStarts[j]),
                PairStarts[k], Opposite(PairStarts[k])
            };
            if (holes.Any(hole => board.At(hole) is not null) || !HasOpenCorridors(holes))
                continue;
            if (isStartingPosition && !HasPlayableOpening(board.WithSwissHoles(holes)))
                continue;
            possible.Add(ToMask(holes));
        }

        if (possible.Count == 0)
            throw new InvalidOperationException("No hay una distribución segura de agujeros para esta posición.");

        lock (GenerationLock)
        {
            // Exclude the previous layout while the game is running, including rematches.
            var choices = possible.Count > 1 && _previousGeneratedMask is ulong last
                ? possible.Where(mask => mask != last).ToArray()
                : possible.ToArray();
            if (choices.Length == 0) choices = possible.ToArray();
            ulong selected = choices[RandomNumberGenerator.GetInt32(choices.Length)];
            _previousGeneratedMask = selected;
            return FromMask(selected);
        }
    }

    /// <summary>
    /// Validate imported layouts (including PGN) and protect piece occupancy.
    /// Layout validation does not require an opening position so analysis FENs work.
    /// </summary>
    public static ulong ValidateLayout(Board board, IEnumerable<Square> holes)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(holes);
        var selected = holes.ToArray();
        if (selected.Length != HoleCount || selected.Distinct().Count() != HoleCount)
            throw new ArgumentException("Queso Suizo requiere seis agujeros diferentes.", nameof(holes));
        if (selected.Any(hole => hole.Rank is < 2 or > 5 ||
                                 board.At(hole) is not null || !selected.Contains(Opposite(hole))))
            throw new ArgumentException("Los agujeros deben quedar libres y equilibrados en el centro.", nameof(holes));
        if (!HasOpenCorridors(selected))
            throw new ArgumentException("Los agujeros bloquean demasiadas rutas de salida.", nameof(holes));
        return ToMask(selected);
    }

    public static string Format(IReadOnlyList<Square> holes) =>
        string.Join(",", holes.OrderBy(hole => hole.Index).Select(hole => hole.ToString()));

    public static IReadOnlyList<Square> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("Falta la distribución de agujeros.");
        var names = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (names.Length != HoleCount || names.Any(name => name.Length != 2 ||
            name[0] is < 'a' or > 'h' || name[1] is < '1' or > '8'))
            throw new FormatException("Distribución de agujeros inválida.");
        return Array.AsReadOnly(names.Select(name =>
            new Square(name[0] - 'a', name[1] - '1')).ToArray());
    }

    public static IReadOnlyList<Square> FromMask(ulong mask)
    {
        var squares = new List<Square>(HoleCount);
        for (int index = 0; index < 64; index++)
            if ((mask & (1UL << index)) != 0)
                squares.Add(Square.FromIndex(index));
        return squares.AsReadOnly();
    }

    private static ulong ToMask(IEnumerable<Square> holes)
    {
        ulong mask = 0;
        foreach (var square in holes) mask |= 1UL << square.Index;
        return mask;
    }

    private static Square Opposite(Square square) =>
        new(7 - square.File, 7 - square.Rank);

    private static bool HasOpenCorridors(IReadOnlyList<Square> holes)
    {
        // At least four completely clear files and six unobstructed first pawn steps.
        if (holes.Select(h => h.File).Distinct().Count() > 4) return false;
        if (holes.Count(h => h.Rank == 2) > 2 || holes.Count(h => h.Rank == 5) > 2)
            return false;
        return holes.Count(h => (h.File is 3 or 4) && (h.Rank is 3 or 4)) <= 2;
    }

    private static bool HasPlayableOpening(Board swiss)
    {
        foreach (var side in new[] { PlayerColor.White, PlayerColor.Black })
        {
            var board = swiss.WithState(side, 0, 1);
            var moves = MoveGenerator.Legal(board, side);
            if (moves.Count < 14) return false;

            var pawnFiles = moves.Where(move => board.At(move.From)?.Type == PieceType.Pawn)
                .Select(move => move.From.File).Distinct().Count();
            if (pawnFiles < 6) return false;

            int home = side == PlayerColor.White ? 0 : 7;
            // Both starting knights must each have at least one legal destination.
            if (!moves.Any(move => move.From == new Square(1, home)) ||
                !moves.Any(move => move.From == new Square(6, home)))
                return false;
        }

        return true;
    }
}
