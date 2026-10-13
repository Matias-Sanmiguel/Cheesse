using Chess.Core.Model;
using Chess.Core.Rules;
using NUnit.Framework;

namespace Chess.Tests;

public class MoveGeneratorTests
{
    [TestCase(PlayerColor.White)]
    [TestCase(PlayerColor.Black)]
    public void Empty_board_has_no_moves(PlayerColor side)
    {
        Assert.That(MoveGenerator.Generate(Board.Empty(), side), Is.Empty);
    }

    [TestCase(PlayerColor.White, 1, 2, 3)]
    [TestCase(PlayerColor.Black, 6, 5, 4)]
    public void Pawn_can_advance_one_or_two_squares_from_start(
        PlayerColor side, int startingRank, int nextRank, int doubleRank)
    {
        var from = new Square(4, startingRank);
        var board = Board.Empty().WithPiece(from, new Piece(PieceType.Pawn, side));

        AssertDestinations(board, side, from, new Square(4, nextRank), new Square(4, doubleRank));
    }

    [TestCase(PlayerColor.White, 3, 4)]
    [TestCase(PlayerColor.Black, 4, 3)]
    public void Pawn_away_from_start_cannot_advance_twice(PlayerColor side, int rank, int nextRank)
    {
        var from = new Square(4, rank);
        var board = Board.Empty().WithPiece(from, new Piece(PieceType.Pawn, side));

        AssertDestinations(board, side, from, new Square(4, nextRank));
    }

    [TestCase(PlayerColor.White, PlayerColor.White, 1, 2, 3)]
    [TestCase(PlayerColor.White, PlayerColor.Black, 1, 2, 3)]
    [TestCase(PlayerColor.Black, PlayerColor.White, 6, 5, 4)]
    [TestCase(PlayerColor.Black, PlayerColor.Black, 6, 5, 4)]
    public void Pawn_forward_moves_require_both_squares_to_be_empty(
        PlayerColor side, PlayerColor blockerColor, int rank, int nextRank, int doubleRank)
    {
        var from = new Square(4, rank);
        var board = Board.Empty().WithPiece(from, new Piece(PieceType.Pawn, side));
        var blocker = new Piece(PieceType.Knight, blockerColor);

        AssertDestinations(board.WithPiece(new Square(4, nextRank), blocker), side, from);
        AssertDestinations(board.WithPiece(new Square(4, doubleRank), blocker), side, from,
            new Square(4, nextRank));
    }

    [TestCase(PlayerColor.White, 3, 4)]
    [TestCase(PlayerColor.Black, 4, 3)]
    public void Pawn_captures_only_enemies_on_forward_diagonals(
        PlayerColor side, int rank, int nextRank)
    {
        var from = new Square(4, rank);
        var left = new Square(3, nextRank);
        var right = new Square(5, nextRank);
        var forward = new Square(4, nextRank);
        var board = Board.Empty()
            .WithPiece(from, new Piece(PieceType.Pawn, side))
            .WithPiece(left, new Piece(PieceType.Bishop, Opponent(side)))
            .WithPiece(right, new Piece(PieceType.Knight, side))
            .WithPiece(forward, new Piece(PieceType.Rook, Opponent(side)));

        AssertDestinations(board, side, from, left);
        AssertDestinations(board.WithPiece(right, null).WithPiece(forward, null), side, from,
            left, forward);
        AssertDestinations(board.WithPiece(right, new Piece(PieceType.Knight, Opponent(side))),
            side, from, left, right);
    }

    [TestCase(PlayerColor.White, 0, 6, 1, 7)]
    [TestCase(PlayerColor.White, 7, 6, 6, 7)]
    [TestCase(PlayerColor.Black, 0, 1, 1, 0)]
    [TestCase(PlayerColor.Black, 7, 1, 6, 0)]
    public void Pawn_at_file_edge_stays_on_board_and_leaves_promotion_pending(
        PlayerColor side, int file, int rank, int captureFile, int nextRank)
    {
        var from = new Square(file, rank);
        var capture = new Square(captureFile, nextRank);
        var board = Board.Empty()
            .WithPiece(from, new Piece(PieceType.Pawn, side))
            .WithPiece(capture, new Piece(PieceType.Rook, Opponent(side)));

        AssertDestinations(board, side, from, new Square(file, nextRank), capture);
        Assert.That(MoveGenerator.Generate(board, side).All(move => move.Promotion is null), Is.True);
    }

