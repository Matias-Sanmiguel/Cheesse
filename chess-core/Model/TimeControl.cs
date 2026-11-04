namespace Chess.Core.Model;

public readonly record struct TimeControl(int InitialSeconds, int IncrementSeconds)
{
    public static TimeControl Unlimited => new(0, 0);
}

public readonly record struct ClockSnapshot(double WhiteSeconds, double BlackSeconds, bool Running);
