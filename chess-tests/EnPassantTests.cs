using Chess.Core.Model;
using Chess.Core.Rules;
using NUnit.Framework;

namespace Chess.Tests;

public class EnPassantTests
{
    [Test]
    public void WhitePawn_ShouldGenerateEnPassant()
    {
        //ARRANGE: preparamos el tablero
        Square whitePawn = new Square(4, 4); // e5
        Square blackPawn = new Square(3, 4); // d5
        Square target = new Square(3, 5);    // d6

        //Creamos el tablero con sus 2 peones y la casilla al paso 
        Board board = Board.Empty().WithPiece(whitePawn, new Piece(PieceType.Pawn, PlayerColor.White)).WithPiece(blackPawn, new Piece(PieceType.Pawn, PlayerColor.Black))
        .WithEnPassant(target); //Indicamos que existe una oportunidad de captura al paso

        //ACT: buscamos capturas al paso disponibles (ejecutamos)
        var moves = SpecialMoves.EnPassant(board, PlayerColor.White);

        //ASSERT: debe existir exactamente un movimiento
        Assert.That(moves, Has.Count.EqualTo(1));
        Assert.That(moves[0].From, Is.EqualTo(whitePawn));
        Assert.That(moves[0].To, Is.EqualTo(target));
    }

    [Test] //Mismo test pero para las negras
    public void BlackPawn_ShouldGenerateEnPassant()
    {
        //ARRANGE: preparamos el tablero
        Square blackPawn = new Square(4, 3); // e4
        Square whitePawn = new Square(3, 3); // d4
        Square target = new Square(3, 2);    // d3

        Board board = Board.Empty().WithPiece(blackPawn, new Piece(PieceType.Pawn, PlayerColor.Black)).WithPiece(whitePawn, new Piece(PieceType.Pawn, PlayerColor.White))
        .WithEnPassant(target);

        //ACT: buscamos capturas al paso para negras
        var moves = SpecialMoves.EnPassant(
            board, PlayerColor.Black
        );

        //ASSERT
        Assert.That(moves, Has.Count.EqualTo(1));
        Assert.That(moves[0].From, Is.EqualTo(blackPawn));
        Assert.That(moves[0].To, Is.EqualTo(target));
    }

    [Test] //Comprobamos que se elimine el peón luego de la captura al paso
    public void EnPassant_ShouldRemoveCapturedPawn()
    {
        //ARRANGE: peón blanco en e5, negro en d5
        Square from = new Square(4, 4);           // e5
        Square capturedSquare = new Square(3, 4); // d5
        Square target = new Square(3, 5);         // d6

        Board board = Board.Empty().WithPiece(from, new Piece(PieceType.Pawn, PlayerColor.White)).WithPiece(capturedSquare, new Piece(PieceType.Pawn, PlayerColor.Black))
        .WithEnPassant(target);

        Move move = new Move(from, target);

        //ACT: aplicamos la captura al paso
        Board newBoard = board.WithMove(move);

        //ASSERT: el peón blanco terminó en d6
        Assert.That(newBoard.At(target), Is.EqualTo(new Piece(PieceType.Pawn, PlayerColor.White)));

        //e5 quedó vacía
        Assert.That(newBoard.At(from), Is.Null);

        //El peón negro de d5 fue eliminado
        Assert.That(newBoard.At(capturedSquare), Is.Null);

        //Ya no existe oportunidad de captura al paso
        Assert.That(newBoard.EnPassant, Is.Null);

        //El tablero original permanece intacto
        Assert.That(board.At(capturedSquare), Is.EqualTo(new Piece(PieceType.Pawn, PlayerColor.Black)));
    }

    [Test] //Comprobamos el doble avance del peón blanco
    public void WhitePawnDoubleMove_ShouldSetEnPassant() {

        //ARRANGE: peón blanco en e2
        Square from = new Square(4, 1);   // e2
        Square to = new Square(4, 3);     // e4
        Square expected = new Square(4, 2); // e3

        Board board = Board.Empty().WithPiece(from, new Piece(PieceType.Pawn, PlayerColor.White));

        Move move = new Move(from, to);

        //ACT: avanzamos el peón dos casillas
        Board newBoard = board.WithMove(move);

        //ASSERT: debe guardarse e3 como casilla de al paso
        Assert.That(newBoard.EnPassant, Is.EqualTo(expected));

        //Verificamos que el peón llegó a e4
        Assert.That(newBoard.At(to), Is.EqualTo(new Piece(PieceType.Pawn, PlayerColor.White)));

        //El tablero original no se modifica
        Assert.That(board.EnPassant, Is.Null);
    }

