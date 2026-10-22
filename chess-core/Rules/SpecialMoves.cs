using Chess.Core.Model;

namespace Chess.Core.Rules;

public static class SpecialMoves
{

    public static IReadOnlyList<Move> Castling(Board board, PlayerColor side)
    {
        var moves = new List<Move>();

        int rank = side == PlayerColor.White ? 0 : 7;
        Square kingSquare = new Square(4, rank);

        Piece? king = board.At(kingSquare); //Compruebo que el rey esté en la casilla inicial

        if (king == null || king.Value.Type != PieceType.King || king.Value.Color != side)
        {
            return moves;
        }

        //El rey no puede estar actualmente en jaque.
        PlayerColor opponent = side == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;

        if (board.IsAttacked(kingSquare, opponent))
            return moves;

        //Enroque corto
        bool canCastleShort = side == PlayerColor.White ? board.Castling.WhiteKingside : board.Castling.BlackKingside;

        Square shortRookSquare = new Square(7, rank);
        Piece? shortRook = board.At(shortRookSquare);

        Square fSquare = new Square(5, rank);
        Square gSquare = new Square(6, rank);

        if (canCastleShort && shortRook is Piece rookShort && rookShort.Type == PieceType.Rook && rookShort.Color == side && board.At(fSquare) == null && board.At(gSquare) == null &&
            !board.IsAttacked(fSquare, opponent) && !board.IsAttacked(gSquare, opponent))
        {
            moves.Add(new Move(kingSquare, gSquare));
        }

        //Enroque largo
        bool canCastleLong = side == PlayerColor.White ? board.Castling.WhiteQueenside : board.Castling.BlackQueenside;

        Square longRookSquare = new Square(0, rank);
        Piece? longRook = board.At(longRookSquare);

        Square bSquare = new Square(1, rank);
        Square cSquare = new Square(2, rank);
        Square dSquare = new Square(3, rank);

        if (canCastleLong && longRook is Piece rookLong && rookLong.Type == PieceType.Rook && rookLong.Color == side &&
            board.At(bSquare) == null && board.At(cSquare) == null && board.At(dSquare) == null && !board.IsAttacked(dSquare, opponent) && !board.IsAttacked(cSquare, opponent))
        {
            moves.Add(new Move(kingSquare, cSquare));
        }

        return moves;
    }



    public static IReadOnlyList<Move> EnPassant(Board board, PlayerColor side) {

        var moves = new List<Move>();

        //Si no existe casilla al paso, no hay capturas posibles
        if (board.EnPassant == null)
            return moves;

        Square target = board.EnPassant.Value;

        //Validamos la fila de destino según el color
        int expectedRank = side == PlayerColor.White ? 5 : 2;

        if (target.Rank != expectedRank)
            return moves;

        //Buscamos la fila donde pueden estar nuestros peones
        int pawnRank = side == PlayerColor.White? target.Rank - 1: target.Rank + 1;

        //La casilla donde debe estar el peón rival
        Square capturedSquare = new Square(target.File, pawnRank);
        Piece? capturedPiece = board.At(capturedSquare);

        //Comprobamos que exista un peón rival y el destino esté vacío
        if (capturedPiece == null || capturedPiece.Value.Type != PieceType.Pawn || capturedPiece.Value.Color == side || board.At(target) != null) {
            return moves;
        }

        //Buscamos peones propios a izquierda y derecha
        foreach (int offset in new[] { -1, 1 }) {
            int pawnFile = target.File + offset;

            if (pawnFile < 0 || pawnFile > 7)
                continue;

            Square from = new Square(pawnFile, pawnRank);
            Piece? piece = board.At(from);

            if (piece != null && piece.Value.Type == PieceType.Pawn && piece.Value.Color == side)
            {
                moves.Add(new Move(from, target));
            }
        }

        return moves;
    }


    public static IReadOnlyList<Move> Promotions(Board board, PlayerColor side) {

        var moves = new List<Move>();

        //Usando ? establezco un if-else, si la pieza es blanca entonces el rango del peón es 6, si es negra entonces es 1, lo mismo para el rango de promoción
        //Usamos de 0 a 7 en vez de 1 a 8 para que sea más fácil de manejar en el código, ya que los índices de los arreglos comienzan en 0
        int pawnRank = side == PlayerColor.White ? 6 : 1;
        int promotionRank = side == PlayerColor.White ? 7 : 0;

        PieceType[] promotions = {PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight};

        for (int file = 0; file < 8; file++){
            Square from = new Square(file, pawnRank);
            Piece? piece = board.At(from);

            //Si no hay una pieza mia, no es un peón o no es de mi color, salto a la siguiente columna
            if (piece == null || piece.Value.Type != PieceType.Pawn || piece.Value.Color != side) {
                continue;
            }

            //Promoción avanzando hacia adelante, promocion normal, solo va para adelante, por eso verificamos que la casilla de destino esté vacía
            Square forward = new Square(file, promotionRank);

            if (board.At(forward) == null) {
                foreach (PieceType promotion in promotions) {
                    moves.Add(new Move(from, forward, promotion));
                }
            }

            //Promoción capturando diagonalmente, verificamos qque vaya de 0 a 7, y que la casilla de destino tenga una pieza del color contrario y que no sea un rey
            foreach (int offset in new[] { -1, 1 }) {
                int targetFile = file + offset;

                if (targetFile < 0 || targetFile > 7)
                    continue;

                Square to = new Square(targetFile, promotionRank);
                Piece? destination = board.At(to);

                if (destination != null && destination.Value.Color != side && destination.Value.Type != PieceType.King) {
                    foreach (PieceType promotion in promotions) {
                        moves.Add(new Move(from, to, promotion));
                    }
                }
            }
        }

        return moves;
    }


}
