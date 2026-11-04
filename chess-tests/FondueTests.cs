using Chess.Core.Events;
using Chess.Core.Model;
using Chess.Core.Ports;
using Chess.Core.Rules;
using Chess.Core.Services;
using NUnit.Framework;
using static Chess.Tests.GameTestSupport;

namespace Chess.Tests;

public class FondueTests
{
    private sealed class Recorder : IEventPublisher
    {
        public readonly List<object> Items = [];
        public void Publish<T>(T domainEvent) => Items.Add(domainEvent!);
    }

    private static ChessGame Fondue(string fen, IEventPublisher? events = null)
    {
        var ports = Create();
        return new(events ?? ports.Events, ports.Repository, ports.Clock,
            FenSerializer.Load(fen).WithMode(GameMode.Fondue));
    }

    [Test]
    public void Capture_resolves_diagonal_chain_without_crossing_empty_squares_or_mutating_original()
    {
        var game = Fondue("7k/8/5n2/3pB3/8/8/8/K2R4 w - - 0 1");
        var before = game.Board;
        Assert.That(game.TryMove(Move("d1", "d5")), Is.True);
        Assert.That(FenSerializer.Write(game.Board), Is.EqualTo("7k/8/8/8/8/8/8/K7 b - - 0 1"));
        Assert.That(before.At(Square("d5"))?.Type, Is.EqualTo(PieceType.Pawn));
        Assert.That(before.At(Square("f6"))?.Type, Is.EqualTo(PieceType.Knight));
        Assert.That(game.Board.Mode, Is.EqualTo(GameMode.Fondue));
        Assert.That(game.History, Has.Count.EqualTo(1));
        Assert.That(game.Status, Is.EqualTo(GameStatus.DrawByInsufficientMaterial));
    }

    [Test]
    public void Normal_chess_capture_keeps_attacker_and_neighbors()
    {
        var game = Create("7k/8/5n2/3pB3/8/8/8/K2R4 w - - 0 1");
        Assert.That(game.TryMove(Move("d1", "d5")), Is.True);
        Assert.That(game.Board.At(Square("d5"))?.Type, Is.EqualTo(PieceType.Rook));
        Assert.That(game.Board.At(Square("e5"))?.Type, Is.EqualTo(PieceType.Bishop));
    }

    [Test]
    public void Quiet_move_does_not_melt_neighbors()
    {
        var game = Fondue("7k/8/8/8/8/8/8/K1NR4 w - - 0 1");
        Assert.That(game.TryMove(Move("d1", "d2")), Is.True);
        Assert.That(game.Board.At(Square("c1"))?.Type, Is.EqualTo(PieceType.Knight));
        Assert.That(game.Board.At(Square("d2"))?.Type, Is.EqualTo(PieceType.Rook));
    }

    [Test]
    public void En_passant_propagates_from_destination_and_captured_pawn()
    {
        var game = Fondue("7k/8/2b5/3pP3/2n5/8/8/K7 w - d6 0 1");
        Assert.That(game.TryMove(Move("e5", "d6")), Is.True);
        Assert.That(FenSerializer.Write(game.Board), Is.EqualTo("7k/8/8/8/8/8/8/K7 b - - 0 1"));
    }

    [Test]
    public void Capture_promotion_melts_the_promoted_piece_and_does_not_wrap_at_board_edges()
    {
        var events = new Recorder();
        var game = Fondue("r1k5/1P6/8/8/8/8/7p/K7 w - - 0 1", events);
        Assert.That(game.TryMove(Move("b7", "a8", PieceType.Queen)), Is.True);
        Assert.That(game.Board.At(Square("a8")), Is.Null);
        Assert.That(game.Board.At(Square("h2"))?.Type, Is.EqualTo(PieceType.Pawn));
        Assert.That(game.Board.At(Square("c8"))?.Type, Is.EqualTo(PieceType.King));
        Assert.That(events.Items.OfType<PromotionOccurred>(), Is.Empty);
    }

