namespace Chess.Core.Model;

public readonly record struct CastlingRights(
    bool WhiteKingside,
    bool WhiteQueenside,
    bool BlackKingside,
    bool BlackQueenside)
{
    public static CastlingRights None { get; } = new(false, false, false, false);

    public static CastlingRights All { get; } = new(true, true, true, true);
}
