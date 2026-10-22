using Chess.Core.Model;
using Chess.Core.Rules;
using NUnit.Framework;

namespace Chess.Tests;

public class PromotionsTests
{
    [Test]
    public void WhitePawn_ShouldGenerateFourPromotions()
    {
        //ARRANGE: preparamos el tablero
        Square from = new Square(4, 6); // e7
        Square to = new Square(4, 7);   // e8

        Board board = Board.Empty(); //creamos un tablero vacío

        board = board.WithPiece(from, new Piece(PieceType.Pawn, PlayerColor.White)); //paramos a nuestro peon en la casilla e7 del nuevo tablero

        //ACT: ejecutamos el método a probar
        var moves = SpecialMoves.Promotions(board, PlayerColor.White);

        //ASSERT: verificamos el resultado
        Assert.That(moves, Has.Count.EqualTo(4)); //se tienen que generar 4 movimientos, porque el peon puede promocionar a reina, torre, alfil o caballo

        Assert.That(moves.Select(m => m.Promotion), Is.EquivalentTo(new PieceType?[]
            {
                PieceType.Queen,
                PieceType.Rook,
                PieceType.Bishop,
                PieceType.Knight
            })
        );

        Assert.That(moves.All(m => m.From == from && m.To == to), Is.True); //todos los movimientos deben tener la casilla de origen y destino correctas
    }


    [Test] //Mismo test pero para un peón negro
    public void BlackPawn_ShouldGenerateFourPromotions()
    {
        // ARRANGE: peón negro en e2
        Square from = new Square(4, 1);
        Square to = new Square(4, 0);

        Board board = Board.Empty();

        board = board.WithPiece(
            from,
            new Piece(PieceType.Pawn, PlayerColor.Black)
        );

        // ACT
        var moves = SpecialMoves.Promotions(
            board, PlayerColor.Black
        );

        // ASSERT
        Assert.That(moves, Has.Count.EqualTo(4));

        Assert.That(
            moves.Select(m => m.Promotion),
            Is.EquivalentTo(new PieceType?[]
            {
                PieceType.Queen,
                PieceType.Rook,
                PieceType.Bishop,
                PieceType.Knight
            })
        );

        Assert.That(
            moves.All(m => m.From == from && m.To == to),
            Is.True
        );
    }


    [Test] //Probamos que pueda promocionar habiendo capturado una pieza (en este caso una torre)
    public void WhitePawn_ShouldPromoteByCapturing()
    {
        //ARRANGE
        Square from = new Square(4, 6); // e7
        Square to = new Square(5, 7);   // f8

        Board board = Board.Empty()
            .WithPiece(from,
                new Piece(PieceType.Pawn, PlayerColor.White))
            .WithPiece(to,
                new Piece(PieceType.Rook, PlayerColor.Black));

        //ACT
        var moves = SpecialMoves.Promotions(
            board, PlayerColor.White
        );

        //ASSERT
        var capturePromotions = moves
            .Where(m => m.From == from && m.To == to)
            .ToList();

        Assert.That(capturePromotions, Has.Count.EqualTo(4));

        Assert.That(
            capturePromotions.Select(m => m.Promotion),
            Is.EquivalentTo(new PieceType?[]
            {
                PieceType.Queen,
                PieceType.Rook,
                PieceType.Bishop,
                PieceType.Knight
            })
        );
    }


    [Test]
    public void Pawn_ShouldPromoteToKnight()
    {
        //ARRANGE: creamos un peón blanco en e7
        Square from = new Square(4, 6);
        Square to = new Square(4, 7);

        Board board = Board.Empty()
            .WithPiece(from,
                new Piece(PieceType.Pawn, PlayerColor.White));

        //Elegimos promocionar a caballo
        Move move = new Move(from, to, PieceType.Knight);

        //ACT: aplicamos el movimiento
        Board newBoard = board.WithMove(move);

        //ASSERT: verificamos que ahora haya un caballo
        Piece? promotedPiece = newBoard.At(to);

        Assert.That(promotedPiece, Is.Not.Null);
        Assert.That(promotedPiece!.Value.Type,
            Is.EqualTo(PieceType.Knight));

        Assert.That(promotedPiece.Value.Color,
            Is.EqualTo(PlayerColor.White));

        //La casilla original debe quedar vacía
        Assert.That(newBoard.At(from), Is.Null);

        //El tablero original no debe modificarse
        Assert.That(board.At(from)?.Type,
            Is.EqualTo(PieceType.Pawn));
    }

}
