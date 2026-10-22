using Chess.Core.Rules;

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

    public Square? EnPassant { get; } //Guarda la casilla a la que puede ir para capturar al paso, por ejemplo si el peón va de d7 a d5, el otro peón puede ir a d6 para capturarlo

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

    public Board WithMove(Move move) {

        var next = (Piece?[])_squares.Clone();
        var piece = next[move.From.Index];
        var originalPiece = piece;
        next[move.From.Index] = null;

        //Verficamos si estamos haciendo una captura al paso
        if (piece is Piece movingPawn && movingPawn.Type == PieceType.Pawn && EnPassant == move.To && move.From.File != move.To.File && next[move.To.Index] == null) {

            //Buscamos la casilla donde está el peón rival
            Square capturedSquare = new Square(move.To.File, move.From.Rank);

            Piece? capturedPiece = next[capturedSquare.Index];

            //Si es un peón rival, lo eliminamos
            if (capturedPiece is Piece captured && captured.Type == PieceType.Pawn && captured.Color != movingPawn.Color) {
                next[capturedSquare.Index] = null;
            }
        }

        //Transformamos el peón en la pieza elegida (Promoción)
        if (piece is Piece moving && move.Promotion is PieceType promotion)
            piece = moving with { Type = promotion };

        //Ponemos a nuestra pieza en el destino
        next[move.To.Index] = piece;

        Square? newEnPassant = null;

        //Verificamos si el movimiento es un doble avance de peón
        if (originalPiece is Piece pawn && pawn.Type == PieceType.Pawn && move.From.File == move.To.File) {

            //Fila inicial y dirección según el color
            int initialRank = pawn.Color == PlayerColor.White ? 1 : 6;
            int direction = pawn.Color == PlayerColor.White ? 1 : -1;

            //Verificamos que avance dos casillas desde su fila inicial
            if (move.From.Rank == initialRank && move.To.Rank - move.From.Rank == direction * 2){

                int middleRank = (move.From.Rank + move.To.Rank) / 2;

                Square middleSquare = new Square(move.From.File, middleRank);

                // Verificamos que el camino original estuviera libre
                if (At(middleSquare) == null && At(move.To) == null) {
                    newEnPassant = middleSquare;
                }
            }
        }
        return new Board(next, Castling, newEnPassant);
    }

    public bool IsAttacked(Square square, PlayerColor by) =>
        AttackDetector.IsAttacked(this, square, by);
}
