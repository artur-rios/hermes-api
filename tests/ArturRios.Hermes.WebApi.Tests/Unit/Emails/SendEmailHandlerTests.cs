using System.Text;
using ArturRios.Hermes.WebApi.Clients;
using ArturRios.Hermes.WebApi.Emails;
using ArturRios.Hermes.WebApi.Limits;
using ArturRios.Messaging.Email;
using ArturRios.Output;
using ArturRios.Util.Test.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Emails;

/// <summary>
/// What the handler decides on its own: how it reads a body, and how it tells the caller going away from a failed
/// delivery. The order of its checks and the status of each outcome are tested over HTTP in <c>SendEmailTests</c>.
/// </summary>
public class SendEmailHandlerTests
{
    private const string Key = "active-key";
    private const string Recipient = "owner@example.com";

    private static readonly Client Client = new(
        "personal-page", [HermesApi.HashOf(Key)], "Client <client@mg.example.com>", RecipientPolicy.Fixed([Recipient]), 5, 50);

    private readonly Mock<IEmailService> _email = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 11, 8, 12, 0, 20, TimeSpan.Zero));

    public SendEmailHandlerTests() =>
        _email.Setup(email => email.SendEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessOutput());

    private SendEmailHandler Handler() => new(
        new ClientAuthenticator(new ClientDirectory([Client])),
        new SendEmailRequestValidator(),
        new UsageCounters(_clock.Provider),
        new OwnerAlert(new ClientSettings().Build(), _email.Object, NullLogger<OwnerAlert>.Instance),
        _email.Object,
        NullLogger<SendEmailHandler>.Instance);

    private Task<SendEmailResult> Handle(string body, CancellationToken? token = null) =>
        Handler().HandleAsync($"Bearer {Key}", Encoding.UTF8.GetBytes(body), token ?? TestContext.Current.CancellationToken);

    [UnitTheory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("""{"to":5,"subject":"Subject","body":"Body"}""")]
    [InlineData("""{"to":"owner@example.com","subject":"Subject","body":"Body"} trailing""")]
    public async Task GivenABodyThatIsNotASendRequest_WhenHandled_ThenItIsAnInvalidBody(string body)
    {
        // When
        var result = await Handle(body);

        // Then
        Assert.Equal([SendEmailOutcomes.InvalidBody], result.Output.Errors);
        _email.VerifyNoOtherCalls();
    }

    [UnitFact]
    public async Task GivenFieldNamesInAnotherCase_WhenHandled_ThenTheyAreRead()
    {
        // When
        var result = await Handle("""{"TO":"owner@example.com","Subject":"Subject","BODY":"Body","ReplyTo":"visitor@example.com"}""");

        // Then
        Assert.Equal([SendEmailOutcomes.Accepted], result.Output.Messages);
        _email.Verify(email => email.SendEmailAsync(
            It.Is<EmailMessage>(message => message.To == Recipient && message.ReplyTo == "visitor@example.com"),
            It.IsAny<CancellationToken>()));
    }

    [UnitFact]
    public async Task GivenEveryFieldMissing_WhenHandled_ThenEveryFieldErrorIsReturned()
    {
        // When
        var result = await Handle("{}");

        // Then
        Assert.Equal(["to is required.", "subject is required.", "body is required."], result.Output.Errors);
        Assert.Null(result.RetryAfterSeconds);
    }

    [UnitFact]
    public async Task GivenAnAcceptedMessage_WhenHandled_ThenThereIsNoRetryAfter()
    {
        // When
        var result = await Handle("""{"to":"owner@example.com","subject":"Subject","body":"Body"}""");

        // Then
        Assert.True(result.Output.Success);
        Assert.Null(result.RetryAfterSeconds);
    }

    [UnitFact]
    public async Task GivenTheCallerGoesAway_WhenDeliveryIsCanceled_ThenTheCancellationIsNotTurnedIntoADeliveryFailure()
    {
        // Given
        using var caller = new CancellationTokenSource();
        _email.Setup(email => email.SendEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns<EmailMessage, CancellationToken>(async (_, token) =>
            {
                await caller.CancelAsync();
                token.ThrowIfCancellationRequested();

                return new ProcessOutput();
            });

        // When
        var handling = Handle("""{"to":"owner@example.com","subject":"Subject","body":"Body"}""", caller.Token);

        // Then
        await Assert.ThrowsAsync<OperationCanceledException>(() => handling);
    }

    [UnitFact]
    public async Task GivenTheCallerIsStillThere_WhenDeliveryIsCanceledByItsOwnTimeout_ThenItIsADeliveryFailure()
    {
        // Given
        _email.Setup(email => email.SendEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));

        // When
        var result = await Handle("""{"to":"owner@example.com","subject":"Subject","body":"Body"}""");

        // Then
        Assert.Equal([SendEmailOutcomes.DeliveryFailed], result.Output.Errors);
    }

    [UnitFact]
    public async Task GivenTheCallersToken_WhenDelivering_ThenItIsPassedToTheProvider()
    {
        // Given
        using var caller = new CancellationTokenSource();

        // When
        await Handle("""{"to":"owner@example.com","subject":"Subject","body":"Body"}""", caller.Token);

        // Then
        _email.Verify(email => email.SendEmailAsync(It.IsAny<EmailMessage>(), caller.Token));
    }
}
