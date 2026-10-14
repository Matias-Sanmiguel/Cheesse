
using System;
using Chess.Core.Model;
using Chess.Core.Rules;
using NUnit.Framework;

namespace Chess.Tests;

public class CheckDetectorTests
{
    private static Board WithKings(Square white, Square black)
    {
        return Board.Empty()
            .WithPiece(
                white,
                new Piece(PieceType.King, PlayerColor.White))
            .WithPiece(
                black,
                new Piece(PieceType.King, PlayerColor.Black));
    }

    [Test]
    public void King_is_in_check_from_unblocked_rook()
    {
        // Rey blanco e1, torre negra e8.
        var board = WithKings(
                new Square(4, 0),
                new Square(0, 7))
            .WithPiece(
                new Square(4, 7),
                new Piece(PieceType.Rook, PlayerColor.Black));

        Assert.That(
            CheckDetector.IsInCheck(board, PlayerColor.White),
            Is.True);
    }

    [Test]
    public void King_is_not_in_check_when_attack_is_blocked()
    {
        // Una pieza blanca en e4 bloquea la torre negra.
        var board = WithKings(
                new Square(4, 0),
                new Square(0, 7))
            .WithPiece(
                new Square(4, 3),
                new Piece(PieceType.Pawn, PlayerColor.White))
            .WithPiece(
                new Square(4, 7),
                new Piece(PieceType.Rook, PlayerColor.Black));

        Assert.That(
            CheckDetector.IsInCheck(board, PlayerColor.White),
            Is.False);
    }

    [Test]
    public void Knight_can_give_check()
    {
        // Caballo negro f3 ataca al rey blanco e1.
        var board = WithKings(
                new Square(4, 0),
                new Square(0, 7))
            .WithPiece(
                new Square(5, 2),
                new Piece(PieceType.Knight, PlayerColor.Black));

        Assert.That(
            CheckDetector.IsInCheck(board, PlayerColor.White),
            Is.True);
    }

    [Test]
    public void Black_pawn_attacks_diagonally()
    {
        // Peon negro d5 ataca e4.
        var board = WithKings(
                new Square(4, 3),
                new Square(0, 7))
            .WithPiece(
                new Square(3, 4),
                new Piece(PieceType.Pawn, PlayerColor.Black));

        Assert.That(
            CheckDetector.IsInCheck(board, PlayerColor.White),
            Is.True);
    }

    [Test]
    public void Black_pawn_does_not_attack_straight_ahead()
    {
        // Peon negro d5 NO ataca d4.
        var board = WithKings(
                new Square(3, 3),
                new Square(0, 7))
            .WithPiece(
                new Square(3, 4),
                new Piece(PieceType.Pawn, PlayerColor.Black));

        Assert.That(
            CheckDetector.IsInCheck(board, PlayerColor.White),
            Is.False);
    }

    [Test]
    public void White_pawn_attacks_diagonally()
    {
        // Peon blanco d4 ataca al rey negro e5.
        var board = WithKings(
                new Square(0, 0),
                new Square(4, 4))
            .WithPiece(
                new Square(3, 3),
                new Piece(PieceType.Pawn, PlayerColor.White));

        Assert.That(
            CheckDetector.IsInCheck(board, PlayerColor.Black),
            Is.True);
    }

    [Test]
    public void Adjacent_kings_attack_each_other()
    {
        // Posicion invalida deliberada:
        // sirve para probar la deteccion de ataques.
        var board = WithKings(
            new Square(4, 3),
            new Square(5, 4));

        Assert.That(
            CheckDetector.IsInCheck(board, PlayerColor.White),
            Is.True);
    }

    [Test]
    public void Missing_king_throws_an_exception()
    {
        var board = Board.Empty();

        Assert.Throws<InvalidOperationException>(
            () => CheckDetector.IsInCheck(
                board,
                PlayerColor.White));
    }
}
