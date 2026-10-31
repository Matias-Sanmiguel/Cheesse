using Cheesse.Game.Adapters.Ai;
using NUnit.Framework;
using static Chess.Tests.GameTestSupport;

namespace Chess.Tests;

public class RandomAITests
{
    [Test]
    public void Seed_reproduces_legal_choices_through_a_game()
    {
        var first = new RandomAI(123);
        var second = new RandomAI(123);
        var game = Create();
        for (var ply = 0; ply < 40 && game.LegalMoves().Count > 0; ply++)
        {
            var move = first.ChooseMove(game);
            Assert.That(second.ChooseMove(game), Is.EqualTo(move));
            Assert.That(game.LegalMoves(), Does.Contain(move));
            Assert.That(game.TryMove(move), Is.True);
        }
    }

    [Test]
    public void Finished_game_and_null_are_rejected()
    {
        var ai = new RandomAI(1);
        Assert.Throws<InvalidOperationException>(() => ai.ChooseMove(Create("7k/8/5KQ1/8/8/8/8/8 b - - 0 1")));
        Assert.Throws<ArgumentNullException>(() => ai.ChooseMove(null!));
    }
}
