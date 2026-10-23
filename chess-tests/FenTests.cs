using Chess.Core.Model;
using Chess.Core.Services;
using NUnit.Framework;
using static Chess.Tests.GameTestSupport;

namespace Chess.Tests;

public class FenTests
{
    [TestCase(FenSerializer.InitialPosition)]
    [TestCase("8/8/8/8/8/8/8/8 w - - 0 1")]
    [TestCase("r3k2r/8/8/3pP3/8/8/8/R3K2R w KQkq d6 17 42")]
    [TestCase("r3k2r/8/8/8/3Pp3/8/8/R3K2R b Kq d3 99 125")]
    [TestCase("4k3/8/8/8/8/8/8/4K3 b - - 100 93")]
    public void All_six_fields_round_trip(string fen)
    {
        var board = FenSerializer.Load(fen);

        Assert.That(FenSerializer.Write(board), Is.EqualTo(fen));
        Assert.That(FenSerializer.Write(board, board.Turn), Is.EqualTo(fen));
    }

    [Test]
    public void Load_keeps_piece_coordinates_turn_rights_target_and_counters()
    {
        var board = FenSerializer.Load("r3k2r/8/8/8/3Pp3/8/8/R3K2R b Kq d3 99 125");

        Assert.Multiple(() =>
        {
            Assert.That(board.At(Square("a8")), Is.EqualTo(new Piece(PieceType.Rook, PlayerColor.Black)));
            Assert.That(board.At(Square("d4")), Is.EqualTo(new Piece(PieceType.Pawn, PlayerColor.White)));
            Assert.That(board.At(Square("e4")), Is.EqualTo(new Piece(PieceType.Pawn, PlayerColor.Black)));
            Assert.That(board.At(Square("e1")), Is.EqualTo(new Piece(PieceType.King, PlayerColor.White)));
            Assert.That(board.Turn, Is.EqualTo(PlayerColor.Black));
            Assert.That(board.Castling, Is.EqualTo(new CastlingRights(true, false, false, true)));
            Assert.That(board.EnPassant, Is.EqualTo(Square("d3")));
            Assert.That(board.HalfmoveClock, Is.EqualTo(99));
            Assert.That(board.FullmoveNumber, Is.EqualTo(125));
        });
    }

    [Test]
    public void Load_play_save_and_reload_opening()
    {
        var game = Create();
        Play(game, "e2e4", "c7c5", "g1f3");
        const string expected = "rnbqkbnr/pp1ppppp/8/2p5/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2";

        Assert.That(FenSerializer.Write(game.Board), Is.EqualTo(expected));
        var reloaded = Create(expected);
        Assert.That(reloaded.Turn, Is.EqualTo(PlayerColor.Black));
        Assert.That(reloaded.LegalMoves(), Is.EquivalentTo(game.LegalMoves()));
        Play(reloaded, "b8c6");
        Assert.That(reloaded.Board.HalfmoveClock, Is.EqualTo(2));
        Assert.That(reloaded.Board.FullmoveNumber, Is.EqualTo(3));
    }

    [TestCase("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 12 23", "e1", "g1",
        "r3k2r/8/8/8/8/8/8/R4RK1 b kq - 13 23")]
    [TestCase("4k3/8/8/3pP3/8/8/8/4K3 w - d6 27 42", "e5", "d6",
        "4k3/8/3P4/8/8/8/8/4K3 b - - 0 42")]
    [TestCase("r3k3/8/8/8/8/8/8/R3K3 w q - 39 5", "a1", "a8",
        "R3k3/8/8/8/8/8/8/4K3 b - - 0 5")]
    [TestCase("4k1n1/8/8/8/8/8/8/R3K3 b - - 17 42", "g8", "f6",
        "4k3/8/5n2/8/8/8/8/R3K3 w - - 18 43")]
    public void Load_play_save_updates_special_moves_and_counters(
        string initialFen, string from, string to, string expected)
    {
        var game = Create(initialFen);
        Assert.That(game.TryMove(Move(from, to)), Is.True);
        Assert.That(FenSerializer.Write(game.Board), Is.EqualTo(expected));
        Assert.That(FenSerializer.Write(FenSerializer.Load(expected)), Is.EqualTo(expected));
    }

