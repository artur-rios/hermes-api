using System.Net;
using System.Net.Sockets;
using ArturRios.Hermes.WebApi.Emails;
using ArturRios.Hermes.WebApi.Platform;
using ArturRios.Util.Test.Attributes;
using Microsoft.Extensions.Logging;
using MailgunHealth = ArturRios.Hermes.WebApi.Platform.InternalHealthEndpoint.MailgunHealth;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Platform;

/// <summary>
/// The check behind <c>/internal/health</c>, called directly: everything a caller does before it waits for a check is
/// synchronous, so when <c>GetAsync</c> hands back a task the caller has already joined or started a check, and the
/// tests can order callers without sleeping.
/// </summary>
public class MailgunHealthTests
{
    private const string Secret = "secret-detail-that-must-not-leak";

    private readonly TestClock _clock = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
    private readonly RecordingLogger<MailgunHealth> _logger = new();

    private MailgunHealth Health(HealthProbeHandler mailgun, bool configured = true)
    {
        var settings = configured ? new ClientSettings().WithMailgun() : new ClientSettings();

        return new MailgunHealth(MailgunSettings.From(settings.Build()), new HandlerClientFactory(mailgun), _clock.Provider, _logger);
    }

    private static Task<InternalHealthEndpoint.Service> Check(MailgunHealth health, CancellationToken? token = null) =>
        health.GetAsync(token ?? TestContext.Current.CancellationToken);

    private static HttpRequestException Refusal() =>
        new(Secret, new SocketException((int)SocketError.ConnectionRefused));

    private IEnumerable<string> Warnings() =>
        _logger.Entries.Where(entry => entry.Level == LogLevel.Warning).Select(entry => entry.Message);

    [UnitFact]
    public async Task GivenMailgunNotConfigured_WhenChecked_ThenItIsNotConfiguredAndNotCalled()
    {
        // Given
        var mailgun = HealthProbeHandler.Answering(HttpStatusCode.OK);

        // When
        var service = await Check(Health(mailgun, configured: false));

        // Then
        Assert.Equal(new InternalHealthEndpoint.Service("Mailgun", "NotConfigured"), service);
        Assert.Equal(0, mailgun.Calls);
    }

    [UnitTheory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task GivenMailgunAnswersBelow500_WhenChecked_ThenItIsHealthy(HttpStatusCode status)
    {
        // When
        var service = await Check(Health(HealthProbeHandler.Answering(status)));

        // Then
        Assert.Equal(new InternalHealthEndpoint.Service("Mailgun", "Healthy"), service);
    }

    [UnitTheory]
    [InlineData(HttpStatusCode.InternalServerError, "HTTP 500")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "HTTP 503")]
    public async Task GivenMailgunAnswers5xx_WhenChecked_ThenItIsUnhealthyWithTheStatus(HttpStatusCode status, string reason)
    {
        // When
        var service = await Check(Health(HealthProbeHandler.Answering(status)));

        // Then
        Assert.Equal(new InternalHealthEndpoint.Service("Mailgun", "Unhealthy", reason), service);
    }

    [UnitFact]
    public async Task GivenTheCallFails_WhenChecked_ThenItIsUnhealthyWithTheReasonWordOnly()
    {
        // When
        var service = await Check(Health(HealthProbeHandler.Failing(Refusal())));

        // Then
        Assert.Equal(new InternalHealthEndpoint.Service("Mailgun", "Unhealthy", "connection refused"), service);
    }

    [UnitFact]
    public async Task GivenTheCallTimesOut_WhenChecked_ThenItIsUnhealthyWithTimeout()
    {
        // When
        var service = await Check(Health(HealthProbeHandler.Failing(new TaskCanceledException(Secret))));

        // Then
        Assert.Equal("timeout", service.Reason);
    }

    [UnitFact]
    public async Task GivenACheckInFlight_WhenOtherCallersAsk_ThenTheyAllWaitForTheSameCall()
    {
        // Given
        var mailgun = new HealthProbeHandler(() => new HttpResponseMessage(HttpStatusCode.OK)) { Gate = new TaskCompletionSource() };
        var health = Health(mailgun);
        var first = Check(health);

        // When
        var others = Enumerable.Range(0, 5).Select(_ => Check(health)).ToList();
        mailgun.Gate.SetResult();
        var services = await Task.WhenAll([first, .. others]);

        // Then
        Assert.All(services, service => Assert.Equal("Healthy", service.Status));
        Assert.Equal(1, mailgun.Calls);
    }

