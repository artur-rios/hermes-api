namespace ArturRios.Hermes.WebApi.Limits;

/// <summary>
/// Whether a message may go out, how long until the exhausted window ends when it may not, and whether this message
/// used up the last of the client's day, so the owner should be told.
/// </summary>
public readonly record struct LimitDecision(bool Allowed, int RetryAfterSeconds, bool AlertOwner = false)
{
    public static LimitDecision Allow { get; } = new(true, 0);

    public static LimitDecision AllowAndAlertOwner { get; } = new(true, 0, AlertOwner: true);

    public static LimitDecision Refuse(TimeSpan untilWindowEnds) =>
        new(false, (int)Math.Ceiling(untilWindowEnds.TotalSeconds));
}
