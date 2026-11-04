using Chess.Core.Model;
using Chess.Core.Services;
using NUnit.Framework;
using static Chess.Tests.GameTestSupport;

namespace Chess.Tests;

public class PicadaModeTests
{
    private static ChessGame Picada(string fen = FenSerializer.InitialPosition) =>
        Create(GameMode.ReyDeLaPicada, fen);

    [Test]
    public void A_legal_white_king_move_to_the_center_wins_immediately()
    {
        var game = Picada("7k/8/8/8/8/4K3/8/8 w - - 0 1");
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing),
            "Bare kings can still win by reaching the center.");
        Assert.That(game.TryMove(Move("e3", "e4")), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.PicadaVictory));
        Assert.That(game.Turn, Is.EqualTo(PlayerColor.Black));
        Assert.That(game.LegalMoves(), Is.Empty);
        Assert.That(game.TryMove(Move("h8", "g8")), Is.False);
    }

    [Test]
    public void A_legal_black_king_move_to_the_center_wins()
    {
        var game = Picada("8/8/3k4/8/8/8/8/7K b - - 0 1");
        Assert.That(game.TryMove(Move("d6", "d5")), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.PicadaVictory));
        Assert.That(game.Turn, Is.EqualTo(PlayerColor.White));
    }

    [Test]
    public void An_attacked_center_square_is_not_a_legal_king_destination()
    {
        var game = Picada("k3r3/8/8/8/8/3K4/8/8 w - - 0 1");
        var before = game.Board;
        Assert.That(game.TryMove(Move("d3", "e4")), Is.False);
        Assert.That(game.Board, Is.SameAs(before));
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    [Test]
    public void Undo_redo_and_restore_keep_variant_and_victory()
    {
        var game = Picada("7k/8/8/8/8/4K3/8/8 w - - 0 1");
        Play(game, "e3e4");
        var saved = game.CreateMemento();
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.Mode, Is.EqualTo(GameMode.ReyDeLaPicada));
        Assert.That(game.Redo(), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.PicadaVictory));

        var another = Create();
        another.Restore(saved);
        Assert.That(another.Mode, Is.EqualTo(GameMode.ReyDeLaPicada));
        Assert.That(another.Status, Is.EqualTo(GameStatus.PicadaVictory));
        Assert.That(another.Undo(), Is.True);
        Assert.That(another.Status, Is.EqualTo(GameStatus.Ongoing));
    }

    [Test]
    public void Picada_still_allows_checkmate_and_classic_stays_unchanged()
    {
        var game = Picada();
        Play(game, "f2f3", "e7e5", "g2g4", "d8h4");
        Assert.That(game.Status, Is.EqualTo(GameStatus.Checkmate));
        var classic = Create("7k/8/8/8/8/4K3/8/8 w - - 0 1");
        Assert.That(classic.Status, Is.EqualTo(GameStatus.DrawByInsufficientMaterial));
        Assert.That(classic.Mode, Is.EqualTo(GameMode.Classic));
    }

    [Test]
    public void Variant_pgn_has_a_winner_and_can_be_replayed()
    {
        var game = Picada("7k/8/8/8/8/4K3/8/8 w - - 0 1");
        Play(game, "e3e4");
        var pgn = SanNotation.ToPgn(game);
        Assert.That(pgn, Does.Contain("[Variant \"Rey de la Picada\"]"));
        Assert.That(pgn, Does.Contain("[Result \"1-0\"]"));
        var moves = SanNotation.FromPgn(pgn, out var initial, GameMode.ReyDeLaPicada);
        Assert.That(moves, Is.EqualTo(game.History));
        Assert.That(FenSerializer.Write(initial), Is.EqualTo(FenSerializer.Write(game.InitialBoard)));
    }
}
