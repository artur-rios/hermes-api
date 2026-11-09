using Moq;

namespace ArturRios.Hermes.WebApi.Tests;

/// <summary>A clock a test moves by hand, so window boundaries can be tested exactly.</summary>
internal sealed class TestClock
{
    private readonly Mock<TimeProvider> _provider = new();

    public TestClock(DateTimeOffset now)
    {
        Now = now;
        _provider.Setup(provider => provider.GetUtcNow()).Returns(() => Now);
    }

    public DateTimeOffset Now { get; set; }

    public TimeProvider Provider => _provider.Object;
}
