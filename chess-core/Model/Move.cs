namespace Chess.Core.Model;

public readonly record struct Move(Square From, Square To, PieceType? Promotion = null)
{
    public string ToSan() => throw new NotImplementedException("CORE-F5");
}
