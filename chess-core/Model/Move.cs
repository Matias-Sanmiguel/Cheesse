using Chess.Core.Services;

namespace Chess.Core.Model;

public readonly record struct Move(Square From, Square To, PieceType? Promotion = null)
{
    // A move alone does not carry capture, piece, disambiguation, or check information.
    public string ToSan() =>
        throw new InvalidOperationException("SAN requires the position before the move. Use ToSan(Board).");

    public string ToSan(Board board) => SanNotation.ToSan(board, this);
}