    [TestCase(PlayerColor.White, 7)]
    [TestCase(PlayerColor.Black, 0)]
    public void Pawn_on_last_rank_has_no_moves(PlayerColor side, int rank)
    {
        var from = new Square(4, rank);
        var board = Board.Empty().WithPiece(from, new Piece(PieceType.Pawn, side));

        AssertDestinations(board, side, from);
    }

    [TestCase(PlayerColor.White)]
    [TestCase(PlayerColor.Black)]
    public void Knight_has_eight_destinations_and_jumps_over_adjacent_pieces(PlayerColor side)
    {
        var board = Board.Empty().WithPiece(S("d4"), new Piece(PieceType.Knight, side));
        foreach (var square in new[] { "c3", "c4", "c5", "d3", "d5", "e3", "e4", "e5" })
            board = board.WithPiece(S(square), new Piece(PieceType.Pawn, side));

        AssertDestinations(board, side, S("d4"),
            S("b3"), S("b5"), S("c2"), S("c6"), S("e2"), S("e6"), S("f3"), S("f5"));
    }

    [TestCase(PlayerColor.White)]
    [TestCase(PlayerColor.Black)]
    public void Knight_at_corner_captures_enemy_and_excludes_friendly_target(PlayerColor side)
    {
        var board = Board.Empty()
            .WithPiece(S("a1"), new Piece(PieceType.Knight, side))
            .WithPiece(S("b3"), new Piece(PieceType.Pawn, side))
            .WithPiece(S("c2"), new Piece(PieceType.Pawn, Opponent(side)));

        AssertDestinations(board, side, S("a1"), S("c2"));
    }

    [TestCase(PlayerColor.White)]
    [TestCase(PlayerColor.Black)]
    public void Bishop_moves_on_all_diagonals_until_first_obstacle(PlayerColor side)
    {
        var board = Board.Empty()
            .WithPiece(S("d4"), new Piece(PieceType.Bishop, side))
            .WithPiece(S("b2"), new Piece(PieceType.Pawn, side))
            .WithPiece(S("b6"), new Piece(PieceType.Pawn, Opponent(side)))
            .WithPiece(S("f2"), new Piece(PieceType.Pawn, Opponent(side)))
            .WithPiece(S("f6"), new Piece(PieceType.Pawn, side));

        AssertDestinations(board, side, S("d4"),
            S("c3"), S("c5"), S("b6"), S("e3"), S("f2"), S("e5"));
    }

    [TestCase(PlayerColor.White)]
    [TestCase(PlayerColor.Black)]
    public void Rook_moves_on_all_straight_lines_until_first_obstacle(PlayerColor side)
    {
        var board = Board.Empty()
            .WithPiece(S("d4"), new Piece(PieceType.Rook, side))
            .WithPiece(S("b4"), new Piece(PieceType.Pawn, side))
            .WithPiece(S("f4"), new Piece(PieceType.Pawn, Opponent(side)))
            .WithPiece(S("d2"), new Piece(PieceType.Pawn, Opponent(side)))
            .WithPiece(S("d6"), new Piece(PieceType.Pawn, side));

        AssertDestinations(board, side, S("d4"),
            S("c4"), S("e4"), S("f4"), S("d3"), S("d2"), S("d5"));
    }

