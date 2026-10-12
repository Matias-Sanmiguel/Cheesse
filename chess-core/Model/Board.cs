namespace Chess.Core.Model;

public sealed class Board
{
    private readonly Piece?[] _squares;

    private Board(Piece?[] squares, CastlingRights castling, Square? enPassant)
    {
        _squares = squares;
        Castling = castling;
        EnPassant = enPassant;
    }

    public CastlingRights Castling { get; }

    public Square? EnPassant { get; }

    public static Board Empty() => new(new Piece?[64], CastlingRights.None, null);

    public Piece? At(Square square) => _squares[square.Index];

    public Board WithPiece(Square square, Piece? piece)
    {
        var next = (Piece?[])_squares.Clone();
        next[square.Index] = piece;
        return new Board(next, Castling, EnPassant);
    }

    public Board WithCastling(CastlingRights castling) => new((Piece?[])_squares.Clone(), castling, EnPassant);

    public Board WithEnPassant(Square? enPassant) => new((Piece?[])_squares.Clone(), Castling, enPassant);

    public Board WithMove(Move move)
    {
        var next = (Piece?[])_squares.Clone();
        var piece = next[move.From.Index];
        next[move.From.Index] = null;

        if (piece is Piece moving && move.Promotion is PieceType promotion)
            piece = moving with { Type = promotion };

        next[move.To.Index] = piece;
        return new Board(next, Castling, EnPassant);
    }

    public bool IsAttacked(Square square, PlayerColor by) =>
        throw new NotImplementedException("CORE-F2");
}
