
using Chess.Core.Model;
using Chess.Core.Services;
using NUnit.Framework;

namespace Chess.Tests;

public class GameRulesTests
{
    [Test]
    public void Check_when_king_is_attacked_but_can_escape()
    {
        // Rey blanco en e1.
        // Torre negra en e8: da jaque.
        // Rey negro en a8.
        // El rey blanco puede escapar.

        var board = Board.Empty()
            .WithPiece(
                new Square(4, 0),
                new Piece(PieceType.King, PlayerColor.White))
            .WithPiece(
                new Square(4, 7),
                new Piece(PieceType.Rook, PlayerColor.Black))
            .WithPiece(
                new Square(0, 7),
                new Piece(PieceType.King, PlayerColor.Black));

        var result = GameRules.Evaluate(
            board,
            PlayerColor.White);

        Assert.That(result, Is.EqualTo(GameStatus.Check));
    }

    [Test]
    public void Ongoing_when_king_is_safe_and_has_legal_moves()
    {
        // Solo dos reyes, separados.
        // El blanco puede moverse y no esta en jaque.

        var board = Board.Empty()
            .WithPiece(
                new Square(4, 0),
                new Piece(PieceType.King, PlayerColor.White))
            .WithPiece(
                new Square(0, 0),
                new Piece(PieceType.Rook, PlayerColor.White))
            .WithPiece(
                new Square(4, 7),
                new Piece(PieceType.King, PlayerColor.Black));

        var result = GameRules.Evaluate(
            board,
            PlayerColor.White);

        Assert.That(result, Is.EqualTo(GameStatus.Ongoing));
    }
}
