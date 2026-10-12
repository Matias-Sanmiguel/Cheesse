using Chess.Core.Model;

namespace Chess.Core.Events;

public sealed record PieceMoved(Move Move, Piece Piece);
