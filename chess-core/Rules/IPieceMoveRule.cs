using Chess.Core.Model;

namespace Chess.Core.Rules;

public interface IPieceMoveRule
{
    void AddMoves(Board board, Square from, PlayerColor side, List<Move> moves);
}