    [TestCase(PlayerColor.White)]
    [TestCase(PlayerColor.Black)]
    public void Queen_combines_straight_and_diagonal_moves_with_blockers(PlayerColor side)
    {
        var board = Board.Empty()
            .WithPiece(S("d4"), new Piece(PieceType.Queen, side))
            .WithPiece(S("b2"), new Piece(PieceType.Pawn, side))
            .WithPiece(S("b6"), new Piece(PieceType.Pawn, Opponent(side)))
            .WithPiece(S("f2"), new Piece(PieceType.Pawn, Opponent(side)))
            .WithPiece(S("f6"), new Piece(PieceType.Pawn, side))
            .WithPiece(S("b4"), new Piece(PieceType.Pawn, side))
            .WithPiece(S("f4"), new Piece(PieceType.Pawn, Opponent(side)))
            .WithPiece(S("d2"), new Piece(PieceType.Pawn, Opponent(side)))
            .WithPiece(S("d6"), new Piece(PieceType.Pawn, side));

        AssertDestinations(board, side, S("d4"),
            S("c3"), S("c5"), S("b6"), S("e3"), S("f2"), S("e5"),
            S("c4"), S("e4"), S("f4"), S("d3"), S("d2"), S("d5"));
    }

    [TestCase(PieceType.Bishop, PlayerColor.White, "b2 c3 d4 e5 f6 g7 h8")]
    [TestCase(PieceType.Bishop, PlayerColor.Black, "b2 c3 d4 e5 f6 g7 h8")]
    [TestCase(PieceType.Rook, PlayerColor.White, "b1 c1 d1 e1 f1 g1 h1 a2 a3 a4 a5 a6 a7 a8")]
    [TestCase(PieceType.Rook, PlayerColor.Black, "b1 c1 d1 e1 f1 g1 h1 a2 a3 a4 a5 a6 a7 a8")]
    [TestCase(PieceType.Queen, PlayerColor.White, "b2 c3 d4 e5 f6 g7 h8 b1 c1 d1 e1 f1 g1 h1 a2 a3 a4 a5 a6 a7 a8")]
    [TestCase(PieceType.Queen, PlayerColor.Black, "b2 c3 d4 e5 f6 g7 h8 b1 c1 d1 e1 f1 g1 h1 a2 a3 a4 a5 a6 a7 a8")]
    public void Sliding_piece_on_corner_reaches_board_edges(
        PieceType type, PlayerColor side, string destinations)
    {
        var board = Board.Empty().WithPiece(S("a1"), new Piece(type, side));

        AssertDestinations(board, side, S("a1"), destinations.Split(' ').Select(S).ToArray());
    }

    [TestCase(PlayerColor.White)]
    [TestCase(PlayerColor.Black)]
    public void King_moves_one_square_and_can_capture_enemies(PlayerColor side)
    {
        var board = Board.Empty()
            .WithPiece(S("d4"), new Piece(PieceType.King, side))
            .WithPiece(S("c3"), new Piece(PieceType.Pawn, side))
            .WithPiece(S("e5"), new Piece(PieceType.Pawn, Opponent(side)));

        AssertDestinations(board, side, S("d4"),
            S("c4"), S("c5"), S("d3"), S("d5"), S("e3"), S("e4"), S("e5"));
    }

    [TestCase(PlayerColor.White)]
    [TestCase(PlayerColor.Black)]
    public void King_at_corner_stays_on_board(PlayerColor side)
    {
        var board = Board.Empty().WithPiece(S("h8"), new Piece(PieceType.King, side));

        AssertDestinations(board, side, S("h8"), S("g7"), S("g8"), S("h7"));
    }

    [TestCase(PlayerColor.White)]
    [TestCase(PlayerColor.Black)]
    public void Only_requested_color_generates_moves(PlayerColor side)
    {
        var board = Board.Empty()
            .WithPiece(S("a1"), new Piece(PieceType.Knight, side))
            .WithPiece(S("h8"), new Piece(PieceType.Knight, Opponent(side)));

        Assert.That(MoveGenerator.Generate(board, side),
            Is.EquivalentTo(new[] { new Move(S("a1"), S("b3")), new Move(S("a1"), S("c2")) }));
    }

