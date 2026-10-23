using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Chess.Core.Model;
using Chess.Core.Rules;

namespace Chess.Core.Services;

public static class SanNotation
{
    public static string ToSan(Board board, Move move)
    {
        ArgumentNullException.ThrowIfNull(board);
        var legalMoves = MoveGenerator.Legal(board, board.Turn);
        if (!legalMoves.Contains(move))
            throw new ArgumentException("SAN requires a legal move for the active color.", nameof(move));

        return FormatMove(board, move, legalMoves);
    }

    private static string FormatMove(Board board, Move move, IReadOnlyList<Move> legalMoves)
    {
        var piece = board.At(move.From)!.Value;
        var text = new StringBuilder();
        if (piece.Type == PieceType.King && Math.Abs(move.To.File - move.From.File) == 2)
        {
            text.Append(move.To.File == 6 ? "O-O" : "O-O-O");
        }
        else
        {
            var capture = board.At(move.To) is not null ||
                (piece.Type == PieceType.Pawn && move.From.File != move.To.File);

            if (piece.Type != PieceType.Pawn)
            {
                text.Append(FenSerializer.PieceSymbol(piece.Type));
                var alternatives = legalMoves.Where(other =>
                    other.From != move.From && other.To == move.To &&
                    board.At(other.From)?.Type == piece.Type).ToArray();

                if (alternatives.Length > 0)
                {
                    if (alternatives.All(other => other.From.File != move.From.File))
                        text.Append((char)('a' + move.From.File));
                    else if (alternatives.All(other => other.From.Rank != move.From.Rank))
                        text.Append(move.From.Rank + 1);
                    else
                        text.Append(move.From);
                }
            }
            else if (capture)
            {
                text.Append((char)('a' + move.From.File));
            }

            if (capture) text.Append('x');
            text.Append(move.To);
            if (move.Promotion is PieceType promotion)
                text.Append('=').Append(FenSerializer.PieceSymbol(promotion));
        }

        var next = board.WithMove(move);
        if (CheckDetector.IsInCheck(next, next.Turn))
            text.Append(MoveGenerator.Legal(next, next.Turn).Count == 0 ? '#' : '+');

        return text.ToString();
    }

    public static string ToPgn(IReadOnlyList<Move> history) =>
        ToPgn(history, FenSerializer.Load(FenSerializer.InitialPosition));

    public static string ToPgn(ChessGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return ToPgn(game.History, game.InitialBoard);
    }

    public static string ToPgn(IReadOnlyList<Move> history, Board initialBoard)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(initialBoard);
        var board = initialBoard;
        var positions = new List<Board> { board };
        var status = GameRules.Evaluate(board, board.Turn, positions);
        var tokens = new List<string>();

        foreach (var move in history)
        {
            if (status is not (GameStatus.Ongoing or GameStatus.Check))
                throw new ArgumentException("History continues after the game has ended.", nameof(history));

            if (board.Turn == PlayerColor.White)
                tokens.Add(board.FullmoveNumber.ToString(CultureInfo.InvariantCulture) + ".");
            else if (tokens.Count == 0)
                tokens.Add(board.FullmoveNumber.ToString(CultureInfo.InvariantCulture) + "...");

            tokens.Add(ToSan(board, move));
            board = board.WithMove(move);
            positions.Add(board);
            status = GameRules.Evaluate(board, board.Turn, positions);
        }

        var result = Result(status, board.Turn);
        tokens.Add(result);
        var pgn = new StringBuilder();
        pgn.Append("[Event \"?\"]\n[Site \"?\"]\n[Date \"????.??.??\"]\n");
        pgn.Append("[Round \"?\"]\n[White \"?\"]\n[Black \"?\"]\n");
        pgn.Append("[Result \"").Append(result).Append("\"]\n");

        var initialFen = FenSerializer.Write(initialBoard);
        if (initialFen != FenSerializer.InitialPosition)
        {
            pgn.Append("[SetUp \"1\"]\n");
            pgn.Append("[FEN \"").Append(initialFen).Append("\"]\n");
        }

        pgn.Append('\n');
        var lineLength = 0;
        foreach (var token in tokens)
        {
            if (lineLength > 0)
            {
                if (lineLength + 1 + token.Length > 79)
                {
                    pgn.Append('\n');
                    lineLength = 0;
                }
                else
                {
                    pgn.Append(' ');
                    lineLength++;
                }
            }

            pgn.Append(token);
            lineLength += token.Length;
        }

