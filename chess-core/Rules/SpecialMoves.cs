using Chess.Core.Model;

namespace Chess.Core.Rules;

public static class SpecialMoves
{
    public static IReadOnlyList<Move> Castling(Board board, PlayerColor side) =>
        throw new NotImplementedException("CORE-F4");

    public static IReadOnlyList<Move> EnPassant(Board board, PlayerColor side) =>
        throw new NotImplementedException("CORE-F4");

    public static IReadOnlyList<Move> Promotions(Board board, PlayerColor side) =>
        throw new NotImplementedException("CORE-F4");
}
