
using Chess.Core.Model;
using Chess.Core.Rules;
using NUnit.Framework;
using Chess.Core.Services;

namespace Chess.Tests;

public class CheckmateTests
{
    [Test]
    public void Black_is_checkmated_when_queen_covers_all_escape_squares()
    {
        // Posicion:
        // Rey negro: h8
        // Dama blanca: g7
        // Rey blanco: f6
        // Turno: negras

        var board = Board.Empty()
            .WithPiece(
                new Square(7, 7),
                new Piece(PieceType.King, PlayerColor.Black))
            .WithPiece(
                new Square(6, 6),
                new Piece(PieceType.Queen, PlayerColor.White))
            .WithPiece(
                new Square(5, 5),
                new Piece(PieceType.King, PlayerColor.White));

        // El rey negro esta en jaque.
        Assert.That(
            CheckDetector.IsInCheck(
                board,
                PlayerColor.Black),
            Is.True);

        // No existe ninguna jugada legal para escapar.
        Assert.That(
            MoveGenerator.Legal(
                board,
                PlayerColor.Black),
            Is.Empty);
        Assert.That(
            GameRules.Evaluate(board, PlayerColor.Black),
            Is.EqualTo(GameStatus.Checkmate));
    }

}