    [UnitFact]
    public async Task GivenACallerGivesUp_WhenACheckIsInFlight_ThenTheCheckGoesOnForTheOthers()
    {
        // Given
        var mailgun = new HealthProbeHandler(() => new HttpResponseMessage(HttpStatusCode.OK)) { Gate = new TaskCompletionSource() };
        var health = Health(mailgun);
        using var givingUp = new CancellationTokenSource();
        var impatient = Check(health, givingUp.Token);
        var patient = Check(health);

        // When
        await givingUp.CancelAsync();
        var gaveUp = await Record.ExceptionAsync(() => impatient);
        mailgun.Gate.SetResult();
        var service = await patient;

        // Then
        Assert.IsAssignableFrom<OperationCanceledException>(gaveUp);
        Assert.Equal("Healthy", service.Status);
        Assert.Equal(1, mailgun.Calls);
    }

    [UnitFact]
    public async Task GivenARecentResult_WhenAskedJustBeforeTheWindowEnds_ThenItIsServedWithoutANewCall()
    {
        // Given
        var mailgun = HealthProbeHandler.Answering(HttpStatusCode.OK);
        var health = Health(mailgun);
        await Check(health);

        // When
        _clock.Now += InternalHealthEndpoint.CacheFor - TimeSpan.FromTicks(1);
        await Check(health);

        // Then
        Assert.Equal(1, mailgun.Calls);
    }

    [UnitFact]
    public async Task GivenAnOlderResult_WhenAskedAsTheWindowEnds_ThenANewCallIsMade()
    {
        // Given
        var mailgun = HealthProbeHandler.Answering(HttpStatusCode.OK);
        var health = Health(mailgun);
        await Check(health);

        // When
        _clock.Now += InternalHealthEndpoint.CacheFor;
        await Check(health);

        // Then
        Assert.Equal(2, mailgun.Calls);
    }

    [UnitFact]
    public async Task GivenARecentFailure_WhenAskedWithinTheWindow_ThenTheFailureIsServedWithoutANewCall()
    {
        // Given
        var mailgun = HealthProbeHandler.Failing(Refusal());
        var health = Health(mailgun);
        await Check(health);
        mailgun.Answer = () => new HttpResponseMessage(HttpStatusCode.OK);

        // When
        var service = await Check(health);

        // Then
        Assert.Equal("Unhealthy", service.Status);
        Assert.Equal(1, mailgun.Calls);
    }

    [UnitFact]
    public async Task GivenTheSameFailureTwice_WhenChecked_ThenItIsLoggedOnceWithOnlyTheReasonWord()
    {
        // Given
        var health = Health(HealthProbeHandler.Failing(Refusal()));
        await Check(health);
        _clock.Now += InternalHealthEndpoint.CacheFor;

        // When
        await Check(health);

        // Then
        var warning = Assert.Single(Warnings());
        Assert.Contains("connection refused", warning);
        Assert.DoesNotContain(Secret, warning);
    }

    [UnitFact]
    public async Task GivenAFailureAfterARecovery_WhenChecked_ThenItIsLoggedAgain()
    {
        // Given
        var mailgun = HealthProbeHandler.Failing(Refusal());
        var health = Health(mailgun);
        await Check(health);
        mailgun.Answer = () => new HttpResponseMessage(HttpStatusCode.OK);
        _clock.Now += InternalHealthEndpoint.CacheFor;
        await Check(health);
        mailgun.Answer = () => throw Refusal();
        _clock.Now += InternalHealthEndpoint.CacheFor;

        // When
        await Check(health);

        // Then
        Assert.Equal(2, Warnings().Count());
    }

    [UnitFact]
    public async Task GivenMailgunHealthy_WhenChecked_ThenNothingIsLogged()
    {
        // When
        await Check(Health(HealthProbeHandler.Answering(HttpStatusCode.OK)));

        // Then
        Assert.Empty(_logger.Entries);
    }
}
