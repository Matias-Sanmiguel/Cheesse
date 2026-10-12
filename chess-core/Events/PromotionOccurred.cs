using Chess.Core.Model;

namespace Chess.Core.Events;

public sealed record PromotionOccurred(Square Square, PieceType PromotedTo);
