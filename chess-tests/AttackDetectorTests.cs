using Chess.Core.Model;
using Chess.Core.Rules;
using NUnit.Framework;

namespace Chess.Tests;

public class AttackDetectorTests
{
    [TestCase(PlayerColor.White)]
    [TestCase(PlayerColor.Black)]
    public void Empty_board_has_no_attacked_squares(PlayerColor by)
    {
        for (var index = 0; index < 64; index++)
            AssertAttack(Board.Empty(), Square.FromIndex(index), by, false);
    }

    [TestCase(PlayerColor.White, 3, 4, 2)]
    [TestCase(PlayerColor.Black, 4, 3, 5)]
    public void Pawn_attacks_empty_forward_diagonals_but_not_forward_or_backward(
        PlayerColor by, int rank, int forwardRank, int backwardRank)
    {
        var board = Board.Empty().WithPiece(new Square(3, rank), new Piece(PieceType.Pawn, by));

        AssertAttack(board, new Square(2, forwardRank), by, true);
        AssertAttack(board, new Square(4, forwardRank), by, true);
        AssertAttack(board, new Square(3, forwardRank), by, false);
        AssertAttack(board, new Square(3, rank), by, false);
        AssertAttack(board, new Square(2, backwardRank), by, false);
        AssertAttack(board, new Square(4, backwardRank), by, false);
        AssertAttack(board, new Square(2, forwardRank), Opponent(by), false);
    }

    [TestCase(PlayerColor.White, 0, 1)]
    [TestCase(PlayerColor.White, 7, 1)]
    [TestCase(PlayerColor.Black, 0, 6)]
    [TestCase(PlayerColor.Black, 7, 6)]
    public void Pawn_attacks_at_board_edges_depend_only_on_geometry(
        PlayerColor by, int file, int rank)
    {
        var direction = by == PlayerColor.White ? 1 : -1;
        var target = new Square(file == 0 ? 1 : 6, rank + direction);
        var board = Board.Empty().WithPiece(new Square(file, rank), new Piece(PieceType.Pawn, by));

        AssertAttack(board, target, by, true);
        AssertAttack(board.WithPiece(target, new Piece(PieceType.Pawn, by)), target, by, true);
        AssertAttack(board.WithPiece(target, new Piece(PieceType.Pawn, Opponent(by))), target, by, true);
    }

    [TestCase(PlayerColor.White)]
    [TestCase(PlayerColor.Black)]
    public void Knight_attacks_all_eight_targets_despite_adjacent_blockers(PlayerColor by)
    {
        var board = Board.Empty().WithPiece(S("d4"), new Piece(PieceType.Knight, by));
        foreach (var square in new[] { "c3", "c4", "c5", "d3", "d5", "e3", "e4", "e5" })
            board = board.WithPiece(S(square), new Piece(PieceType.Pawn, Opponent(by)));

        AssertAttackedSquares(board, by, "b3 b5 c2 c6 e2 e6 f3 f5");
    }

    [TestCase(PieceType.Bishop, PlayerColor.White, "a1 b2 c3 c5 b6 a7 e3 f2 g1 e5 f6 g7 h8")]
    [TestCase(PieceType.Bishop, PlayerColor.Black, "a1 b2 c3 c5 b6 a7 e3 f2 g1 e5 f6 g7 h8")]
    [TestCase(PieceType.Rook, PlayerColor.White, "a4 b4 c4 e4 f4 g4 h4 d1 d2 d3 d5 d6 d7 d8")]
    [TestCase(PieceType.Rook, PlayerColor.Black, "a4 b4 c4 e4 f4 g4 h4 d1 d2 d3 d5 d6 d7 d8")]
    [TestCase(PieceType.Queen, PlayerColor.White, "a1 b2 c3 c5 b6 a7 e3 f2 g1 e5 f6 g7 h8 a4 b4 c4 e4 f4 g4 h4 d1 d2 d3 d5 d6 d7 d8")]
    [TestCase(PieceType.Queen, PlayerColor.Black, "a1 b2 c3 c5 b6 a7 e3 f2 g1 e5 f6 g7 h8 a4 b4 c4 e4 f4 g4 h4 d1 d2 d3 d5 d6 d7 d8")]
    [TestCase(PieceType.King, PlayerColor.White, "c3 c4 c5 d3 d5 e3 e4 e5")]
    [TestCase(PieceType.King, PlayerColor.Black, "c3 c4 c5 d3 d5 e3 e4 e5")]
    public void Piece_attacks_exact_geometric_targets_on_empty_board(
        PieceType type, PlayerColor by, string expected)
    {
        var board = Board.Empty().WithPiece(S("d4"), new Piece(type, by));

        AssertAttackedSquares(board, by, expected);
    }

