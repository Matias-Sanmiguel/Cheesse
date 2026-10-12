namespace Chess.Core.Model;

public readonly record struct Square
{
    public int File { get; }
    public int Rank { get; }

    public Square(int file, int rank)
    {
        if (file is < 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(file));
        if (rank is < 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(rank));

        File = file;
        Rank = rank;
    }

    public int Index => (Rank * 8) + File;

    public static Square FromIndex(int index)
    {
        if (index is < 0 or > 63)
            throw new ArgumentOutOfRangeException(nameof(index));

        return new Square(index % 8, index / 8);
    }

    public override string ToString() => $"{(char)('a' + File)}{Rank + 1}";
}
