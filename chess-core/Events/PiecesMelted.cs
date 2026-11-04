using Chess.Core.Model;

namespace Chess.Core.Events;

public readonly record struct MeltedPiece(Square Square, Piece Piece);

public sealed record PiecesMelted
{
    public PiecesMelted(Move move, IEnumerable<MeltedPiece> pieces)
    {
        Move = move;
        Pieces = Array.AsReadOnly(pieces.ToArray());
    }

    public Move Move { get; }
    public IReadOnlyList<MeltedPiece> Pieces { get; }
}