    [TestCase(PieceType.Bishop, PlayerColor.White, "f6", "e5", "g7")]
    [TestCase(PieceType.Bishop, PlayerColor.Black, "b2", "c3", "a1")]
    [TestCase(PieceType.Rook, PlayerColor.White, "f4", "e4", "g4")]
    [TestCase(PieceType.Rook, PlayerColor.Black, "d2", "d3", "d1")]
    [TestCase(PieceType.Queen, PlayerColor.White, "b6", "c5", "a7")]
    [TestCase(PieceType.Queen, PlayerColor.Black, "d6", "d5", "d7")]
    public void Sliding_attack_stops_after_first_piece_of_either_color(
        PieceType type, PlayerColor by, string blockerSquare, string before, string beyond)
    {
        foreach (var blockerColor in new[] { PlayerColor.White, PlayerColor.Black })
        {
            var board = Board.Empty()
                .WithPiece(S("d4"), new Piece(type, by))
                .WithPiece(S(blockerSquare), new Piece(PieceType.Knight, blockerColor));

            AssertAttack(board, S(before), by, true);
            AssertAttack(board, S(blockerSquare), by, true);
            AssertAttack(board, S(beyond), by, false);
        }
    }

    [TestCase(PlayerColor.White)]
    [TestCase(PlayerColor.Black)]
    public void Kings_attack_each_other_and_adjacent_empty_squares(PlayerColor by)
    {
        var board = Board.Empty()
            .WithPiece(S("a1"), new Piece(PieceType.King, by))
            .WithPiece(S("b2"), new Piece(PieceType.King, Opponent(by)));

        AssertAttackedSquares(board, by, "a2 b1 b2");
        AssertAttack(board, S("a1"), Opponent(by), true);
    }

    [TestCase(PlayerColor.White)]
    [TestCase(PlayerColor.Black)]
    public void Pinned_knight_still_attacks_geometrically(PlayerColor by)
    {
        var board = Board.Empty()
            .WithPiece(S("e1"), new Piece(PieceType.King, by))
            .WithPiece(S("e2"), new Piece(PieceType.Knight, by))
            .WithPiece(S("e8"), new Piece(PieceType.Rook, Opponent(by)));

        AssertAttack(board, S("c3"), by, true);
        AssertAttack(board, S("f4"), by, true);
    }

    [Test]
    public void Detector_continues_past_non_attacking_pieces_of_requested_color()
    {
        var board = Board.Empty()
            .WithPiece(S("a1"), new Piece(PieceType.Pawn, PlayerColor.White))
            .WithPiece(S("d4"), new Piece(PieceType.Rook, PlayerColor.White));

        AssertAttack(board, S("d8"), PlayerColor.White, true);
        AssertAttack(board, S("h7"), PlayerColor.White, false);
    }

    private static void AssertAttackedSquares(Board board, PlayerColor by, string expected)
    {
        var attacked = Enumerable.Range(0, 64)
            .Select(Square.FromIndex)
            .Where(square => AttackDetector.IsAttacked(board, square, by))
            .ToArray();

        Assert.That(attacked, Is.EquivalentTo(expected.Split(' ').Select(S)));
        foreach (var square in Enumerable.Range(0, 64).Select(Square.FromIndex))
            Assert.That(board.IsAttacked(square, by),
                Is.EqualTo(AttackDetector.IsAttacked(board, square, by)), square.ToString());
    }

    private static void AssertAttack(Board board, Square target, PlayerColor by, bool expected)
    {
        Assert.That(AttackDetector.IsAttacked(board, target, by), Is.EqualTo(expected), target.ToString());
        Assert.That(board.IsAttacked(target, by), Is.EqualTo(expected), target.ToString());
    }

    private static Square S(string square) => new(square[0] - 'a', square[1] - '1');

    private static PlayerColor Opponent(PlayerColor side) =>
        side == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
}