        return pgn.Append('\n').ToString();
    }

    // Imports the main line. Comments, annotations and variations are not history moves.
    public static IReadOnlyList<Move> FromPgn(string pgn, out Board initialBoard)
    {
        ArgumentNullException.ThrowIfNull(pgn);
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        var remaining = pgn.TrimStart();
        while (remaining.StartsWith('['))
        {
            var tag = Regex.Match(remaining, """^\[(\w+)\s+"((?:\\.|[^"\\])*)"\s*\]\s*""");
            if (!tag.Success || !tags.TryAdd(tag.Groups[1].Value,
                    tag.Groups[2].Value.Replace("\\\"", "\"").Replace("\\\\", "\\")))
                throw new FormatException("Invalid or duplicate PGN tag.");
            remaining = remaining[tag.Length..];
        }

        if (tags.TryGetValue("SetUp", out var setup) && setup is not ("0" or "1"))
            throw new FormatException("Invalid PGN SetUp tag.");
        if (tags.ContainsKey("FEN") != (setup == "1"))
            throw new FormatException("A custom PGN position requires both SetUp and FEN tags.");

        initialBoard = FenSerializer.Load(setup == "1" ? tags["FEN"] : FenSerializer.InitialPosition);
        var board = initialBoard;
        var history = new List<Move>();
        var positions = new List<Board> { board };
        string? termination = null;

        foreach (var rawToken in RemoveAnnotations(remaining)
                     .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = Regex.Replace(rawToken, @"^\d+\.(?:\.\.)?", "");
            token = Regex.Replace(token, @"\$\d+", "").TrimEnd('!', '?');
            if (token.Length == 0 || token == "...")
                continue;

            if (termination is not null)
                throw new FormatException("Unexpected text after PGN termination.");

            if (token is "1-0" or "0-1" or "1/2-1/2" or "*")
            {
                termination = token;
                continue;
            }

            if (GameRules.Evaluate(board, board.Turn, positions) is not (GameStatus.Ongoing or GameStatus.Check))
                throw new FormatException("PGN continues after the game has ended.");

            var legalMoves = MoveGenerator.Legal(board, board.Turn);
            var normalized = token.Replace('0', 'O');
            var matches = legalMoves.Where(move => FormatMove(board, move, legalMoves) == normalized).ToArray();
            if (matches.Length != 1)
                throw new FormatException($"Invalid or ambiguous SAN move: {token}.");

            history.Add(matches[0]);
            board = board.WithMove(matches[0]);
            positions.Add(board);
        }

        if (termination is null ||
            (tags.TryGetValue("Result", out var declaredResult) && declaredResult != termination))
            throw new FormatException("Missing or inconsistent PGN result.");

        return history.AsReadOnly();
    }

    private static string RemoveAnnotations(string text)
    {
        var mainLine = new StringBuilder();
        var variationDepth = 0;
        var inComment = false;
        var lineComment = false;
        foreach (var symbol in text)
        {
            if (lineComment)
            {
                if (symbol is '\r' or '\n') lineComment = false;
                else continue;
            }
            if (inComment)
            {
                if (symbol == '}') inComment = false;
                continue;
            }
            if (symbol == '{')
            {
                inComment = true;
                mainLine.Append(' ');
            }
            else if (symbol == ';')
            {
                lineComment = true;
                mainLine.Append(' ');
            }
            else if (symbol == '(')
            {
                variationDepth++;
                mainLine.Append(' ');
            }
            else if (symbol == ')')
            {
                if (variationDepth == 0)
                    throw new FormatException("Unmatched PGN variation.");
                variationDepth--;
                mainLine.Append(' ');
            }
            else if (symbol == '}')
            {
                throw new FormatException("Unmatched PGN comment.");
            }
            else if (variationDepth == 0)
            {
                mainLine.Append(symbol);
            }
        }

        if (inComment || variationDepth != 0)
            throw new FormatException("Unterminated PGN comment or variation.");
        return mainLine.ToString();
    }

    private static string Result(GameStatus status, PlayerColor turn) => status switch
    {
        GameStatus.Checkmate => turn == PlayerColor.White ? "0-1" : "1-0",
        GameStatus.Stalemate or GameStatus.DrawByFiftyMove or
            GameStatus.DrawByRepetition or GameStatus.DrawByInsufficientMaterial => "1/2-1/2",
        _ => "*",
    };
}
