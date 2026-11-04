using Chess.Core.Model;
using NUnit.Framework;
using static Chess.Tests.GameTestSupport;

namespace Chess.Tests;

public class FideDrawTests
{
    [Test]
    public void Fifty_moves_require_current_turn_claim_and_announced_move_does_not_apply_it()
    {
        var game = Create("7k/8/8/8/8/8/8/R6K w - - 99 50");
        var move = Move("a1", "a2");
        Assert.That(game.CanClaimDraw, Is.False);
        Assert.That(game.ClaimableDrawMoves(), Does.Contain(move));
        Assert.That(game.ClaimDraw(PlayerColor.Black, move), Is.False);
        Assert.That(game.ClaimDraw(PlayerColor.White, Move("a1", "b2")), Is.False);
        var board = game.Board;
        Assert.That(game.ClaimDraw(PlayerColor.White, move), Is.True);
        Assert.That(game.Board, Is.SameAs(board));
        Assert.That(game.History, Is.Empty);
        Assert.That(game.Status, Is.EqualTo(GameStatus.DrawByFiftyMove));
    }

    [Test]
    public void Threefold_requires_claim_and_can_be_claimed_with_intended_move()
    {
        var game = Create();
        Play(game, "g1f3", "g8f6", "f3g1", "f6g8", "g1f3", "g8f6", "f3g1");
        var intended = Move("f6", "g8");
        Assert.That(game.CanClaimDraw, Is.False);
        Assert.That(game.ClaimableDrawMoves(), Does.Contain(intended));
        Assert.That(game.ClaimDraw(PlayerColor.Black, intended), Is.True);
        Assert.That(game.History, Has.Count.EqualTo(7));
        Assert.That(game.Status, Is.EqualTo(GameStatus.DrawByRepetition));
    }

    [Test]
    public void Fivefold_is_automatic_and_threefold_current_position_is_claimable()
    {
        var game = Create();
        for (var cycle = 0; cycle < 4; cycle++)
        {
            Play(game, "g1f3", "g8f6", "f3g1", "f6g8");
            if (cycle == 1)
            {
                Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
                Assert.That(game.CanClaimDraw, Is.True);
            }
        }
        Assert.That(game.Status, Is.EqualTo(GameStatus.DrawByFivefoldRepetition));
        Assert.That(game.ClaimDraw(game.Turn), Is.False);
        Assert.That(game.Undo(), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.Ongoing));
        Assert.That(game.Redo(), Is.True);
        Assert.That(game.Status, Is.EqualTo(GameStatus.DrawByFivefoldRepetition));
    }

    [TestCase("7k/8/8/8/8/8/8/R6K w - - 149 75", "a1", "a2", GameStatus.DrawBySeventyFiveMove)]
    [TestCase("7k/8/5KQ1/8/8/8/8/8 w - - 149 75", "g6", "g7", GameStatus.Checkmate)]
    public void Seventyfive_is_automatic_but_checkmate_takes_precedence(string fen, string from, string to, GameStatus expected)
    {
        var game = Create(fen);
        Assert.That(game.TryMove(Move(from, to)), Is.True);
        Assert.That(game.Status, Is.EqualTo(expected));
    }
}
