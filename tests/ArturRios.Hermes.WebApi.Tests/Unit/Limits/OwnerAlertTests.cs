using ArturRios.Hermes.WebApi.Clients;
using ArturRios.Hermes.WebApi.Limits;
using ArturRios.Messaging.Email;
using ArturRios.Output;
using ArturRios.Util.Test.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Limits;

public class OwnerAlertTests
{
    private const string AlertAddress = "alerts@example.com";

    private static readonly Client Client = new("personal-page", ["hash"], "page@example.com", RecipientPolicy.Any, 5, 50);

    private readonly Mock<IEmailService> _email = new();

    private OwnerAlert Alert(string? address = AlertAddress) =>
        new(new ClientSettings().Global("HERMES_ALERT_TO", address).Build(), _email.Object, NullLogger<OwnerAlert>.Instance);

    private void Answer(ProcessOutput output) =>
        _email.Setup(email => email.SendEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(output);

    private EmailMessage Sent() =>
        (EmailMessage)Assert.Single(_email.Invocations).Arguments[0]!;

    [UnitFact]
    public async Task GivenAnAlertAddress_WhenAlerting_ThenTheOwnerIsEmailedOnce()
    {
        // Given
        Answer(new ProcessOutput());

        // When
        await Alert().SendAsync(Client);

        // Then
        Assert.Equal(AlertAddress, Sent().To);
    }

    [UnitFact]
    public async Task GivenAnAlert_WhenItIsWritten_ThenItNamesTheClientAndItsLimit()
    {
        // Given
        Answer(new ProcessOutput());

        // When
        await Alert().SendAsync(Client);

        // Then
        Assert.Contains("personal-page", Sent().Subject);
        Assert.Contains("personal-page", Sent().Body);
        Assert.Contains("50", Sent().Body);
    }

    [UnitFact]
    public async Task GivenAnAlert_WhenItIsWritten_ThenItIsNotSentAsTheClient()
    {
        // Given
        Answer(new ProcessOutput());

        // When
        await Alert().SendAsync(Client);

        // Then
        Assert.Null(Sent().From);
    }

    [UnitFact]
    public async Task GivenAnAlert_WhenItIsSent_ThenNothingCanCancelIt()
    {
        // Given
        Answer(new ProcessOutput());

        // When
        await Alert().SendAsync(Client);

        // Then
        var token = (CancellationToken)Assert.Single(_email.Invocations).Arguments[1]!;
        Assert.False(token.CanBeCanceled);
    }

    [UnitFact]
    public async Task GivenAProviderRefusal_WhenAlerting_ThenItDoesNotThrow()
    {
        // Given
        Answer(new ProcessOutput().WithError("refused"));

        // When
        var failure = await Record.ExceptionAsync(() => Alert().SendAsync(Client));

        // Then
        Assert.Null(failure);
    }

    [UnitFact]
    public async Task GivenAProviderOutage_WhenAlerting_ThenItDoesNotThrow()
    {
        // Given
        _email.Setup(email => email.SendEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("unreachable"));

        // When
        var failure = await Record.ExceptionAsync(() => Alert().SendAsync(Client));

        // Then
        Assert.Null(failure);
    }

    [UnitTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GivenNoAlertAddress_WhenAlerting_ThenNothingIsSent(string? address)
    {
        // When
        await Alert(address).SendAsync(Client);

        // Then
        Assert.Empty(_email.Invocations);
    }
}