    [TestCase("7k/8/8/3p4/2K5/8/8/3R4 w - - 0 1", "d1", "d5")]
    [TestCase("8/8/4k3/3p4/2K5/8/8/3R4 w - - 0 1", "d1", "d5")]
    [TestCase("7k/8/8/3p4/2K5/8/8/8 w - - 0 1", "c4", "d5")]
    public void Self_melting_is_illegal_even_when_both_kings_would_melt(string fen, string from, string to)
    {
        var events = new Recorder();
        var game = Fondue(fen, events);
        var before = game.Board;
        Assert.That(game.LegalMoves(), Does.Not.Contain(Move(from, to)));
        Assert.That(game.TryMove(Move(from, to)), Is.False);
        Assert.That(game.Board, Is.SameAs(before));
        Assert.That(game.History, Is.Empty);
        Assert.That(events.Items, Is.Empty);
    }

    [Test]
    public void Indirect_chain_threat_is_check_and_cannot_be_ignored()
    {
        var game = Fondue("7k/8/8/8/8/2KBr3/8/R7 w - - 0 1");
        Assert.That(CheckDetector.IsInCheck(game.Board, PlayerColor.White), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Check));
        Assert.That(game.TryMove(Move("a1", "a2")), Is.False);
        Assert.That(game.TryMove(Move("c3", "b2")), Is.True);
    }

    [Test]
    public void A_chain_that_would_also_melt_the_attacking_king_is_not_an_indirect_threat()
    {
        var game = Fondue("8/8/8/8/4k3/2KBr3/8/R7 w - - 0 1");
        Assert.That(CheckDetector.IsInCheck(game.Board, PlayerColor.White), Is.False);
        Assert.That(game.TryMove(Move("a1", "a2")), Is.True);
    }

    [Test]
    public void Castling_cannot_start_under_an_indirect_capture_threat()
    {
        var game = Fondue("7k/8/8/2b5/8/8/5P2/4K2R w K - 0 1");
        Assert.That(CheckDetector.IsInCheck(game.Board, PlayerColor.White), Is.True);
        Assert.That(game.LegalMoves(), Does.Not.Contain(Move("e1", "g1")));
    }

    [Test]
    public void Safe_castling_preserves_the_mode_and_moves_both_pieces()
    {
        var game = Fondue("4k2r/8/8/8/8/8/8/R3K2R w KQk - 0 1");
        Assert.That(game.TryMove(Move("e1", "g1")), Is.True);
        Assert.That(game.Board.At(Square("g1"))?.Type, Is.EqualTo(PieceType.King));
        Assert.That(game.Board.At(Square("f1"))?.Type, Is.EqualTo(PieceType.Rook));
        Assert.That(game.Board.Mode, Is.EqualTo(GameMode.Fondue));
    }

    [Test]
    public void Double_pawn_move_cannot_offer_a_winning_en_passant_chain_against_own_king()
    {
        var game = Fondue("7k/8/8/8/3p4/8/4PK2/8 w - - 0 1");
        Assert.That(game.TryMove(Move("e2", "e4")), Is.False);
        Assert.That(game.TryMove(Move("f2", "g1")), Is.True);
    }

    [Test]
    public void Standard_stockfish_rejects_fondue_before_launching_a_process()
    {
        var game = Fondue("7k/6p1/5n2/8/8/8/8/K5R1 w - - 0 1");
        var ai = new Cheesse.Game.Adapters.Ai.StockfishAI("missing-engine");
        Assert.ThrowsAsync<NotSupportedException>(async () => await ai.ChooseMoveAsync(game));
    }

    [Test]
    public void Melting_a_home_rook_revokes_its_castling_rights()
    {
        var board = FenSerializer.Load("r3k3/1p6/8/8/8/8/8/1R5K w q - 0 1")
            .WithMode(GameMode.Fondue);
        var after = board.WithMove(Move("b1", "b7"));
        Assert.That(after.At(Square("a8")), Is.Null);
        Assert.That(after.Castling.BlackQueenside, Is.False);
        Assert.That(board.Castling.BlackQueenside, Is.True);
    }

    [Test]
    public void Victory_is_atomic_publishes_removed_pieces_and_stops_play_until_undo()
    {
        var events = new Recorder();
        var game = Fondue("7k/6p1/5n2/8/8/8/8/K5R1 w - - 0 1", events);
        var initial = game.Board;
        var move = Move("g1", "g7");
        game.ConfigureClock(new TimeControl(60, 0));
        Assert.That(game.TryMove(move), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.KingMelted));
        Assert.That(game.Winner, Is.EqualTo(PlayerColor.White));
        Assert.That(game.ClockState.Running, Is.False);
        Assert.That(game.LegalMoves(), Is.Empty);
        Assert.That(game.TryMove(Move("h8", "h7")), Is.False);
        Assert.That(events.Items.Select(item => item.GetType()), Is.EqualTo(new[]
            { typeof(PieceMoved), typeof(PiecesMelted), typeof(GameEnded) }));
        var melted = (PiecesMelted)events.Items[1];
        Assert.That(melted.Move, Is.EqualTo(move));
        Assert.That(melted.Pieces, Is.EquivalentTo(new[]
        {
            new MeltedPiece(Square("g7"), new Piece(PieceType.Pawn, PlayerColor.Black)),
            new MeltedPiece(Square("g7"), new Piece(PieceType.Rook, PlayerColor.White)),
            new MeltedPiece(Square("f6"), new Piece(PieceType.Knight, PlayerColor.Black)),
            new MeltedPiece(Square("h8"), new Piece(PieceType.King, PlayerColor.Black)),
        }));
        Assert.Throws<NotSupportedException>(() => ((IList<MeltedPiece>)melted.Pieces).Clear());
        var won = game.CreateMemento();
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Board, Is.SameAs(initial));
        Assert.That(game.Winner, Is.Null);
        Assert.That(game.Redo(), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.KingMelted));
        var restored = Create();
        restored.Restore(won);
        Assert.That(restored.Board.Mode, Is.EqualTo(GameMode.Fondue));
        Assert.That(restored.Undo(), Is.True);
        Assert.That(restored.Redo(), Is.True);
        Assert.That(restored.Winner, Is.EqualTo(PlayerColor.White));
        restored.RestoreFrom(Create());
        Assert.That(restored.Board.Mode, Is.EqualTo(GameMode.Standard));
        Assert.That(restored.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    [Test]
    public void A_minor_piece_can_still_win_by_melting_and_is_not_an_insufficient_material_draw()
    {
        var game = Fondue("7k/6b1/8/8/8/2B5/8/K7 w - - 0 1");
        Assert.That(game.State.AcceptsMoves, Is.True);
        Assert.That(GameRules.CanPossiblyMate(game.InitialBoard, PlayerColor.White), Is.True);
        Assert.That(game.TryMove(Move("c3", "g7")), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.KingMelted));
    }

    [Test]
    public void Winning_capture_is_allowed_while_under_an_unrelated_direct_check()
    {
        var game = Fondue("7k/6p1/8/8/8/8/r7/K5R1 w - - 0 1");
        Assert.That(game.Status, Is.EqualTo(GameStatus.Check));
        Assert.That(game.TryMove(Move("g1", "g7")), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.KingMelted));
    }

    [Test]
    public void Pgn_round_trip_preserves_mode_chain_and_winning_result()
    {
        var game = Fondue("7k/6p1/5n2/8/8/8/8/K5R1 w - - 0 1");
        Assert.That(SanNotation.ToSan(game.Board, Move("g1", "g7")), Is.EqualTo("Rxg7#"));
        Assert.That(game.TryMove(Move("g1", "g7")), Is.True);
        var pgn = SanNotation.ToPgn(game);
        Assert.That(pgn, Does.Contain("[Variant \"Fondue\"]"));
        var history = SanNotation.FromPgn(pgn, out var board, out var result);
        Assert.That(board.Mode, Is.EqualTo(GameMode.Fondue));
        var ports = Create();
        var restored = new ChessGame(ports.Events, ports.Repository, ports.Clock, board);
        foreach (var move in history) Assert.That(restored.TryMove(move), Is.True);
        restored.SetImportedResult(result);
        restored.RestoreOutcome(game.Status, game.Winner);
        Assert.That(restored.Status, Is.EqualTo(GameStatus.KingMelted));
        Assert.That(restored.Winner, Is.EqualTo(PlayerColor.White));
    }
}
