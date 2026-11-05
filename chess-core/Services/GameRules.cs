using Chess.Core.Model;
using Chess.Core.Rules;

namespace Chess.Core.Services;

public static class GameRules
{
    public static GameStatus Evaluate(ChessGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return Evaluate(game.Board, game.Turn, game.Positions);
    }

    /// <summary>
    /// Capture moves that can melt <paramref name="side"/>'s king without
    /// destroying the opposing king. Used by UI to explain Fondue check.
    /// This has no effect on legal-move generation.
    /// </summary>
    public static IReadOnlyList<Move> KingMeltingThreats(Board board, PlayerColor side)
    {
        ArgumentNullException.ThrowIfNull(board);
        return board.Mode == GameMode.Fondue
            ? FondueRules.KingMeltingCaptures(board, side)
            : [];
    }

    /// <summary>
    /// Exact local impact of a Fondue capture, before committing the move.
    /// Pawn captures return no melting squares. The view must only ask for
    /// legal moves, as this prediction does not validate legality.
    /// </summary>
    public static IReadOnlyList<Square> MeltingSquares(Board board, Move move)
    {
        ArgumentNullException.ThrowIfNull(board);
        return board.Mode == GameMode.Fondue
            ? FondueRules.Preview(board, move)
            : [];
    }


    public static GameStatus Evaluate(Board board, PlayerColor side) =>
        Evaluate(board, side, null);

    internal static GameStatus Evaluate(Board board, PlayerColor side, IEnumerable<Board>? positions)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (board.Mode == GameMode.Fondue &&
            (!FondueRules.HasKing(board, PlayerColor.White) || !FondueRules.HasKing(board, PlayerColor.Black)))
        {
            if (!FondueRules.HasKing(board, PlayerColor.White) && !FondueRules.HasKing(board, PlayerColor.Black))
                throw new ArgumentException("Fondue requires at least one surviving king.", nameof(board));
            return GameStatus.KingMelted;
        }
        var modeRules = GameModeRuleCatalog.For(board.Mode);
        var lastMover = side == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
        if (modeRules.SpecialOutcome(board, lastMover) is { } victory) return victory;
        var inCheck = CheckDetector.IsInCheck(board, side);
        var legalMoves = MoveGenerator.Legal(board, side);

        // Mate and stalemate take precedence over move-count draws.
        if (legalMoves.Count == 0)
            return inCheck ? GameStatus.Checkmate : GameStatus.Stalemate;

        if (modeRules.InsufficientMaterialIsDraw && HasInsufficientMaterial(board))
            return GameStatus.DrawByInsufficientMaterial;

        // Claims use threefold / fifty moves; only fivefold / seventy-five are automatic.
        if (board.HalfmoveClock >= 150)
            return GameStatus.DrawBySeventyFiveMove;

        if (positions is not null)
        {
            var currentKey = PositionKey(board, side);
            if (positions.Count(position => PositionKey(position, position.Turn) == currentKey) >= 5)
                return GameStatus.DrawByFivefoldRepetition;
        }

        return inCheck ? GameStatus.Check : GameStatus.Ongoing;
    }

    internal static GameStatus? ClaimStatus(Board board, IEnumerable<Board> positions)
    {
        if (board.HalfmoveClock >= 100) return GameStatus.DrawByFiftyMove;
        var key = PositionKey(board, board.Turn);
        return positions.Count(position => PositionKey(position, position.Turn) == key) >= 3
            ? GameStatus.DrawByRepetition : null;
    }

    // shortcut: material checks cover common dead positions; use a solver for positional fortresses.
    public static bool CanPossiblyMate(Board board, PlayerColor side)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (board.Mode == GameMode.ReyDeLaPicada) return true;
        var own = new List<Piece>();
        var opponents = new List<Piece>();
        var bishopColors = new HashSet<int>();
        for (var index = 0; index < 64; index++)
        {
            var square = Square.FromIndex(index);
            if (board.At(square) is not Piece piece) continue;
            (piece.Color == side ? own : opponents).Add(piece);
            if (piece.Type == PieceType.Bishop) bishopColors.Add((square.File + square.Rank) % 2);
        }
        if (board.Mode == GameMode.Fondue) return own.Any(piece => piece.Type != PieceType.King);
        if (own.Any(piece => piece.Type is PieceType.Pawn or PieceType.Rook or PieceType.Queen)) return true;
        var knights = own.Count(piece => piece.Type == PieceType.Knight);
        var bishops = own.Count(piece => piece.Type == PieceType.Bishop);
        if (knights > 0)
            return knights > 1 || bishops > 0 || opponents.Any(piece => piece.Type is not (PieceType.King or PieceType.Queen));
        if (bishops > 0)
            return bishopColors.Count > 1 || own.Concat(opponents).Any(piece => piece.Type is PieceType.Pawn or PieceType.Knight);
        return false;
    }

    internal static string PositionKey(Board board, PlayerColor side)
    {
        var fields = FenSerializer.Write(board, side).Split(' ');
        // A nominal FEN target distinguishes positions only if a legal capture exists.
        var canCaptureEnPassant = SpecialMoves.EnPassant(board, side)
            .Any(move => !CheckDetector.IsInCheck(board.WithMove(move), side));
        if (!canCaptureEnPassant)
            fields[3] = "-";
        // Different Swiss boards must not share a repetition identity.
        var mode = board.Mode == GameMode.Standard ? "" : board.Mode + ":";
        if (board.Mode == GameMode.QuesoSuizo)
            mode += SwissCheeseRules.Format(board.SwissHoles) + ":";
        if (board.Mode == GameMode.Fondue)
            mode += (board.HotSquare?.ToString() ?? "-") + ":";
        return mode + string.Join(" ", fields.Take(4));
    }

    private static bool HasInsufficientMaterial(Board board)
    {
        var minors = new List<(PieceType Type, int SquareColor)>();
        var whiteKings = 0;
        var blackKings = 0;
        for (var index = 0; index < 64; index++)
        {
            var square = Square.FromIndex(index);
            if (board.At(square) is not Piece piece)
                continue;

            switch (piece.Type)
            {
                case PieceType.King:
                    if (piece.Color == PlayerColor.White) whiteKings++;
                    else blackKings++;
                    break;
                case PieceType.Bishop:
                case PieceType.Knight:
                    if (board.Mode == GameMode.Fondue) return false;
                    minors.Add((piece.Type, (square.File + square.Rank) % 2));
                    break;
                default:
                    return false;
            }
        }

        if (whiteKings != 1 || blackKings != 1)
            return false;

        // Bare kings, a single minor, or bishops confined to one square color.
        // Two knights and opposite-colored bishops can participate in a mate.
        return minors.Count <= 1 ||
            (minors.All(piece => piece.Type == PieceType.Bishop) &&
             minors.All(piece => piece.SquareColor == minors[0].SquareColor));
    }
}
