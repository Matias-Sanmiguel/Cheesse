using Chess.Core.Model;
using Chess.Core.Services;
using NUnit.Framework;
using static Chess.Tests.GameTestSupport;

namespace Chess.Tests;

public class PgnTests
{
    [Test]
    public void Opening_can_be_saved_loaded_played_and_saved_again()
    {
        var game = Create();
        Play(game, "e2e4", "e7e5", "g1f3", "b8c6", "f1b5");
        var pgn = SanNotation.ToPgn(game);

        Assert.That(pgn, Does.Contain("[Result \"*\"]"));
        Assert.That(pgn, Does.Contain("1. e4 e5 2. Nf3 Nc6 3. Bb5 *"));
        Assert.That(SanNotation.ToPgn(game.History), Is.EqualTo(pgn));
        var moves = SanNotation.FromPgn(pgn, out var initialBoard);
        var loaded = new ChessGame(game.Events, game.Repository, game.Clock, initialBoard);
        foreach (var move in moves)
            Assert.That(loaded.TryMove(move), Is.True);

        Assert.That(FenSerializer.Write(loaded.Board), Is.EqualTo(FenSerializer.Write(game.Board)));
        Assert.That(SanNotation.ToPgn(loaded), Is.EqualTo(pgn));
        Play(loaded, "a7a6");
        Assert.That(SanNotation.ToPgn(loaded), Does.Contain("3. Bb5 a6 *"));
    }