    [Test] //Mismo test pero para el peón negro
    public void BlackPawnDoubleMove_ShouldSetEnPassant() {

        //ARRANGE: peón negro en d7
        Square from = new Square(3, 6); // d7
        Square to = new Square(3, 4);   // d5
        Square expected = new Square(3, 5); // d6

        Board board = Board.Empty().WithPiece(from, new Piece(PieceType.Pawn, PlayerColor.Black));

        //ACT
        Board newBoard = board.WithMove(new Move(from, to));

        //ASSERT
        Assert.That(newBoard.EnPassant, Is.EqualTo(expected));
        Assert.That(newBoard.At(to), Is.EqualTo(new Piece(PieceType.Pawn, PlayerColor.Black)));
    }

    [Test] //Comprobamos que la captura al paso expire, si el rival no captura inmediatamente al paso, pierde la oportunidad
    public void EnPassant_ShouldExpireAfterAnotherMove() {

        //ARRANGE: peón blanco en e2 y caballo negro en g8
        Square pawnFrom = new Square(4, 1);
        Square pawnTo = new Square(4, 3);

        Square knightFrom = new Square(6, 7);
        Square knightTo = new Square(5, 5);

        Board board = Board.Empty().WithPiece(pawnFrom, new Piece(PieceType.Pawn, PlayerColor.White)).WithPiece(knightFrom, new Piece(PieceType.Knight, PlayerColor.Black));

        //ACT: el blanco avanza dos casillas
        Board afterPawnMove = board.WithMove(new Move(pawnFrom, pawnTo));

        //Verificamos que se guardó e3
        Assert.That(afterPawnMove.EnPassant, Is.EqualTo(new Square(4, 2)));

        //El negro mueve su caballo en vez de capturar al paso
        Board afterKnightMove = afterPawnMove.WithMove(new Move(knightFrom, knightTo));

        //ASSERT: la oportunidad anterior desapareció
        Assert.That(afterKnightMove.EnPassant, Is.Null);

        //El tablero anterior sigue conservando e3
        Assert.That(afterPawnMove.EnPassant, Is.EqualTo(new Square(4, 2)));
    }

    [Test] //Probamos que pasa si no existe el peon rival
    public void EnPassant_ShouldNotGenerateWithoutEnemyPawn() {

        //ARRANGE: peón blanco en e5
        Square whitePawn = new Square(4, 4);
        Square target = new Square(3, 5); // d6

        Board board = Board.Empty().WithPiece(whitePawn, new Piece(PieceType.Pawn, PlayerColor.White)).WithEnPassant(target);

        //No colocamos ningún peón negro en d5
        //ACT
        var moves = SpecialMoves.EnPassant(board, PlayerColor.White);

        //ASSERT: no debe generar capturas
        Assert.That(moves, Is.Empty);
    }

    [Test] //Comprobamos que ocurre si la casilla de destino de la captura al pasoestá ocupada
    public void EnPassant_ShouldNotGenerateWhenTargetIsOccupied() {

        //ARRANGE
        Square whitePawn = new Square(4, 4); // e5
        Square blackPawn = new Square(3, 4); // d5
        Square target = new Square(3, 5);    // d6

        Board board = Board.Empty().WithPiece(whitePawn, new Piece(PieceType.Pawn, PlayerColor.White)).WithPiece(blackPawn, new Piece(PieceType.Pawn, PlayerColor.Black))
        .WithPiece(target, new Piece(PieceType.Rook, PlayerColor.Black)).WithEnPassant(target);

        //ACT
        var moves = SpecialMoves.EnPassant(board, PlayerColor.White);

        //ASSERT
        Assert.That(moves, Is.Empty);
    }

}
