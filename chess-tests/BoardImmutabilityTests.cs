using Chess.Core.Model;
using NUnit.Framework;

namespace Chess.Tests;

public class BoardImmutabilityTests
{
    private static readonly Square E2 = new(4, 1);
    private static readonly Square E4 = new(4, 3);
    private static readonly Square D5 = new(3, 4);

    [Test]
    public void WithMove_returns_a_new_board_and_leaves_the_previous_one_unchanged()
    {
        var whitePawn = new Piece(PieceType.Pawn, PlayerColor.White);
        var original = Board.Empty().WithPiece(E2, whitePawn);

        var moved = original.WithMove(new Move(E2, E4));

        Assert.That(moved, Is.Not.SameAs(original));
        Assert.That(original.At(E2), Is.EqualTo(whitePawn));
        Assert.That(original.At(E4), Is.Null);
        Assert.That(moved.At(E2), Is.Null);
        Assert.That(moved.At(E4), Is.EqualTo(whitePawn));
    }

    [Test]
    public void WithMove_replaces_the_destination_without_touching_the_captured_piece_on_the_old_board()
    {
        var whitePawn = new Piece(PieceType.Pawn, PlayerColor.White);
        var blackPawn = new Piece(PieceType.Pawn, PlayerColor.Black);
        var original = Board.Empty()
            .WithPiece(E4, whitePawn)
            .WithPiece(D5, blackPawn);

        var moved = original.WithMove(new Move(E4, D5));

        Assert.That(original.At(E4), Is.EqualTo(whitePawn));
        Assert.That(original.At(D5), Is.EqualTo(blackPawn));
        Assert.That(moved.At(E4), Is.Null);
        Assert.That(moved.At(D5), Is.EqualTo(whitePawn));
    }

    [Test]
    public void WithPiece_and_metadata_changes_do_not_mutate_the_previous_board()
    {
        var king = new Piece(PieceType.King, PlayerColor.White);
        var original = Board.Empty().WithCastling(CastlingRights.All);
        var enPassant = new Square(4, 2);

        var placed = original.WithPiece(E2, king).WithEnPassant(enPassant).WithCastling(CastlingRights.None);

        Assert.That(original.At(E2), Is.Null);
        Assert.That(original.Castling, Is.EqualTo(CastlingRights.All));
        Assert.That(original.EnPassant, Is.Null);
        Assert.That(placed.At(E2), Is.EqualTo(king));
        Assert.That(placed.Castling, Is.EqualTo(CastlingRights.None));
        Assert.That(placed.EnPassant, Is.EqualTo(enPassant));
    }

    [Test]
    public void WithMove_keeps_castling_rights_and_en_passant_and_applies_promotion()
    {
        var pawn = new Piece(PieceType.Pawn, PlayerColor.White);
        var enPassant = new Square(4, 5);
        var original = Board.Empty()
            .WithPiece(E2, pawn)
            .WithCastling(CastlingRights.All)
            .WithEnPassant(enPassant);

        var moved = original.WithMove(new Move(E2, E4, PieceType.Queen));

        Assert.That(original.At(E2), Is.EqualTo(pawn));
        Assert.That(original.Castling, Is.EqualTo(CastlingRights.All));
        Assert.That(original.EnPassant, Is.EqualTo(enPassant));
        Assert.That(moved.At(E4), Is.EqualTo(new Piece(PieceType.Queen, PlayerColor.White)));
        Assert.That(moved.Castling, Is.EqualTo(CastlingRights.All));
        Assert.That(moved.EnPassant, Is.EqualTo(enPassant));
    }
}
