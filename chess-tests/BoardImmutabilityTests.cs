using Chess.Core.Model;
using NUnit.Framework;

namespace Chess.Tests;

public class BoardImmutabilityTests
{
    private static readonly Square E2 = new(4, 1);
    private static readonly Square E3 = new(4, 2);
    private static readonly Square E4 = new(4, 3);
    private static readonly Square D5 = new(3, 4);
    private static readonly Square E6 = new(4, 5);
    private static readonly Square E7 = new(4, 6);
    private static readonly Square E8 = new(4, 7);


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
        var enPassant = E3;

        var placed = original.WithPiece(E2, king).WithEnPassant(enPassant).WithCastling(CastlingRights.None);

        Assert.That(original.At(E2), Is.Null);
        Assert.That(original.Castling, Is.EqualTo(CastlingRights.All));
        Assert.That(original.EnPassant, Is.Null);
        Assert.That(placed.At(E2), Is.EqualTo(king));
        Assert.That(placed.Castling, Is.EqualTo(CastlingRights.None));
        Assert.That(placed.EnPassant, Is.EqualTo(enPassant));
    }

    [Test]
    public void WithMove_applies_promotion_without_modifying_original_board()
    {
        var pawn = new Piece(PieceType.Pawn, PlayerColor.White);

        var original = Board.Empty()
            .WithPiece(E7, pawn)
            .WithCastling(CastlingRights.All)
            .WithEnPassant(E6);

        var moved = original.WithMove(new Move(E7, E8, PieceType.Queen));

        //Primero vamos a comprobar la promoción
        Assert.That(moved.At(E8), Is.EqualTo(new Piece(PieceType.Queen, PlayerColor.White)));

        //Ahora vamos a comprobar la inmutabilidad
        Assert.That(moved, Is.Not.SameAs(original));
        Assert.That(original.At(E7), Is.EqualTo(pawn));
        Assert.That(original.At(E8), Is.Null);
        Assert.That(moved.At(E7), Is.Null);

        //El movimiento de peon no modifica los derechos de enroque
        Assert.That(original.Castling, Is.EqualTo(CastlingRights.All));
        Assert.That(moved.Castling, Is.EqualTo(CastlingRights.All));

        //El estado anterior no cambia, el nuevo pierde el enpassant
        Assert.That(original.EnPassant, Is.EqualTo(E6));
        Assert.That(moved.EnPassant, Is.Null);
    }
        [Test]
           public void WithMove_double_pawn_push_updates_en_passant_without_modifying_original()
           {
               var pawn = new Piece(PieceType.Pawn, PlayerColor.White);

               var original = Board.Empty()
                   .WithPiece(E2, pawn)
                   .WithCastling(CastlingRights.All)
                   .WithEnPassant(E6);

               var moved = original.WithMove(new Move(E2, E4));

               //El peon avanzó dos casillas
               Assert.That(moved.At(E2), Is.Null);
               Assert.That(moved.At(E4), Is.EqualTo(pawn));

               //El tablero original no cambia
               Assert.That(original.At(E2), Is.EqualTo(pawn));
               Assert.That(original.At(E4), Is.Null);
               Assert.That(original.EnPassant, Is.EqualTo(E6));

               //El nuevo tablero actualiza enpassant a E3
               Assert.That(moved.EnPassant, Is.EqualTo(E3));

               //El peon no modifica los derechos de enroque
               Assert.That(original.Castling, Is.EqualTo(CastlingRights.All));
               Assert.That(moved.Castling, Is.EqualTo(CastlingRights.All));
           }
    }