    [Test]
    public void Promotion_is_saved_as_the_promoted_piece()
    {
        var game = Create("7k/P7/8/8/8/8/8/7K w - - 8 11");
        Assert.That(game.TryMove(Move("a7", "a8", PieceType.Queen)), Is.True);

        Assert.That(FenSerializer.Write(game.Board), Is.EqualTo("Q6k/8/8/8/8/8/8/7K b - - 0 11"));
    }

    [TestCase("")]
    [TestCase("8/8/8/8/8/8/8/8 w - - 0")]
    [TestCase("8/8/8/8/8/8/8/8 w - - 0 1 extra")]
    [TestCase("8/8/8/8/8/8/8 w - - 0 1")]
    [TestCase("8/8/8/8/8/8/8/9 w - - 0 1")]
    [TestCase("8/8/8/8/8/8/8/7 w - - 0 1")]
    [TestCase("8/8/8/8/8/8/8/8P w - - 0 1")]
    [TestCase("8/8/8/8/8/8/8/44 w - - 0 1")]
    [TestCase("8/8/8/8/8/8/8/7X w - - 0 1")]
    [TestCase("8/8/8/8/8/8/8/8 x - - 0 1")]
    [TestCase("8/8/8/8/8/8/8/8 w KK - 0 1")]
    [TestCase("8/8/8/8/8/8/8/8 w K- - 0 1")]
    [TestCase("8/8/8/8/8/8/8/8 w A - 0 1")]
    [TestCase("8/8/8/8/8/8/8/8 w - a3 0 1")]
    [TestCase("8/8/8/8/8/8/8/8 b - a6 0 1")]
    [TestCase("8/8/8/8/8/8/8/8 w - i6 0 1")]
    [TestCase("8/8/8/8/8/8/8/8 w - a66 0 1")]
    [TestCase("8/8/8/8/8/8/8/8 w - - -1 1")]
    [TestCase("8/8/8/8/8/8/8/8 w - - 0 0")]
    [TestCase("8/8/8/8/8/8/8/8 w - - 0 -1")]
    [TestCase("8/8/8/8/8/8/8/8 w - - 1x 1")]
    [TestCase("8/8/8/8/8/8/8/8 w - - 2147483648 1")]
    public void Malformed_fen_is_rejected(string fen)
    {
        Assert.Throws<FormatException>(() => FenSerializer.Load(fen));
    }

    [Test]
    public void Null_fen_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => FenSerializer.Load(null!));
        Assert.Throws<ArgumentNullException>(() => FenSerializer.Write(null!));
        Assert.Throws<ArgumentNullException>(() => FenSerializer.Write(null!, PlayerColor.White));
    }

    [Test]
    public void Board_builders_preserve_position_metadata_without_mutating_original()
    {
        var original = FenSerializer.Load("4k3/8/8/8/8/8/8/R3K3 b - - 17 42");
        var placed = original.WithPiece(Square("b1"), new Piece(PieceType.Knight, PlayerColor.White))
            .WithCastling(CastlingRights.All).WithEnPassant(Square("a3"));

        Assert.That(placed.Turn, Is.EqualTo(PlayerColor.Black));
        Assert.That(placed.HalfmoveClock, Is.EqualTo(17));
        Assert.That(placed.FullmoveNumber, Is.EqualTo(42));
        var moved = original.WithMove(Move("e8", "d8"));
        Assert.That(moved.Turn, Is.EqualTo(PlayerColor.White));
        Assert.That(moved.HalfmoveClock, Is.EqualTo(18));
        Assert.That(moved.FullmoveNumber, Is.EqualTo(43));
        Assert.That(FenSerializer.Write(original), Is.EqualTo("4k3/8/8/8/8/8/8/R3K3 b - - 17 42"));

        var changed = original.WithState(PlayerColor.White, 0, 1);
        Assert.That(changed, Is.Not.SameAs(original));
        Assert.That(original.Turn, Is.EqualTo(PlayerColor.Black));
        Assert.Throws<ArgumentOutOfRangeException>(() => original.WithState(PlayerColor.White, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => original.WithState(PlayerColor.White, 0, 0));
    }
}
