using ArturRios.Hermes.WebApi.Clients;
using ArturRios.Hermes.WebApi.Limits;
using ArturRios.Util.Test.Attributes;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Limits;

public class UsageCountersTests
{
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 11, 8, 12, 0, 20, TimeSpan.Zero));

    private static Client ClientWith(int perMinute, int perDay, string id = "client") =>
        new(id, ["hash"], "client@example.com", RecipientPolicy.Any, perMinute, perDay);

    private UsageCounters Counters() => new(_clock.Provider);

    [UnitFact]
    public void GivenTheLastMessageOfTheMinute_WhenCounting_ThenItIsAllowed()
    {
        // Given
        var counters = Counters();
        var client = ClientWith(perMinute: 2, perDay: 50);
        counters.TryCount(client);

        // When
        var decision = counters.TryCount(client);

        // Then
        Assert.True(decision.Allowed);
    }

    [UnitFact]
    public void GivenTheMinuteUsedUp_WhenCounting_ThenItIsRefusedUntilTheMinuteEnds()
    {
        // Given
        var counters = Counters();
        var client = ClientWith(perMinute: 1, perDay: 50);
        counters.TryCount(client);

        // When
        var decision = counters.TryCount(client);

        // Then
        Assert.False(decision.Allowed);
        Assert.Equal(40, decision.RetryAfterSeconds);
    }

    [UnitFact]
    public void GivenAPartOfASecondLeft_WhenRefused_ThenRetryAfterRoundsUp()
    {
        // Given
        _clock.Now = new DateTimeOffset(2026, 11, 8, 12, 0, 59, 500, TimeSpan.Zero);
        var counters = Counters();
        var client = ClientWith(perMinute: 1, perDay: 50);
        counters.TryCount(client);

        // When
        var decision = counters.TryCount(client);

        // Then
        Assert.Equal(1, decision.RetryAfterSeconds);
    }

    [UnitFact]
    public void GivenTheMinuteUsedUp_WhenTheNextMinuteStarts_ThenItIsAllowed()
    {
        // Given
        var counters = Counters();
        var client = ClientWith(perMinute: 1, perDay: 50);
        counters.TryCount(client);
        _clock.Now = new DateTimeOffset(2026, 11, 8, 12, 1, 0, TimeSpan.Zero);

        // When
        var decision = counters.TryCount(client);

        // Then
        Assert.True(decision.Allowed);
    }

    [UnitFact]
    public void GivenTheLastSecondOfTheMinute_WhenTheMinuteIsUsedUp_ThenItIsStillRefused()
    {
        // Given
        var counters = Counters();
        var client = ClientWith(perMinute: 1, perDay: 50);
        counters.TryCount(client);
        _clock.Now = new DateTimeOffset(2026, 11, 8, 12, 0, 59, 999, TimeSpan.Zero);

        // When
        var decision = counters.TryCount(client);

        // Then
        Assert.False(decision.Allowed);
    }

    [UnitFact]
    public void GivenTheDayUsedUp_WhenCounting_ThenItIsRefusedUntilMidnightUtc()
    {
        // Given
        _clock.Now = new DateTimeOffset(2026, 11, 8, 23, 0, 0, TimeSpan.Zero);
        var counters = Counters();
        var client = ClientWith(perMinute: 5, perDay: 1);
        counters.TryCount(client);

        // When
        var decision = counters.TryCount(client);

        // Then
        Assert.False(decision.Allowed);
        Assert.Equal(3600, decision.RetryAfterSeconds);
    }

    [UnitFact]
    public void GivenTheDayUsedUp_WhenMidnightUtcPasses_ThenItIsAllowed()
    {
        // Given
        _clock.Now = new DateTimeOffset(2026, 11, 8, 23, 59, 59, TimeSpan.Zero);
        var counters = Counters();
        var client = ClientWith(perMinute: 5, perDay: 1);
        counters.TryCount(client);
        _clock.Now = new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero);

        // When
        var decision = counters.TryCount(client);

        // Then
        Assert.True(decision.Allowed);
    }

    [UnitFact]
    public void GivenTheLastMessageOfTheDay_WhenCounting_ThenItIsAllowed()
    {
        // Given
        var counters = Counters();
        var client = ClientWith(perMinute: 5, perDay: 2);
        counters.TryCount(client);

        // When
        var decision = counters.TryCount(client);

        // Then
        Assert.True(decision.Allowed);
    }

    [UnitFact]
    public void GivenBothLimitsUsedUp_WhenCounting_ThenRetryAfterIsTheLongerWait()
    {
        // Given
        var counters = Counters();
        var client = ClientWith(perMinute: 1, perDay: 1);
        counters.TryCount(client);

        // When
        var decision = counters.TryCount(client);

        // Then
        Assert.Equal((int)TimeSpan.FromHours(12).TotalSeconds - 20, decision.RetryAfterSeconds);
    }

    [UnitFact]
    public void GivenAMessageRefusedByTheMinute_WhenTheNextMinuteStarts_ThenTheDayDidNotCountIt()
    {
        // Given
        var counters = Counters();
        var client = ClientWith(perMinute: 1, perDay: 2);
        counters.TryCount(client);
        counters.TryCount(client);
        _clock.Now = new DateTimeOffset(2026, 11, 8, 12, 1, 0, TimeSpan.Zero);

        // When
        var decision = counters.TryCount(client);

        // Then
        Assert.True(decision.Allowed);
    }

    [UnitFact]
    public void GivenOneClientAtItsLimit_WhenAnotherClientCounts_ThenItIsAllowed()
    {
        // Given
        var counters = Counters();
        counters.TryCount(ClientWith(perMinute: 1, perDay: 1, id: "busy"));

        // When
        var decision = counters.TryCount(ClientWith(perMinute: 1, perDay: 1, id: "quiet"));

        // Then
        Assert.True(decision.Allowed);
    }

    [UnitFact]
    public async Task GivenConcurrentMessages_WhenCounting_ThenNoMoreThanTheLimitAreAllowed()
    {
        // Given
        var counters = Counters();
        var client = ClientWith(perMinute: 100, perDay: 1000);

        // When
        var decisions = await Task.WhenAll(Enumerable.Range(0, 500).Select(_ => Task.Run(() => counters.TryCount(client))));

        // Then
        Assert.Equal(100, decisions.Count(decision => decision.Allowed));
    }
}
