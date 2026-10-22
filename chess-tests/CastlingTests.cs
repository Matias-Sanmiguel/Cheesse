
using Chess.Core.Model;
using Chess.Core.Rules;
using NUnit.Framework;

namespace Chess.Tests;

[TestFixture]
public class CastlingTests
{
    [TestCase(PlayerColor.White, 0)]
    [TestCase(PlayerColor.Black, 7)]
    public void Kingside_castling_moves_king_and_rook(PlayerColor side, int rank)
    {
        var kingFrom = new Square(4, rank);
        var kingTo = new Square(6, rank);
        var rookFrom = new Square(7, rank);
        var rookTo = new Square(5, rank);
        var board = Board.Empty().WithPiece(kingFrom, new Piece(PieceType.King, side)).WithPiece(rookFrom, new Piece(PieceType.Rook, side)).WithCastling(CastlingRights.All);
        var move = new Move(kingFrom, kingTo);

        Assert.That(SpecialMoves.Castling(board, side),Does.Contain(move));

        var result = board.WithMove(move);

        Assert.Multiple(() =>
        {
            Assert.That(result.At(kingTo), Is.EqualTo(new Piece(PieceType.King, side)));
            Assert.That(result.At(rookTo), Is.EqualTo(new Piece(PieceType.Rook, side)));
            Assert.That(result.At(kingFrom), Is.Null);
            Assert.That(result.At(rookFrom), Is.Null);
        });
    }

    [TestCase(PlayerColor.White, 0)]
    [TestCase(PlayerColor.Black, 7)]
    public void Queenside_castling_moves_king_and_rook(PlayerColor side, int rank)
    {
        var kingFrom = new Square(4, rank);
        var kingTo = new Square(2, rank);
        var rookFrom = new Square(0, rank);
        var rookTo = new Square(3, rank);

        var board = Board.Empty().WithPiece(kingFrom, new Piece(PieceType.King, side)).WithPiece(rookFrom, new Piece(PieceType.Rook, side)).WithCastling(CastlingRights.All);
        var move = new Move(kingFrom, kingTo);

        Assert.That(SpecialMoves.Castling(board, side), Does.Contain(move));

        var result = board.WithMove(move);

        Assert.Multiple(() =>
        {
            Assert.That(result.At(kingTo), Is.EqualTo(new Piece(PieceType.King, side)));
            Assert.That(result.At(rookTo), Is.EqualTo(new Piece(PieceType.Rook, side)));
            Assert.That(result.At(kingFrom), Is.Null);
            Assert.That(result.At(rookFrom), Is.Null);
        });
    }

    [Test]
    public void Castling_is_not_allowed_without_rights()
    {
        var board = Board.Empty().WithPiece(new Square(4, 0), new Piece(PieceType.King, PlayerColor.White)).WithPiece(new Square(7, 0), new Piece(PieceType.Rook, PlayerColor.White));

        Assert.That(SpecialMoves.Castling(board, PlayerColor.White), Is.Empty);
    }

    [Test]
    public void Castling_is_not_allowed_through_attack()
    {
        var board = Board.Empty().WithPiece(new Square(4, 0), new Piece(PieceType.King, PlayerColor.White)).WithPiece(new Square(7, 0), new Piece(PieceType.Rook, PlayerColor.White)).WithPiece(new Square(5, 7), new Piece(PieceType.Rook, PlayerColor.Black)).WithCastling(CastlingRights.All);

        Assert.That(SpecialMoves.Castling(board, PlayerColor.White), Does.Not.Contain(new Move(new Square(4, 0), new Square(6, 0))));
    }


    [Test]
    public void Moving_king_removes_both_castling_rights()
    {
        var board = Board.Empty().WithPiece(new Square(4, 0), new Piece(PieceType.King, PlayerColor.White)).WithCastling(CastlingRights.All);

        var result = board.WithMove(new Move(new Square(4, 0), new Square(4, 1)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Castling.WhiteKingside, Is.False);
            Assert.That(result.Castling.WhiteQueenside, Is.False);
            Assert.That(result.Castling.BlackKingside, Is.True);
            Assert.That(result.Castling.BlackQueenside, Is.True);
        });
    }

    [Test]
    public void Moving_rook_removes_its_castling_right()
    {
        var board = Board.Empty().WithPiece(new Square(7, 0), new Piece(PieceType.Rook, PlayerColor.White)).WithCastling(CastlingRights.All);

        var result = board.WithMove(new Move(new Square(7, 0), new Square(7, 1)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Castling.WhiteKingside, Is.False);
            Assert.That(result.Castling.WhiteQueenside, Is.True);
            Assert.That(result.Castling.BlackKingside, Is.True);
            Assert.That(result.Castling.BlackQueenside, Is.True);
        });
    }

}
