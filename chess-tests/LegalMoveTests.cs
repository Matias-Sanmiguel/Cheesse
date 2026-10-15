
using Chess.Core.Model;
using Chess.Core.Rules;
using NUnit.Framework;

namespace Chess.Tests;

public class LegalMoveTests
{
    [Test]
    public void Pinned_rook_cannot_expose_own_king()
    {
        // Rey blanco e1.
        // Torre blanca e2 protege al rey.
        // Torre negra e8 amenaza la columna e.

        var board = Board.Empty()
            .WithPiece(new Square(4, 0),
                new Piece(PieceType.King, PlayerColor.White))
            .WithPiece(new Square(4, 1),
                new Piece(PieceType.Rook, PlayerColor.White))
            .WithPiece(new Square(4, 7),
                new Piece(PieceType.Rook, PlayerColor.Black))
            .WithPiece(new Square(7, 7),
                new Piece(PieceType.King, PlayerColor.Black));

        var illegalMove = new Move(
            new Square(4, 1),
            new Square(3, 1)); // e2 -> d2

        var pseudoLegal = MoveGenerator.Generate(
            board, PlayerColor.White);

        var legal = MoveGenerator.Legal(
            board, PlayerColor.White);

        // Geometricamente puede moverse.
        Assert.That(pseudoLegal, Does.Contain(illegalMove));

        // Pero no puede dejar expuesto al rey.
        Assert.That(legal, Does.Not.Contain(illegalMove));
    }

    [Test]
    public void King_cannot_move_into_attacked_square()
    {
        // Rey blanco e1.
        // Torre negra d8 controla d1.

        var board = Board.Empty()
            .WithPiece(new Square(4, 0),
                new Piece(PieceType.King, PlayerColor.White))
            .WithPiece(new Square(3, 7),
                new Piece(PieceType.Rook, PlayerColor.Black))
            .WithPiece(new Square(7, 7),
                new Piece(PieceType.King, PlayerColor.Black));

        var illegalMove = new Move(
            new Square(4, 0),
            new Square(3, 0)); // e1 -> d1

        var legal = MoveGenerator.Legal(
            board, PlayerColor.White);

        Assert.That(legal, Does.Not.Contain(illegalMove));
    }

    [Test]
    public void Safe_move_remains_legal()
    {
        // No hay jaque ni clavadas.
        // La torre blanca puede avanzar de a1 a a2.

        var board = Board.Empty()
            .WithPiece(new Square(4, 0),
                new Piece(PieceType.King, PlayerColor.White))
            .WithPiece(new Square(0, 0),
                new Piece(PieceType.Rook, PlayerColor.White))
            .WithPiece(new Square(7, 7),
                new Piece(PieceType.King, PlayerColor.Black));

        var expectedMove = new Move(
            new Square(0, 0),
            new Square(0, 1)); // a1 -> a2

        var legal = MoveGenerator.Legal(
            board, PlayerColor.White);

        Assert.That(legal, Does.Contain(expectedMove));
    }

    [Test]
    public void Blocking_check_is_legal()
    {
        // Torre negra e8 da jaque al rey blanco e1.
        // La torre blanca a2 puede bloquear en e2.

        var board = Board.Empty()
            .WithPiece(new Square(4, 0),
                new Piece(PieceType.King, PlayerColor.White))
            .WithPiece(new Square(0, 1),
                new Piece(PieceType.Rook, PlayerColor.White))
            .WithPiece(new Square(4, 7),
                new Piece(PieceType.Rook, PlayerColor.Black))
            .WithPiece(new Square(0, 7),
                new Piece(PieceType.King, PlayerColor.Black));

        var blockingMove = new Move(
            new Square(0, 1),
            new Square(4, 1)); // a2 -> e2

        Assert.That(
            CheckDetector.IsInCheck(board, PlayerColor.White),
            Is.True);

        var legal = MoveGenerator.Legal(
            board, PlayerColor.White);

        Assert.That(legal, Does.Contain(blockingMove));
    }

    [Test]
    public void Legal_move_generation_does_not_mutate_board()
    {
        var kingSquare = new Square(4, 0);
        var rookSquare = new Square(0, 0);

        var whiteKing = new Piece(
            PieceType.King, PlayerColor.White);

        var whiteRook = new Piece(
            PieceType.Rook, PlayerColor.White);

        var board = Board.Empty()
            .WithPiece(kingSquare, whiteKing)
            .WithPiece(rookSquare, whiteRook)
            .WithPiece(new Square(7, 7),
                new Piece(PieceType.King, PlayerColor.Black));

        MoveGenerator.Legal(board, PlayerColor.White);

        // La simulacion no debe modificar el tablero original.
        Assert.That(board.At(kingSquare), Is.EqualTo(whiteKing));
        Assert.That(board.At(rookSquare), Is.EqualTo(whiteRook));
        Assert.That(board.At(new Square(0, 1)), Is.Null);
    }
}