    [TestCase("4k3/8/8/3p4/4P3/8/8/4K3 w - - 0 1", "e4", "d5", "exd5")]
    [TestCase("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1", "e5", "d6", "exd6")]
    [TestCase("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "e1", "g1", "O-O")]
    [TestCase("r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 0 1", "e8", "c8", "O-O-O")]
    [TestCase("7k/8/8/8/8/8/8/R6K w - - 0 1", "a1", "a8", "Ra8+")]
    [TestCase("7k/8/8/8/8/8/7K/R6R w - - 0 1", "a1", "d1", "Rad1")]
    [TestCase("7k/8/8/8/8/R7/8/R6K w - - 0 1", "a1", "a2", "R1a2")]
    [TestCase("7k/8/8/8/8/1N6/7K/1N3N2 w - - 0 1", "b1", "d2", "Nb1d2")]
    [TestCase("7k/8/8/8/8/8/7K/1N3N2 w - - 0 1", "b1", "d2", "Nbd2")]
    [TestCase("4r2k/8/8/8/8/8/R3R3/4K3 w - - 0 1", "a2", "d2", "Rd2")]
    public void San_handles_captures_castling_checks_and_legal_disambiguation(
        string fen, string from, string to, string expected)
    {
        var board = FenSerializer.Load(fen);
        var move = Move(from, to);

        Assert.That(SanNotation.ToSan(board, move), Is.EqualTo(expected));
        Assert.That(move.ToSan(board), Is.EqualTo(expected));
    }

    [TestCase(PieceType.Queen, "a8=Q+")]
    [TestCase(PieceType.Rook, "a8=R+")]
    [TestCase(PieceType.Bishop, "a8=B")]
    [TestCase(PieceType.Knight, "a8=N")]
    public void Promotion_san_and_custom_start_round_trip(PieceType promotion, string expected)
    {
        const string fen = "7k/P7/8/8/8/8/8/7K w - - 0 11";
        var game = Create(fen);
        var move = Move("a7", "a8", promotion);
        Assert.That(move.ToSan(game.Board), Is.EqualTo(expected));
        Assert.That(game.TryMove(move), Is.True);
        var pgn = SanNotation.ToPgn(game);
        Assert.That(pgn, Does.Contain("[SetUp \"1\"]"));
        Assert.That(pgn, Does.Contain("[FEN \"" + fen + "\"]"));
        Assert.That(pgn, Does.Contain("11. " + expected));

        var loaded = SanNotation.FromPgn(pgn, out var initialBoard);
        Assert.That(loaded, Is.EqualTo(game.History));
        Assert.That(SanNotation.ToPgn(loaded, initialBoard), Is.EqualTo(pgn));
    }

    [Test]
    public void Black_start_preserves_move_number_and_fen()
    {
        var game = Create("4k1n1/8/8/8/8/8/8/R3K3 b - - 17 42");
        Play(game, "g8f6", "a1a2");
        var pgn = SanNotation.ToPgn(game);

        Assert.That(pgn, Does.Contain("42... Nf6 43. Ra2 *"));
        var moves = SanNotation.FromPgn(pgn, out var board);
        Assert.That(SanNotation.ToPgn(moves, board), Is.EqualTo(pgn));
    }

    [Test]
    public void Fool_mate_has_san_mate_suffix_and_matching_black_result()
    {
        var game = Create();
        Play(game, "f2f3", "e7e5", "g2g4", "d8h4");

        Assert.That(game.Status, Is.EqualTo(GameStatus.Checkmate));
        var pgn = SanNotation.ToPgn(game);
        Assert.That(pgn, Does.Contain("[Result \"0-1\"]"));
        Assert.That(pgn, Does.Contain("1. f3 e5 2. g4 Qh4# 0-1"));
        Assert.That(SanNotation.FromPgn(pgn, out _), Is.EqualTo(game.History));
    }

    [Test]
    public void White_mate_result_is_exported()
    {
        var game = Create("7k/8/5KQ1/8/8/8/8/8 w - - 0 1");
        Play(game, "g6g7");

        Assert.That(game.Status, Is.EqualTo(GameStatus.Checkmate));
        Assert.That(SanNotation.ToPgn(game), Does.Contain("1. Qg7# 1-0"));
    }

    [TestCase("7k/8/8/8/8/8/8/7K w - - 0 1")]
    [TestCase("7k/5K2/6Q1/8/8/8/8/8 b - - 0 1")]
    [TestCase("7k/8/8/8/8/8/8/R6K w - - 100 51")]
    public void Draw_results_are_exported(string fen)
    {
        var pgn = SanNotation.ToPgn(Create(fen));
        Assert.That(pgn, Does.Contain("[Result \"1/2-1/2\"]"));
        Assert.That(pgn.TrimEnd(), Does.EndWith("1/2-1/2"));
    }

    [Test]
    public void Threefold_repetition_result_is_exported()
    {
        var game = Create();
        for (var cycle = 0; cycle < 2; cycle++)
            Play(game, "g1f3", "g8f6", "f3g1", "f6g8");

        var pgn = SanNotation.ToPgn(game);
        Assert.That(pgn, Does.Contain("[Result \"1/2-1/2\"]"));
        Assert.That(SanNotation.FromPgn(pgn, out _), Is.EqualTo(game.History));
    }

    [Test]
    public void Import_reads_main_line_ignoring_comments_nags_and_nested_variations()
    {
        const string pgn = """
            [Result "*"]

            1.e4 {king pawn} e5 $1 (1...c5 (1...e6) 2.Nf3)
            2.Nf3!? ; development
            Nc6 *
            """;
        var moves = SanNotation.FromPgn(pgn, out var initialBoard);

        Assert.That(FenSerializer.Write(initialBoard), Is.EqualTo(FenSerializer.InitialPosition));
        Assert.That(moves, Is.EqualTo(new[]
        {
            Move("e2", "e4"), Move("e7", "e5"), Move("g1", "f3"), Move("b8", "c6"),
        }));
    }

    [TestCase("1. e5 *")]
    [TestCase("1. e4")]
    [TestCase("1. e4 * e5")]
    [TestCase("1. e4 {unterminated *")]
    [TestCase("1. e4 (1. d4 *")]
    [TestCase("1. e4 ) *")]
    [TestCase("[Result \"1-0\"] 1. e4 *")]
    [TestCase("[SetUp \"1\"] *")]
    [TestCase("[SetUp \"2\"] *")]
    [TestCase("[FEN \"8/8/8/8/8/8/8/8 w - - 0 1\"] *")]
    public void Malformed_pgn_is_rejected(string pgn)
    {
        Assert.Throws<FormatException>(() => SanNotation.FromPgn(pgn, out _));
    }

    [Test]
    public void Illegal_move_cannot_be_exported_as_san_or_pgn()
    {
        var game = Create();
        var illegal = Move("e2", "e5");

        Assert.Throws<ArgumentException>(() => SanNotation.ToSan(game.Board, illegal));
        Assert.Throws<ArgumentException>(() => SanNotation.ToPgn(new[] { illegal }));
        Assert.Throws<InvalidOperationException>(() => illegal.ToSan());
    }

    [Test]
    public void Saving_does_not_change_game_and_undo_removes_last_pgn_move()
    {
        var game = Create();
        Play(game, "e2e4", "e7e5", "g1f3");
        var board = game.Board;
        var status = game.Status;
        SanNotation.ToPgn(game);

        Assert.That(game.Board, Is.SameAs(board));
        Assert.That(game.Status, Is.EqualTo(status));
        Assert.That(game.History, Has.Count.EqualTo(3));
        Assert.That(game.Undo(), Is.True);
        Assert.That(SanNotation.ToPgn(game), Does.Contain("1. e4 e5 *"));
        Assert.That(SanNotation.ToPgn(game), Does.Not.Contain("Nf3"));
    }

    [TestCase("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1", "e5", "d6", "exd6")]
    [TestCase("4k3/8/8/8/3Pp3/8/8/4K3 b - d3 0 7", "e4", "d3", "exd3")]
    [TestCase("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "e1", "g1", "O-O")]
    [TestCase("r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 0 7", "e8", "c8", "O-O-O")]
    [TestCase("7k/8/8/8/8/1N6/7K/1N3N2 w - - 0 1", "b1", "d2", "Nb1d2")]
    [TestCase("7k/8/8/3p4/4K3/8/8/8 w - - 0 1", "e4", "d5", "Kxd5")]
    public void Special_moves_and_disambiguation_round_trip_through_pgn(
        string fen, string from, string to, string san)
    {
        var game = Create(fen);
        var move = Move(from, to);
        Assert.That(move.ToSan(game.Board), Is.EqualTo(san));
        Assert.That(game.TryMove(move), Is.True);
        var saved = SanNotation.ToPgn(game);
        var loaded = SanNotation.FromPgn(saved, out var initialBoard);

        Assert.That(loaded, Is.EqualTo(new[] { move }));
        Assert.That(SanNotation.ToPgn(loaded, initialBoard), Is.EqualTo(saved));
        Assert.That(FenSerializer.Write(initialBoard.WithMove(loaded[0])),
            Is.EqualTo(FenSerializer.Write(game.Board)));
    }

    [Test]
    public void Capturing_promotion_is_written_and_imported_in_san()
    {
        var game = Create("4k2r/6P1/8/8/8/8/8/4K3 w - - 0 1");
        var move = Move("g7", "h8", PieceType.Queen);
        Assert.That(move.ToSan(game.Board), Is.EqualTo("gxh8=Q+"));
        Assert.That(game.TryMove(move), Is.True);

        var pgn = SanNotation.ToPgn(game);
        Assert.That(SanNotation.FromPgn(pgn, out _), Is.EqualTo(new[] { move }));
    }
}
