
using Chess.Core.Model;
using Chess.Core.Rules;
using NUnit.Framework;
using Chess.Core.Services;

namespace Chess.Tests;

public class StalemateTests
{
    [Test]
    public void Black_is_stalemated_when_not_in_check_but_has_no_legal_moves()
    {
        // Posicion:
        // Rey negro: h8
        // Rey blanco: f7
        // Dama blanca: g6
        // Turno: negras

        var board = Board.Empty()
            .WithPiece(
                new Square(7, 7),
                new Piece(PieceType.King, PlayerColor.Black))
            .WithPiece(
                new Square(5, 6),
                new Piece(PieceType.King, PlayerColor.White))
            .WithPiece(
                new Square(6, 5),
                new Piece(PieceType.Queen, PlayerColor.White));

        // El rey negro NO esta en jaque.
        Assert.That(
            CheckDetector.IsInCheck(
                board,
                PlayerColor.Black),
            Is.False);

        // Pero tampoco puede realizar movimientos legales.
        Assert.That(
            MoveGenerator.Legal(
                board,
                PlayerColor.Black),
            Is.Empty);
        Assert.That(
            GameRules.Evaluate(board, PlayerColor.Black),
            Is.EqualTo(GameStatus.Stalemate));
    }
}
