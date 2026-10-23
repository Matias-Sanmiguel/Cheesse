using Chess.Core.Model;
using Chess.Core.Ports;
using Chess.Core.Services;
using NUnit.Framework;

namespace Chess.Tests;

internal static class GameTestSupport
{
    internal static ChessGame Create(string fen = FenSerializer.InitialPosition) =>
        new(new Events(), new Repository(), new Clock(), FenSerializer.Load(fen));

    internal static Move Move(string from, string to, PieceType? promotion = null) =>
        new(Square(from), Square(to), promotion);

    internal static Square Square(string name) => new(name[0] - 'a', name[1] - '1');

    internal static void Play(ChessGame game, params string[] moves)
    {
        foreach (var move in moves)
            Assert.That(game.TryMove(Move(move[..2], move[2..4])), Is.True, move);
    }

    private sealed class Events : IEventPublisher
    {
        public void Publish<T>(T domainEvent) { }
    }

    private sealed class Repository : IGameRepository
    {
        public void Save(ChessGame game) => throw new NotSupportedException();
        public ChessGame Load(string id) => throw new NotSupportedException();
    }

    private sealed class Clock : IClock
    {
        public double ElapsedSeconds => 0;
    }
}
