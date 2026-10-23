using System.Globalization;
using System.Text;
using Chess.Core.Model;

namespace Chess.Core.Services;

public static class FenSerializer
{
    public const string InitialPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    public static string Write(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return Write(board, board.Turn);
    }

    public static string Write(Board board, PlayerColor turn)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (!Enum.IsDefined(turn))
            throw new ArgumentOutOfRangeException(nameof(turn));

        var text = new StringBuilder();
        for (var rank = 7; rank >= 0; rank--)
        {
            var empty = 0;
            for (var file = 0; file < 8; file++)
            {
                if (board.At(new Square(file, rank)) is not Piece piece)
                {
                    empty++;
                    continue;
                }

                if (empty > 0)
                {
                    text.Append(empty);
                    empty = 0;
                }

                var symbol = PieceSymbol(piece.Type);
                text.Append(piece.Color == PlayerColor.White ? symbol : char.ToLowerInvariant(symbol));
            }

            if (empty > 0)
                text.Append(empty);
            if (rank > 0)
                text.Append('/');
        }

        text.Append(turn == PlayerColor.White ? " w " : " b ");
        var rights = board.Castling;
        if (rights.WhiteKingside) text.Append('K');
        if (rights.WhiteQueenside) text.Append('Q');
        if (rights.BlackKingside) text.Append('k');
        if (rights.BlackQueenside) text.Append('q');
        if (rights == CastlingRights.None) text.Append('-');

        text.Append(' ').Append(board.EnPassant?.ToString() ?? "-");
        text.Append(' ').Append(board.HalfmoveClock.ToString(CultureInfo.InvariantCulture));
        text.Append(' ').Append(board.FullmoveNumber.ToString(CultureInfo.InvariantCulture));
        return text.ToString();
    }

    public static Board Load(string fen)
    {
        ArgumentNullException.ThrowIfNull(fen);
        var fields = fen.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 6)
            throw new FormatException("FEN must contain exactly six fields.");

        var ranks = fields[0].Split('/');
        if (ranks.Length != 8)
            throw new FormatException("FEN must contain eight ranks.");

        var board = Board.Empty();
        for (var row = 0; row < 8; row++)
        {
            var file = 0;
            var previousWasDigit = false;
            foreach (var symbol in ranks[row])
            {
                if (symbol is >= '1' and <= '8')
                {
                    if (previousWasDigit)
                        throw new FormatException("Adjacent empty-square counts are not valid FEN.");
                    file += symbol - '0';
                    previousWasDigit = true;
                }
                else
                {
                    if (file >= 8)
                        throw new FormatException("FEN rank exceeds eight squares.");
                    var type = char.ToUpperInvariant(symbol) switch
                    {
                        'P' => PieceType.Pawn,
                        'N' => PieceType.Knight,
                        'B' => PieceType.Bishop,
                        'R' => PieceType.Rook,
                        'Q' => PieceType.Queen,
                        'K' => PieceType.King,
                        _ => throw new FormatException($"Invalid FEN piece: {symbol}."),
                    };
                    var color = char.IsUpper(symbol) ? PlayerColor.White : PlayerColor.Black;
                    board = board.WithPiece(new Square(file++, 7 - row), new Piece(type, color));
                    previousWasDigit = false;
                }
            }

            if (file != 8)
                throw new FormatException("Each FEN rank must contain exactly eight squares.");
        }

        var turn = fields[1] switch
        {
            "w" => PlayerColor.White,
            "b" => PlayerColor.Black,
            _ => throw new FormatException("Invalid FEN active color."),
        };

        var castling = fields[2];
        if (castling != "-" &&
            (castling.Length == 0 || castling.Any(symbol => !"KQkq".Contains(symbol)) ||
             castling.Distinct().Count() != castling.Length))
            throw new FormatException("Invalid FEN castling rights.");
        board = board.WithCastling(new CastlingRights(
            castling.Contains('K'), castling.Contains('Q'), castling.Contains('k'), castling.Contains('q')));

        if (fields[3] != "-")
        {
            var target = fields[3];
            if (target.Length != 2 || target[0] is < 'a' or > 'h' ||
                target[1] != (turn == PlayerColor.White ? '6' : '3'))
                throw new FormatException("Invalid FEN en passant target.");
            board = board.WithEnPassant(new Square(target[0] - 'a', target[1] - '1'));
        }

        if (!int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out var halfmove) ||
            !int.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out var fullmove) ||
            fullmove < 1)
            throw new FormatException("Invalid FEN move counters.");

        return board.WithState(turn, halfmove, fullmove);
    }

    internal static char PieceSymbol(PieceType type) => type switch
    {
        PieceType.Pawn => 'P',
        PieceType.Knight => 'N',
        PieceType.Bishop => 'B',
        PieceType.Rook => 'R',
        PieceType.Queen => 'Q',
        PieceType.King => 'K',
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
