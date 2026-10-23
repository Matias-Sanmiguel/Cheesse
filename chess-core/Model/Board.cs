using Chess.Core.Rules;

namespace Chess.Core.Model;

public sealed class Board
{
    private readonly Piece?[] _squares;

    private Board(Piece?[] squares, CastlingRights castling, Square? enPassant,
        PlayerColor turn = PlayerColor.White, int halfmoveClock = 0, int fullmoveNumber = 1)
    {
        _squares = squares;
        Castling = castling;
        EnPassant = enPassant;
        Turn = turn;
        HalfmoveClock = halfmoveClock;
        FullmoveNumber = fullmoveNumber;
    }

    public PlayerColor Turn { get; }

    public int HalfmoveClock { get; }

    public int FullmoveNumber { get; }

    public CastlingRights Castling { get; }

    public Board WithState(PlayerColor turn, int halfmoveClock, int fullmoveNumber)
    {
        if (!Enum.IsDefined(turn))
            throw new ArgumentOutOfRangeException(nameof(turn));
        ArgumentOutOfRangeException.ThrowIfNegative(halfmoveClock);
        ArgumentOutOfRangeException.ThrowIfLessThan(fullmoveNumber, 1);

        return new Board((Piece?[])_squares.Clone(), Castling, EnPassant,
            turn, halfmoveClock, fullmoveNumber);
    }

    public Square? EnPassant { get; } //Guarda la casilla a la que puede ir para capturar al paso, por ejemplo si el peón va de d7 a d5, el otro peón puede ir a d6 para capturarlo

    public static Board Empty() => new(new Piece?[64], CastlingRights.None, null);

    public Piece? At(Square square) => _squares[square.Index];

    public Board WithPiece(Square square, Piece? piece)
    {
        var next = (Piece?[])_squares.Clone();
        next[square.Index] = piece;
        return new Board(next, Castling, EnPassant, Turn, HalfmoveClock, FullmoveNumber);
    }

    public Board WithCastling(CastlingRights castling) => new((Piece?[])_squares.Clone(), castling, EnPassant, Turn, HalfmoveClock, FullmoveNumber);

    public Board WithEnPassant(Square? enPassant) => new((Piece?[])_squares.Clone(), Castling, enPassant, Turn, HalfmoveClock, FullmoveNumber);


    public Board WithMove(Move move)
    {
        var next = (Piece?[])_squares.Clone();

        Piece? piece = next[move.From.Index];
        Piece? capturedPiece = next[move.To.Index];

        if (piece == null) throw new InvalidOperationException("No hay una pieza en la casilla de origen.");

        Piece moving = piece.Value;

        next[move.From.Index] = null;

        //Captura al paso (enPassant)
        if (moving.Type == PieceType.Pawn && EnPassant == move.To && move.From.File != move.To.File && capturedPiece == null)
        {
            Square capturedSquare = new Square(move.To.File, move.From.Rank);
            Piece? capturedPawn = next[capturedSquare.Index];

            if (capturedPawn is Piece pawn && pawn.Type == PieceType.Pawn && pawn.Color != moving.Color)
            {
                next[capturedSquare.Index] = null;
            }
        }

        //Promoción
        Piece placedPiece = moving;

        if (moving.Type == PieceType.Pawn && move.Promotion is PieceType promotion && move.To.Rank == (moving.Color == PlayerColor.White ? 7 : 0))
        {
            placedPiece = moving with { Type = promotion };
        }

        next[move.To.Index] = placedPiece;

        //Enroque: mover también la torre
        int homeRank = moving.Color == PlayerColor.White ? 0 : 7;

        if (moving.Type == PieceType.King && move.From.File == 4 && move.From.Rank == homeRank && move.To.Rank == homeRank && (move.To.File == 6 || move.To.File == 2))
        {
            bool kingside = move.To.File == 6;

            Square rookFrom = new Square(kingside ? 7 : 0, homeRank);
            Square rookTo = new Square(kingside ? 5 : 3, homeRank);

            if (next[rookFrom.Index] is Piece rook && rook.Type == PieceType.Rook && rook.Color == moving.Color)
            {
                next[rookFrom.Index] = null;
                next[rookTo.Index] = rook;
            }
        }

        CastlingRights newCastling = Castling;

        //Si se mueve el rey, pierde ambos derechos
        if (moving.Type == PieceType.King)
        {
            newCastling = moving.Color == PlayerColor.White ? newCastling with { WhiteKingside = false, WhiteQueenside = false } : newCastling with { BlackKingside = false, BlackQueenside = false };
        }

        //Si se mueve una torre desde su casilla inicial
        if (moving.Type == PieceType.Rook)
        {
            newCastling = WithoutRookRight(newCastling, move.From, moving.Color);
        }

        //Si se captura una torre en su casilla inicial
        if (capturedPiece is Piece capturedRook && capturedRook.Type == PieceType.Rook)
        {
            newCastling = WithoutRookRight(newCastling, move.To, capturedRook.Color);
        }

        //Actualizar en passant
        Square? newEnPassant = null;

        if (moving.Type == PieceType.Pawn && move.From.File == move.To.File)
        {
            int initialRank = moving.Color == PlayerColor.White ? 1 : 6;
            int direction = moving.Color == PlayerColor.White ? 1 : -1;

            if (move.From.Rank == initialRank && move.To.Rank - move.From.Rank == direction * 2)
            {
                int middleRank = (move.From.Rank + move.To.Rank) / 2;
                Square middleSquare = new Square(move.From.File, middleRank);

                if (At(middleSquare) == null && At(move.To) == null)
                {
                    newEnPassant = middleSquare;
                }
            }
        }

        int halfmoveClock = moving.Type == PieceType.Pawn || capturedPiece is not null
            ? 0
            : HalfmoveClock + 1;
        int fullmoveNumber = FullmoveNumber + (moving.Color == PlayerColor.Black ? 1 : 0);
        PlayerColor nextTurn = moving.Color == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;

        return new Board(next, newCastling, newEnPassant, nextTurn, halfmoveClock, fullmoveNumber);
    }


    private static CastlingRights WithoutRookRight(CastlingRights rights, Square square, PlayerColor color)
    {
        if (color == PlayerColor.White && square.Rank == 0)
        {
            if (square.File == 0) return rights with { WhiteQueenside = false };

            if (square.File == 7) return rights with { WhiteKingside = false };
        }

        if (color == PlayerColor.Black && square.Rank == 7)
        {
            if (square.File == 0) return rights with { BlackQueenside = false };

            if (square.File == 7) return rights with { BlackKingside = false };
        }

        return rights;
    }


    public bool IsAttacked(Square square, PlayerColor by) =>
        AttackDetector.IsAttacked(this, square, by);
}
