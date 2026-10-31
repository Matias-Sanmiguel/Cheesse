using Chess.Core.Model;

namespace Chess.Core.Rules;

public abstract class SlidingMoveRule : IPieceMoveRule
{
    protected abstract IEnumerable<(int File, int Rank)> Directions { get; }

    public void AddMoves(Board board, Square from, PlayerColor side, List<Move> moves)
    {
        foreach (var (dx, dy) in Directions)
        {
            var file = from.File + dx;
            var rank = from.Rank + dy;
            while (file is >= 0 and < 8 && rank is >= 0 and < 8)
            {
                var target = new Square(file, rank);
                var occupant = board.At(target);
                if (occupant is null || occupant.Value.Color != side)
                    moves.Add(new Move(from, target));
                if (occupant is not null) break;
                file += dx;
                rank += dy;
            }
        }
    }
}

public sealed class BishopMoveRule : SlidingMoveRule
{
    internal static readonly (int, int)[] Diagonals = [(-1, -1), (-1, 1), (1, -1), (1, 1)];
    protected override IEnumerable<(int, int)> Directions => Diagonals;
}

public sealed class RookMoveRule : SlidingMoveRule
{
    internal static readonly (int, int)[] Straights = [(-1, 0), (1, 0), (0, -1), (0, 1)];
    protected override IEnumerable<(int, int)> Directions => Straights;
}

public sealed class QueenMoveRule : SlidingMoveRule
{
    protected override IEnumerable<(int, int)> Directions =>
        BishopMoveRule.Diagonals.Concat(RookMoveRule.Straights);
}

public sealed class KnightMoveRule : IPieceMoveRule
{
    private static readonly (int, int)[] Offsets =
        [(-2, -1), (-2, 1), (-1, -2), (-1, 2), (1, -2), (1, 2), (2, -1), (2, 1)];

    public void AddMoves(Board board, Square from, PlayerColor side, List<Move> moves) =>
        StepMoves.Add(board, from, side, Offsets, moves);
}

public sealed class KingMoveRule : IPieceMoveRule
{
    private static readonly (int, int)[] Offsets =
        [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)];

    public void AddMoves(Board board, Square from, PlayerColor side, List<Move> moves)
    {
        StepMoves.Add(board, from, side, Offsets, moves);
        moves.AddRange(SpecialMoves.Castling(board, side).Where(move => move.From == from));
    }
}

internal static class StepMoves
{
    internal static void Add(Board board, Square from, PlayerColor side,
        IEnumerable<(int File, int Rank)> offsets, List<Move> moves)
    {
        foreach (var (dx, dy) in offsets)
        {
            var file = from.File + dx;
            var rank = from.Rank + dy;
            if (file is < 0 or >= 8 || rank is < 0 or >= 8) continue;
            var target = new Square(file, rank);
            if (board.At(target) is not Piece occupant || occupant.Color != side)
                moves.Add(new Move(from, target));
        }
    }
}

public sealed class PawnMoveRule : IPieceMoveRule
{
    public void AddMoves(Board board, Square from, PlayerColor side, List<Move> moves)
    {
        moves.AddRange(SpecialMoves.Promotions(board, side).Where(move => move.From == from));
        moves.AddRange(SpecialMoves.EnPassant(board, side).Where(move => move.From == from));
        var direction = side == PlayerColor.White ? 1 : -1;
        var nextRank = from.Rank + direction;
        if (nextRank is < 0 or >= 8 || nextRank == (side == PlayerColor.White ? 7 : 0)) return;
        var forward = new Square(from.File, nextRank);
        if (board.At(forward) is null)
        {
            moves.Add(new Move(from, forward));
            if (from.Rank == (side == PlayerColor.White ? 1 : 6))
            {
                var doubleForward = new Square(from.File, from.Rank + 2 * direction);
                if (board.At(doubleForward) is null)
                    moves.Add(new Move(from, doubleForward));
            }
        }
        foreach (var dx in new[] { -1, 1 })
        {
            var file = from.File + dx;
            if (file is < 0 or >= 8) continue;
            var target = new Square(file, nextRank);
            if (board.At(target) is Piece occupant && occupant.Color != side)
                moves.Add(new Move(from, target));
        }
    }
}
