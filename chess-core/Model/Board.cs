using Chess.Core.Rules;

namespace Chess.Core.Model;

public sealed class Board
{
    private readonly Piece?[] _squares;
    private readonly ulong _swissHoleMask;

    private Board(Piece?[] squares, CastlingRights castling, Square? enPassant,
        PlayerColor turn = PlayerColor.White, int halfmoveClock = 0, int fullmoveNumber = 1,
        GameMode mode = GameMode.Standard, ulong swissHoleMask = 0,
        Square? hotSquare = null)
    {
        _squares = squares;
        _swissHoleMask = swissHoleMask;
        Castling = castling;
        EnPassant = enPassant;
        Turn = turn;
        HalfmoveClock = halfmoveClock;
        FullmoveNumber = fullmoveNumber;
        Mode = mode;
        HotSquare = mode == GameMode.Fondue ? hotSquare : null;
    }

    public PlayerColor Turn { get; }

    public GameMode Mode { get; }

    // The one-turn residual heat left by a Fondue capture. Only the immediate
    // opposing move can interact with it; every completed move clears it.
    // Its presence is part of the immutable chess position (undo/replay/save).
    public Square? HotSquare { get; }

    internal Board WithHotSquare(Square square)
    {
        if (Mode != GameMode.Fondue || At(square) is not null)
            throw new InvalidOperationException("Heat requires an empty square in Fondue.");
        return new Board((Piece?[])_squares.Clone(), Castling, EnPassant,
            Turn, HalfmoveClock, FullmoveNumber, Mode, _swissHoleMask, square);
    }

    // Immutable holes belong to this board, never to a global random layout.
    public IReadOnlyList<Square> SwissHoles => Mode == GameMode.QuesoSuizo
        ? SwissCheeseRules.FromMask(_swissHoleMask)
        : Array.Empty<Square>();

    public bool IsBlocked(Square square) => Mode == GameMode.QuesoSuizo &&
        (_swissHoleMask & (1UL << square.Index)) != 0;

    public Board WithMode(GameMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (mode == GameMode.QuesoSuizo)
        {
            if (Mode == GameMode.QuesoSuizo)
                return new Board((Piece?[])_squares.Clone(), Castling, EnPassant,
                    Turn, HalfmoveClock, FullmoveNumber, mode, _swissHoleMask);
            return WithSwissHoles(SwissCheeseRules.Generate(this));
        }
        return new Board((Piece?[])_squares.Clone(), Castling, EnPassant,
            Turn, HalfmoveClock, FullmoveNumber, mode,
            hotSquare: mode == GameMode.Fondue ? HotSquare : null);
    }

    // Explicit layout for replay/import: does not regenerate holes.
    public Board WithSwissHoles(IEnumerable<Square> holes)
    {
        var mask = SwissCheeseRules.ValidateLayout(this, holes);
        return new Board((Piece?[])_squares.Clone(), Castling, EnPassant,
            Turn, HalfmoveClock, FullmoveNumber, GameMode.QuesoSuizo, mask);
    }

    public int HalfmoveClock { get; }

    public int FullmoveNumber { get; }

    public CastlingRights Castling { get; }

    public Board WithState(PlayerColor turn, int halfmoveClock, int fullmoveNumber)
    {
        if (!Enum.IsDefined(turn))
            throw new ArgumentOutOfRangeException(nameof(turn));
        ArgumentOutOfRangeException.ThrowIfNegative(halfmoveClock);
        ArgumentOutOfRangeException.ThrowIfLessThan(fullmoveNumber, 1);

        return new Board((Piece?[])_squares.Clone(), Castling, EnPassant,
            turn, halfmoveClock, fullmoveNumber, Mode, _swissHoleMask, HotSquare);
    }

    public Square? EnPassant { get; } //Guarda la casilla a la que puede ir para capturar al paso, por ejemplo si el peón va de d7 a d5, el otro peón puede ir a d6 para capturarlo

    public static Board Empty() => new(new Piece?[64], CastlingRights.None, null);

    public Piece? At(Square square) => _squares[square.Index];

    public Board WithPiece(Square square, Piece? piece)
    {
        if (piece is not null && IsBlocked(square))
            throw new InvalidOperationException("No se puede colocar una pieza dentro de un agujero.");
        var next = (Piece?[])_squares.Clone();
        next[square.Index] = piece;
        return new Board(next, Castling, EnPassant, Turn, HalfmoveClock, FullmoveNumber, Mode, _swissHoleMask, HotSquare);
    }

    public Board WithCastling(CastlingRights castling) => new((Piece?[])_squares.Clone(), castling, EnPassant, Turn, HalfmoveClock, FullmoveNumber, Mode, _swissHoleMask, HotSquare);

    public Board WithEnPassant(Square? enPassant) => new((Piece?[])_squares.Clone(), Castling, enPassant, Turn, HalfmoveClock, FullmoveNumber, Mode, _swissHoleMask, HotSquare);


