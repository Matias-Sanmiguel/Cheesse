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
    public void Capturing_a_nonpawn_only_melts_the_attacker_and_adjacent_nonpawns()
    {
        var events = new Recorder();
        var game = Fondue("7k/8/2p2n2/3rB3/8/8/8/K2R4 w - - 0 1", events);
        var before = game.Board;
        Assert.That(game.TryMove(Move("d1", "d5")), Is.True);
        Assert.That(game.Board.At(Square("d5")), Is.Null);
        Assert.That(game.Board.At(Square("e5")), Is.Null);
        Assert.That(game.Board.At(Square("c6"))?.Type, Is.EqualTo(PieceType.Pawn),
            "neighboring pawns are immune");
        Assert.That(game.Board.At(Square("f6"))?.Type, Is.EqualTo(PieceType.Knight),
            "the bishop's neighbor must not trigger a second wave");
        Assert.That(before.At(Square("d5"))?.Type, Is.EqualTo(PieceType.Rook));
        Assert.That(before.At(Square("e5"))?.Type, Is.EqualTo(PieceType.Bishop));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.Mode, Is.EqualTo(GameMode.Fondue));
        Assert.That(events.Items.OfType<PiecesMelted>().Single().Pieces, Is.EquivalentTo(new[]
        {
            new MeltedPiece(Square("d5"), new Piece(PieceType.Rook, PlayerColor.Black)),
            new MeltedPiece(Square("e5"), new Piece(PieceType.Bishop, PlayerColor.White)),
            new MeltedPiece(Square("d5"), new Piece(PieceType.Rook, PlayerColor.White))
        }));
    }

    [Test]
    public void Capturing_a_pawn_is_regular_chess_and_emits_no_melt_event()
    {
        var events = new Recorder();
        var game = Fondue("7k/p5p1/8/8/8/8/8/K5R1 w - - 0 1", events);
        Assert.That(game.TryMove(Move("g1", "g7")), Is.True);
        Assert.That(game.Board.At(Square("g7")),
            Is.EqualTo(new Piece(PieceType.Rook, PlayerColor.White)));
        Assert.That(game.Board.At(Square("h8")),
            Is.EqualTo(new Piece(PieceType.King, PlayerColor.Black)));
        Assert.That(game.Status, Is.Not.EqualTo(GameStatus.KingMelted));
        Assert.That(events.Items.OfType<PiecesMelted>(), Is.Empty);
    }

    [Test]
    public void Screenshot_pawn_capture_and_following_pawn_check_use_classic_rules()
    {
        // Regression: this was unexpectedly forbidden by the original runaway cascade.
        const string fen = "rnbqkbnr/pp3pp1/3p3p/2p1P3/8/8/PPPP2PP/RNBQKBNR w KQkq - 0 1";
        var capture = Fondue(fen);
        Assert.That(capture.LegalMoves(), Does.Contain(Move("e5", "d6")));
        Assert.That(capture.TryMove(Move("e5", "d6")), Is.True);
        Assert.That(capture.Board.At(Square("d6")),
            Is.EqualTo(new Piece(PieceType.Pawn, PlayerColor.White)));
        Assert.That(GameRules.KingMeltingThreats(capture.Board, PlayerColor.White),
            Does.Not.Contain(Move("d8", "d2")),
            "the d-file remains closed by the surviving pawn");

        var advance = Fondue(fen);
        Assert.That(advance.TryMove(Move("e5", "e6")), Is.True);
        Assert.That(advance.Status, Is.EqualTo(GameStatus.Ongoing),
            "e6xf7 is now a normal pawn capture, not an indirect king check");
        Assert.That(GameRules.KingMeltingThreats(advance.Board, PlayerColor.Black),
            Does.Not.Contain(Move("e6", "f7")));
    }

    [Test]
    public void Preview_matches_local_melt_and_excludes_pawns_and_secondary_neighbors()
    {
        var game = Fondue("7k/8/2p2n2/3rB3/8/8/8/K2R4 w - - 0 1");
        var before = game.Board;
        var move = Move("d1", "d5");
        Assert.That(GameRules.MeltingSquares(before, move), Is.EquivalentTo(new[]
        {
            Square("d5"), Square("e5")
        }));
        Assert.That(game.Board, Is.SameAs(before), "preview must not mutate the game");
        Assert.That(GameRules.MeltingSquares(before, Move("a1", "a2")), Is.Empty);
        var standard = before.WithMode(GameMode.Standard);
        Assert.That(GameRules.MeltingSquares(standard, move), Is.Empty);
    }

    [Test]
    public void Pawn_capturing_a_knight_melts_itself_but_not_neighboring_pawns()
    {
        var events = new Recorder();
        var game = Fondue("7k/8/3p4/3n4/4P3/8/8/K7 w - - 0 1", events);
        Assert.That(game.TryMove(Move("e4", "d5")), Is.True);
        Assert.That(game.Board.At(Square("d5")), Is.Null);
        Assert.That(game.Board.At(Square("d6"))?.Type, Is.EqualTo(PieceType.Pawn));
        Assert.That(events.Items.OfType<PiecesMelted>().Single().Pieces, Is.EquivalentTo(new[]
        {
            new MeltedPiece(Square("d5"), new Piece(PieceType.Knight, PlayerColor.Black)),
            new MeltedPiece(Square("d5"), new Piece(PieceType.Pawn, PlayerColor.White))
        }));
    }

    [Test]
    public void Standard_mode_capture_is_unchanged()
    {
        var game = Create("7k/8/5n2/3rB3/8/8/8/K2R4 w - - 0 1");
        Assert.That(game.TryMove(Move("d1", "d5")), Is.True);
        Assert.That(game.Board.At(Square("d5"))?.Type, Is.EqualTo(PieceType.Rook));
        Assert.That(game.Board.At(Square("e5"))?.Type, Is.EqualTo(PieceType.Bishop));
    }

    [Test]
    public void Quiet_move_never_melts_any_piece()
    {
        var game = Fondue("7k/8/8/8/8/8/8/K1NR4 w - - 0 1");
        Assert.That(game.TryMove(Move("d1", "d2")), Is.True);
        Assert.That(game.Board.At(Square("c1"))?.Type, Is.EqualTo(PieceType.Knight));
        Assert.That(game.Board.At(Square("d2"))?.Type, Is.EqualTo(PieceType.Rook));
    }

    [Test]
    public void En_passant_is_normal_and_pawns_or_minors_nearby_remain()
    {
        var events = new Recorder();
        var game = Fondue("7k/8/2b5/3pP3/2n5/8/8/K7 w - d6 0 1", events);
        Assert.That(game.TryMove(Move("e5", "d6")), Is.True);
        Assert.That(FenSerializer.Write(game.Board),
            Is.EqualTo("7k/8/2bP4/8/2n5/8/8/K7 b - - 0 1"));
        Assert.That(events.Items.OfType<PiecesMelted>(), Is.Empty);
    }

    [Test]
    public void Promotion_capturing_a_rook_melts_promoted_attacker_without_wrapping_edges()
    {
        var events = new Recorder();
        var game = Fondue("r1k5/1P6/8/8/8/8/7p/K7 w - - 0 1", events);
        Assert.That(game.TryMove(Move("b7", "a8", PieceType.Queen)), Is.True);
        Assert.That(game.Board.At(Square("a8")), Is.Null);
        Assert.That(game.Board.At(Square("h2"))?.Type, Is.EqualTo(PieceType.Pawn));
        Assert.That(game.Board.At(Square("c8"))?.Type, Is.EqualTo(PieceType.King));
        Assert.That(events.Items.OfType<PromotionOccurred>(), Is.Empty);
        Assert.That(events.Items.OfType<PiecesMelted>(), Has.Exactly(1).Items);
    }

    [TestCase("7k/8/8/3n4/2K5/8/8/3R4 w - - 0 1", "d1", "d5")]
    [TestCase("8/8/4k3/3n4/2K5/8/8/3R4 w - - 0 1", "d1", "d5")]
    [TestCase("7k/8/8/3n4/2K5/8/8/8 w - - 0 1", "c4", "d5")]
    public void Self_melting_captures_are_illegal_even_if_enemy_king_also_melts(string fen, string from, string to)
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
    public void Capturing_a_nonpawn_near_enemy_king_still_creates_indirect_check()
    {
        var game = Fondue("7k/8/8/8/8/2KBr3/8/R7 w - - 0 1");
        Assert.That(CheckDetector.IsInCheck(game.Board, PlayerColor.White), Is.True);
        Assert.That(GameRules.KingMeltingThreats(game.Board, PlayerColor.White),
            Does.Contain(Move("e3", "d3")));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Check));
        Assert.That(game.TryMove(Move("a1", "a2")), Is.False);
        Assert.That(game.TryMove(Move("c3", "b2")), Is.True);
    }

    [Test]
    public void A_capture_that_would_also_melt_attacking_king_is_not_a_threat()
    {
        var game = Fondue("8/8/8/8/4k3/2KBr3/8/R7 w - - 0 1");
        Assert.That(CheckDetector.IsInCheck(game.Board, PlayerColor.White), Is.False);
        Assert.That(game.TryMove(Move("a1", "a2")), Is.True);
    }

    [Test]
    public void Capturing_a_pawn_cannot_create_indirect_check_and_does_not_block_castling()
    {
        var game = Fondue("7k/8/8/2b5/8/8/5P2/4K2R w K - 0 1");
        Assert.That(CheckDetector.IsInCheck(game.Board, PlayerColor.White), Is.False);
        Assert.That(GameRules.KingMeltingThreats(game.Board, PlayerColor.White), Is.Empty);
        Assert.That(game.LegalMoves(), Does.Contain(Move("e1", "g1")));
    }

    [Test]
    public void Safe_castling_remains_available()
    {
        var game = Fondue("4k2r/8/8/8/8/8/8/R3K2R w KQk - 0 1");
        Assert.That(game.TryMove(Move("e1", "g1")), Is.True);
        Assert.That(game.Board.At(Square("g1"))?.Type, Is.EqualTo(PieceType.King));
        Assert.That(game.Board.At(Square("f1"))?.Type, Is.EqualTo(PieceType.Rook));
        Assert.That(game.Board.Mode, Is.EqualTo(GameMode.Fondue));
    }

    [Test]
    public void Double_pawn_move_and_enemy_en_passant_remain_legal()
    {
        var game = Fondue("7k/8/8/8/3p4/8/4PK2/8 w - - 0 1");
        Assert.That(game.TryMove(Move("e2", "e4")), Is.True);
        Assert.That(game.Board.EnPassant, Is.EqualTo(Square("e3")));
        Assert.That(GameRules.KingMeltingThreats(game.Board, PlayerColor.White), Is.Empty);
        Assert.That(game.TryMove(Move("d4", "e3")), Is.True);
        Assert.That(game.Board.At(Square("e3"))?.Type, Is.EqualTo(PieceType.Pawn));
        Assert.That(game.Board.At(Square("f2"))?.Type, Is.EqualTo(PieceType.King));
    }

    [Test]
    public void Standard_Stockfish_rejects_Fondue()
    {
        var game = Fondue("7k/6n1/8/8/8/8/8/K5R1 w - - 0 1");
        var ai = new Cheesse.Game.Adapters.Ai.StockfishAI("missing-engine");
        Assert.ThrowsAsync<NotSupportedException>(async () => await ai.ChooseMoveAsync(game));
    }

    [Test]
    public void Melting_a_home_rook_revokes_its_castling_rights()
    {
        var board = FenSerializer.Load("r3k3/1n6/8/8/8/8/8/1R5K w q - 0 1")
            .WithMode(GameMode.Fondue);
        var after = board.WithMove(Move("b1", "b7"));
        Assert.That(after.At(Square("a8")), Is.Null);
        Assert.That(after.Castling.BlackQueenside, Is.False);
        Assert.That(board.Castling.BlackQueenside, Is.True);
    }

    [Test]
    public void Melted_king_victory_publishes_only_removed_pieces_and_restores_with_undo_redo()
    {
        var events = new Recorder();
        var game = Fondue("7k/6n1/5p2/8/8/8/8/K5R1 w - - 0 1", events);
        var initial = game.Board;
        var move = Move("g1", "g7");
        game.ConfigureClock(new TimeControl(60, 0));
        Assert.That(game.TryMove(move), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.KingMelted));
        Assert.That(game.Winner, Is.EqualTo(PlayerColor.White));
        Assert.That(game.Board.At(Square("f6"))?.Type, Is.EqualTo(PieceType.Pawn));
        Assert.That(game.ClockState.Running, Is.False);
        Assert.That(game.LegalMoves(), Is.Empty);
        Assert.That(game.TryMove(Move("h8", "h7")), Is.False);
        Assert.That(events.Items.Select(item => item.GetType()), Is.EqualTo(new[]
            { typeof(PieceMoved), typeof(PiecesMelted), typeof(GameEnded) }));
        var melted = (PiecesMelted)events.Items[1];
        Assert.That(melted.Move, Is.EqualTo(move));
        Assert.That(melted.Pieces, Is.EquivalentTo(new[]
        {
            new MeltedPiece(Square("g7"), new Piece(PieceType.Knight, PlayerColor.Black)),
            new MeltedPiece(Square("g7"), new Piece(PieceType.Rook, PlayerColor.White)),
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
    }

    [Test]
    public void A_minor_can_win_by_melting_an_enemy_king()
    {
        var game = Fondue("7k/6b1/8/8/8/2B5/8/K7 w - - 0 1");
        Assert.That(game.State.AcceptsMoves, Is.True);
        Assert.That(GameRules.CanPossiblyMate(game.InitialBoard, PlayerColor.White), Is.True);
        Assert.That(game.TryMove(Move("c3", "g7")), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.KingMelted));
    }

    [Test]
    public void Winning_melt_is_allowed_while_under_unrelated_direct_check()
    {
        var game = Fondue("7k/6n1/8/8/8/8/r7/K5R1 w - - 0 1");
        Assert.That(game.Status, Is.EqualTo(GameStatus.Check));
        Assert.That(game.TryMove(Move("g1", "g7")), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.KingMelted));
    }

    [Test]
    public void Hot_square_burns_landing_piece_only_once_and_undo_redo_restore_the_hazard()
    {
        var events = new Recorder();
        var game = Fondue("7k/3r4/8/3n4/8/8/8/K2R4 w - - 0 1", events);
        var initial = game.Board;
        Assert.That(initial.HotSquare, Is.Null);
        Assert.That(game.TryMove(Move("d1", "d5")), Is.True);
        var afterMelt = game.Board;
        Assert.That(afterMelt.HotSquare, Is.EqualTo(Square("d5")));
        Assert.That(afterMelt.At(Square("d5")), Is.Null);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.LegalMoves(), Does.Contain(Move("d7", "d5")),
            "A rook can voluntarily land on hot tile at the price of the piece.");

        Assert.That(game.TryMove(Move("d7", "d5")), Is.True);
        Assert.That(game.Board.HotSquare, Is.Null, "Heat ends with the opponent's response.");
        Assert.That(game.Board.At(Square("d5")), Is.Null);
        Assert.That(game.Board.HalfmoveClock, Is.Zero, "Burning a rook resets the 50-move counter.");
        Assert.That(events.Items.OfType<PiecesMelted>().Count(), Is.EqualTo(2));
        Assert.That(events.Items.OfType<PiecesMelted>().Last().Pieces, Is.EquivalentTo(new[]
        {
            new MeltedPiece(Square("d5"), new Piece(PieceType.Rook, PlayerColor.Black))
        }), "Landing on heat is a single-piece burn, never a second 3x3 blast.");

        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Board, Is.SameAs(afterMelt));
        Assert.That(game.Board.HotSquare, Is.EqualTo(Square("d5")));
        Assert.That(game.Redo(), Is.True);
        Assert.That(game.Board.HotSquare, Is.Null);
        Assert.That(game.Undo() && game.Undo(), Is.True);
        Assert.That(game.Board, Is.SameAs(initial));
        Assert.That(game.Redo() && game.Redo(), Is.True);
        Assert.That(game.Board.HotSquare, Is.Null);
    }

    [Test]
    public void Heat_cools_after_just_one_reply_without_a_piece_landing_on_it()
    {
        var game = Fondue("7k/3r4/8/3n4/8/8/8/K2R4 w - - 0 1");
        Assert.That(game.TryMove(Move("d1", "d5")), Is.True);
        Assert.That(game.Board.HotSquare, Is.EqualTo(Square("d5")));
        Assert.That(game.TryMove(Move("h8", "h7")), Is.True);
        Assert.That(game.Board.HotSquare, Is.Null);
        Assert.That(game.Mode, Is.EqualTo(GameMode.Fondue));
    }

    [Test]
    public void Rook_crossing_heat_burns_before_capturing_piece_beyond()
    {
        var events = new Recorder();
        var game = Fondue("7k/3r4/8/3nR3/8/3B4/8/K7 w - - 0 1", events);
        Assert.That(game.TryMove(Move("e5", "d5")), Is.True);
        var crossing = Move("d7", "d3");
        Assert.That(GameRules.HeatContact(game.Board, crossing), Is.EqualTo(Square("d5")));
        Assert.That(GameRules.HeatBurnsMover(game.Board, crossing), Is.True);
        Assert.That(game.LegalMoves(), Does.Contain(crossing),
            "Crossing hot terrain is legal even when it sacrifices the rook.");
        Assert.That(game.TryMove(crossing), Is.True);
        Assert.That(game.Board.At(Square("d7")), Is.Null);
        Assert.That(game.Board.At(Square("d5")), Is.Null);
        Assert.That(game.Board.At(Square("d3")),
            Is.EqualTo(new Piece(PieceType.Bishop, PlayerColor.White)),
            "Piece beyond the hot square cannot be captured by a rook that burned on the way.");
        Assert.That(game.Board.HotSquare, Is.Null);
        Assert.That(game.Board.HalfmoveClock, Is.Zero);
        Assert.That(events.Items.OfType<PiecesMelted>().Last().Pieces, Is.EquivalentTo(new[]
        {
            new MeltedPiece(Square("d5"), new Piece(PieceType.Rook, PlayerColor.Black))
        }));
    }

    [Test]
    public void Bishop_and_queen_cannot_cross_hot_square_safely()
    {
        var bishop = Fondue("7k/1b6/8/3nR3/8/5Q2/8/K7 w - - 0 1");
        Assert.That(bishop.TryMove(Move("e5", "d5")), Is.True);
        Assert.That(bishop.LegalMoves(), Does.Contain(Move("b7", "f3")));
        Assert.That(bishop.TryMove(Move("b7", "f3")), Is.True);
        Assert.That(bishop.Board.At(Square("f3")),
            Is.EqualTo(new Piece(PieceType.Queen, PlayerColor.White)));
        Assert.That(bishop.Board.At(Square("b7")), Is.Null);

        var queen = Fondue("7k/8/8/q2nR1B1/8/8/8/K7 w - - 0 1");
        Assert.That(queen.TryMove(Move("e5", "d5")), Is.True);
        Assert.That(queen.LegalMoves(), Does.Contain(Move("a5", "g5")));
        Assert.That(queen.TryMove(Move("a5", "g5")), Is.True);
        Assert.That(queen.Board.At(Square("a5")), Is.Null);
        Assert.That(queen.Board.At(Square("g5")),
            Is.EqualTo(new Piece(PieceType.Bishop, PlayerColor.White)));
        Assert.That(queen.Board.HotSquare, Is.Null);
    }

    [Test]
    public void Pawn_double_step_crossing_heat_burns_without_reaching_destination()
    {
        var game = Fondue("7k/3p4/3n4/8/8/8/8/K2R4 w - - 0 1");
        Assert.That(game.TryMove(Move("d1", "d6")), Is.True);
        Assert.That(game.Board.HotSquare, Is.EqualTo(Square("d6")));
        Assert.That(game.LegalMoves(), Does.Contain(Move("d7", "d5")));
        Assert.That(game.TryMove(Move("d7", "d5")), Is.True);
        Assert.That(game.Board.At(Square("d7")), Is.Null);
        Assert.That(game.Board.At(Square("d5")), Is.Null);
        Assert.That(game.Board.EnPassant, Is.Null);
        Assert.That(game.Board.HotSquare, Is.Null);
    }

    [Test]
    public void Knight_is_the_only_piece_that_can_jump_heat_but_landing_still_burns_it()
    {
        var jumping = Fondue("7k/2n5/8/3nR3/8/8/8/K7 w - - 0 1");
        Assert.That(jumping.TryMove(Move("e5", "d5")), Is.True);
        Assert.That(GameRules.HeatContact(jumping.Board, Move("c7", "e6")), Is.Null);
        Assert.That(jumping.TryMove(Move("c7", "e6")), Is.True);
        Assert.That(jumping.Board.At(Square("e6")),
            Is.EqualTo(new Piece(PieceType.Knight, PlayerColor.Black)));
        Assert.That(jumping.Board.HotSquare, Is.Null);

        var landing = Fondue("7k/5n2/8/3nR3/8/8/8/K7 w - - 0 1");
        Assert.That(landing.TryMove(Move("e5", "d5")), Is.True);
        Assert.That(GameRules.HeatContact(landing.Board, Move("f6", "d5")),
            Is.EqualTo(Square("d5")));
        Assert.That(landing.TryMove(Move("f6", "d5")), Is.True);
        Assert.That(landing.Board.At(Square("d5")), Is.Null);
        Assert.That(landing.Board.At(Square("f6")), Is.Null);
    }

    [Test]
    public void A_new_melt_refreshes_heat_instead_of_leaving_multiple_hot_tiles()
    {
        var game = Fondue("7k/3rB3/8/3n4/8/8/8/K2R4 w - - 0 1");
        Assert.That(game.TryMove(Move("d1", "d5")), Is.True);
        Assert.That(game.Board.HotSquare, Is.EqualTo(Square("d5")));
        Assert.That(game.TryMove(Move("d7", "e7")), Is.True);
        Assert.That(game.Board.HotSquare, Is.EqualTo(Square("e7")));
        Assert.That(game.Board.At(Square("e7")), Is.Null);
        Assert.That(game.Board.At(Square("d5")), Is.Null);
    }

    [Test]
    public void King_cannot_sacrifice_itself_on_a_hot_tile()
    {
        var game = Fondue("7k/3r4/8/3n4/8/8/8/K2R4 w - - 0 1");
        Assert.That(game.TryMove(Move("d1", "d5")), Is.True);
        var mock = game.Board.WithPiece(Square("h8"), null)
            .WithPiece(Square("d4"), new Piece(PieceType.King, PlayerColor.Black));
        Assert.That(MoveGenerator.Generate(mock, PlayerColor.Black),
            Does.Contain(Move("d4", "d5")));
        Assert.That(MoveGenerator.Legal(mock, PlayerColor.Black),
            Does.Not.Contain(Move("d4", "d5")));
    }

    [Test]
    public void Standard_positions_do_not_generate_or_keep_hot_tiles()
    {
        var normal = Create("7k/3r4/8/3n4/8/8/8/K2R4 w - - 0 1");
        Assert.That(normal.TryMove(Move("d1", "d5")), Is.True);
        Assert.That(normal.Board.HotSquare, Is.Null);
        Assert.That(normal.Board.At(Square("d5"))?.Type, Is.EqualTo(PieceType.Rook));

        var fondue = Fondue("7k/3r4/8/3n4/8/8/8/K2R4 w - - 0 1");
        Assert.That(fondue.TryMove(Move("d1", "d5")), Is.True);
        Assert.That(fondue.Board.WithMode(GameMode.Standard).HotSquare, Is.Null);
        Assert.That(fondue.Board.WithMode(GameMode.Fondue).HotSquare,
            Is.EqualTo(Square("d5")));
    }

    [Test]
    public void Pgn_round_trip_reconstructs_current_hot_tile_from_move_history()
    {
        var game = Fondue("7k/3r4/8/3n4/8/8/8/K2R4 w - - 0 1");
        Assert.That(game.TryMove(Move("d1", "d5")), Is.True);
        var pgn = SanNotation.ToPgn(game);
        var history = SanNotation.FromPgn(pgn, out var initial, out var result);
        var ports = Create();
        var restored = new ChessGame(ports.Events, ports.Repository, ports.Clock, initial);
        foreach (var move in history) Assert.That(restored.TryMove(move), Is.True);
        Assert.That(restored.Board.HotSquare, Is.EqualTo(Square("d5")));
        Assert.That(restored.TryMove(Move("d7", "d5")), Is.True);
        Assert.That(restored.Board.HotSquare, Is.Null);
    }

    [Test]
    public void Pgn_roundtrip_preserves_local_melt_and_result()
    {
        var game = Fondue("7k/6n1/5p2/8/8/8/8/K5R1 w - - 0 1");
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
