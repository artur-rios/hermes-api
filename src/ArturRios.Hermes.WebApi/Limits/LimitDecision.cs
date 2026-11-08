namespace ArturRios.Hermes.WebApi.Limits;

/// <summary>Whether a message may go out, and otherwise how long until the exhausted window ends.</summary>
public readonly record struct LimitDecision(bool Allowed, int RetryAfterSeconds)
{
    public static LimitDecision Allow { get; } = new(true, 0);

    public static LimitDecision Refuse(TimeSpan untilWindowEnds) =>
        new(false, (int)Math.Ceiling(untilWindowEnds.TotalSeconds));
}