    [TestCase(PlayerColor.White, 0, 1, 7)]
    [TestCase(PlayerColor.Black, 7, 6, 0)]
    public void Pinned_piece_can_expose_its_king_and_king_can_enter_attack(
        PlayerColor side, int kingRank, int rookRank, int enemyRank)
    {
        var king = new Square(4, kingRank);
        var rook = new Square(4, rookRank);
        var board = Board.Empty()
            .WithPiece(king, new Piece(PieceType.King, side))
            .WithPiece(rook, new Piece(PieceType.Rook, side))
            .WithPiece(new Square(4, enemyRank), new Piece(PieceType.Rook, Opponent(side)))
            .WithPiece(new Square(3, enemyRank), new Piece(PieceType.Rook, Opponent(side)));
        var exposesKing = new Move(rook, new Square(5, rookRank));
        var entersAttack = new Move(king, new Square(3, kingRank));

        Assert.That(board.WithMove(exposesKing).IsAttacked(king, Opponent(side)), Is.True);
        Assert.That(board.IsAttacked(entersAttack.To, Opponent(side)), Is.True);
        Assert.That(MoveGenerator.Generate(board, side), Does.Contain(exposesKing));
        Assert.That(MoveGenerator.Generate(board, side), Does.Contain(entersAttack));
    }

    [TestCase(PlayerColor.White)]
    [TestCase(PlayerColor.Black)]
    public void Initial_position_has_exactly_twenty_expected_moves(PlayerColor side)
    {
        var backRank = new[]
        {
            PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
            PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook,
        };
        var board = Board.Empty();
        for (var file = 0; file < 8; file++)
        {
            board = board
                .WithPiece(new Square(file, 0), new Piece(backRank[file], PlayerColor.White))
                .WithPiece(new Square(file, 1), new Piece(PieceType.Pawn, PlayerColor.White))
                .WithPiece(new Square(file, 6), new Piece(PieceType.Pawn, PlayerColor.Black))
                .WithPiece(new Square(file, 7), new Piece(backRank[file], PlayerColor.Black));
        }

        var pawnRank = side == PlayerColor.White ? 1 : 6;
        var direction = side == PlayerColor.White ? 1 : -1;
        var homeRank = side == PlayerColor.White ? 0 : 7;
        var expected = new List<Move>();
        for (var file = 0; file < 8; file++)
        {
            var from = new Square(file, pawnRank);
            expected.Add(new Move(from, new Square(file, pawnRank + direction)));
            expected.Add(new Move(from, new Square(file, pawnRank + 2 * direction)));
        }

        expected.Add(new Move(new Square(1, homeRank), new Square(0, homeRank + 2 * direction)));
        expected.Add(new Move(new Square(1, homeRank), new Square(2, homeRank + 2 * direction)));
        expected.Add(new Move(new Square(6, homeRank), new Square(5, homeRank + 2 * direction)));
        expected.Add(new Move(new Square(6, homeRank), new Square(7, homeRank + 2 * direction)));

        var actual = MoveGenerator.Generate(board, side);
        Assert.That(actual, Has.Count.EqualTo(20));
        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void Generating_moves_preserves_board_pieces_and_metadata()
    {
        var pawn = new Piece(PieceType.Pawn, PlayerColor.White);
        var enPassant = S("d6");
        var board = Board.Empty()
            .WithPiece(S("e2"), pawn)
            .WithCastling(CastlingRights.All)
            .WithEnPassant(enPassant);

        MoveGenerator.Generate(board, PlayerColor.White);

        Assert.That(board.At(S("e2")), Is.EqualTo(pawn));
        Assert.That(board.At(S("e3")), Is.Null);
        Assert.That(board.At(S("e4")), Is.Null);
        Assert.That(board.Castling, Is.EqualTo(CastlingRights.All));
        Assert.That(board.EnPassant, Is.EqualTo(enPassant));
    }

    private static void AssertDestinations(Board board, PlayerColor side, Square from, params Square[] expected)
    {
        var actual = MoveGenerator.Generate(board, side).Where(move => move.From == from).ToArray();
        Assert.That(actual, Is.EquivalentTo(expected.Select(to => new Move(from, to))));
    }

    private static Square S(string square) => new(square[0] - 'a', square[1] - '1');

    private static PlayerColor Opponent(PlayerColor side) =>
        side == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
}