    public Board WithMove(Move move)
    {
        if (IsBlocked(move.From) || IsBlocked(move.To))
            throw new InvalidOperationException("No se puede mover una pieza desde o hacia un agujero.");
        var next = (Piece?[])_squares.Clone();

        Piece? piece = next[move.From.Index];
        Piece? capturedPiece = next[move.To.Index];

        if (piece == null) throw new InvalidOperationException("No hay una pieza en la casilla de origen.");

        Piece moving = piece.Value;

        // Traveling over hot Fondue without a knight burns the moving piece
        // BEFORE it reaches its requested destination. Anything on the far
        // side (including a potential capture) remains untouched. The attempt
        // still consumes the player's turn and clears residual heat.
        if (Mode == GameMode.Fondue && FondueRules.BurnsBeforeDestination(this, move))
        {
            next[move.From.Index] = null;
            var rights = moving.Type switch
            {
                PieceType.King when moving.Color == PlayerColor.White =>
                    Castling with { WhiteKingside = false, WhiteQueenside = false },
                PieceType.King => Castling with { BlackKingside = false, BlackQueenside = false },
                PieceType.Rook => WithoutRookRight(Castling, move.From, moving.Color),
                _ => Castling
            };
            var opponent = moving.Color == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
            var moveNumber = FullmoveNumber + (moving.Color == PlayerColor.Black ? 1 : 0);
            return new Board(next, rights, null, opponent, 0, moveNumber, Mode, _swissHoleMask);
        }

        next[move.From.Index] = null;

        //Captura al paso (enPassant)
        if (moving.Type == PieceType.Pawn && EnPassant == move.To && move.From.File != move.To.File && capturedPiece == null)
        {
            Square capturedSquare = new Square(move.To.File, move.From.Rank);
            Piece? capturedPawn = next[capturedSquare.Index];

            if (capturedPawn is Piece pawn && pawn.Type == PieceType.Pawn && pawn.Color != moving.Color)
            {
                next[capturedSquare.Index] = null;
            }
        }

        //Promoción
        Piece placedPiece = moving;

        if (moving.Type == PieceType.Pawn && move.Promotion is PieceType promotion && move.To.Rank == (moving.Color == PlayerColor.White ? 7 : 0))
        {
            placedPiece = moving with { Type = promotion };
        }

        next[move.To.Index] = placedPiece;

        //Enroque: mover también la torre
        int homeRank = moving.Color == PlayerColor.White ? 0 : 7;

        if (moving.Type == PieceType.King && move.From.File == 4 && move.From.Rank == homeRank && move.To.Rank == homeRank && (move.To.File == 6 || move.To.File == 2))
        {
            bool kingside = move.To.File == 6;

            Square rookFrom = new Square(kingside ? 7 : 0, homeRank);
            Square rookTo = new Square(kingside ? 5 : 3, homeRank);

            if (next[rookFrom.Index] is Piece rook && rook.Type == PieceType.Rook && rook.Color == moving.Color)
            {
                next[rookFrom.Index] = null;
                next[rookTo.Index] = rook;
            }
        }

        CastlingRights newCastling = Castling;

        //Si se mueve el rey, pierde ambos derechos
        if (moving.Type == PieceType.King)
        {
            newCastling = moving.Color == PlayerColor.White ? newCastling with { WhiteKingside = false, WhiteQueenside = false } : newCastling with { BlackKingside = false, BlackQueenside = false };
        }

        //Si se mueve una torre desde su casilla inicial
        if (moving.Type == PieceType.Rook)
        {
            newCastling = WithoutRookRight(newCastling, move.From, moving.Color);
        }

        //Si se captura una torre en su casilla inicial
        if (capturedPiece is Piece capturedRook && capturedRook.Type == PieceType.Rook)
        {
            newCastling = WithoutRookRight(newCastling, move.To, capturedRook.Color);
        }

        //Actualizar en passant
        Square? newEnPassant = null;

        if (moving.Type == PieceType.Pawn && move.From.File == move.To.File)
        {
            int initialRank = moving.Color == PlayerColor.White ? 1 : 6;
            int direction = moving.Color == PlayerColor.White ? 1 : -1;

            if (move.From.Rank == initialRank && move.To.Rank - move.From.Rank == direction * 2)
            {
                int middleRank = (move.From.Rank + move.To.Rank) / 2;
                Square middleSquare = new Square(move.From.File, middleRank);

                if (At(middleSquare) == null && At(move.To) == null)
                {
                    newEnPassant = middleSquare;
                }
            }
        }

        // Burn the piece that lands on residual heat. Only the knight may
        // leap over the hot square; castling may burn the rook separately if
        // its path crosses heat but the king's path does not.
        var hotContact = Mode == GameMode.Fondue ? FondueRules.HotLanding(this, move) : null;
        bool burnsRook = hotContact is not null &&
            !FondueRules.HotBurnsMover(this, move);
        Square burnedSquare = burnsRook
            ? new Square(move.To.File > move.From.File ? 5 : 3, move.From.Rank)
            : move.To;
        bool burned = hotContact is not null && next[burnedSquare.Index] is not null;
        if (burned) next[burnedSquare.Index] = null;

        int halfmoveClock = moving.Type == PieceType.Pawn || capturedPiece is not null || burned
            ? 0
            : HalfmoveClock + 1;
        int fullmoveNumber = FullmoveNumber + (moving.Color == PlayerColor.Black ? 1 : 0);
        PlayerColor nextTurn = moving.Color == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;

        var board = new Board(next, newCastling, newEnPassant, nextTurn, halfmoveClock, fullmoveNumber, Mode, _swissHoleMask);
        // A new Fondue capture replaces any old heat. Otherwise, the old
        // square cools immediately after the opponent's single responding move.
        return Mode == GameMode.Fondue && FondueRules.IsMeltingCapture(this, move)
            ? FondueRules.Melt(board, move.To).WithHotSquare(move.To)
            : board;
    }


    private static CastlingRights WithoutRookRight(CastlingRights rights, Square square, PlayerColor color)
    {
        if (color == PlayerColor.White && square.Rank == 0)
        {
            if (square.File == 0) return rights with { WhiteQueenside = false };

            if (square.File == 7) return rights with { WhiteKingside = false };
        }

        if (color == PlayerColor.Black && square.Rank == 7)
        {
            if (square.File == 0) return rights with { BlackQueenside = false };

            if (square.File == 7) return rights with { BlackKingside = false };
        }

        return rights;
    }


    public bool IsAttacked(Square square, PlayerColor by) =>
        AttackDetector.IsAttacked(this, square, by);
}
