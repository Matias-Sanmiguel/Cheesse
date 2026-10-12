using Chess.Core.Model;

namespace Chess.Core.Events;

public sealed record GameEnded(GameStatus Status);
