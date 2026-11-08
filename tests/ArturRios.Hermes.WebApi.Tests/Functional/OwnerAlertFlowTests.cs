using System.Net;
using ArturRios.Messaging.Email;
using ArturRios.Output;
using ArturRios.Util.Test.Attributes;
using Moq;

namespace ArturRios.Hermes.WebApi.Tests.Functional;

[Collection(ProcessStateCollection.Name)]
public class OwnerAlertFlowTests
{
    private const string Key = "active-key";
    private const string Recipient = "owner@example.com";
    private const string AlertAddress = "alerts@example.com";
    private const string Subject = "A-very-private-subject";
    private const string Body = "A-very-private-body";

    private static readonly object Message = new { to = Recipient, subject = Subject, body = Body };

    private readonly TestClock _clock = new(new DateTimeOffset(2026, 11, 8, 12, 0, 20, TimeSpan.Zero));

    private static ClientSettings Settings(string? alertAddress = AlertAddress, string perDay = "2") =>
        new ClientSettings().Client(0, "personal-page", HermesApi.HashOf(Key))
            .Set(0, "LIMITPERDAY", perDay)
            .Global("HERMES_ALERT_TO", alertAddress);

    private HermesApi Api(ClientSettings settings) => new(settings, _clock);

    private static IEnumerable<EmailMessage> AlertsIn(HermesApi api) => api.Sent.Where(message => message.To == AlertAddress);

    [FunctionalFact]
    public async Task GivenAnEarlierMessage_WhenSending_ThenNoAlertIsSent()
    {
        // Given
        using var api = Api(Settings());

        // When
        await api.SendAsync(Message, Key);

        // Then
        Assert.Empty(AlertsIn(api));
        Assert.Single(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenTheLastMessageOfTheDay_WhenSending_ThenTheOwnerIsAlertedAndTheMessageStillGoesOut()
    {
        // Given
        using var api = Api(Settings(perDay: "1"));

        // When
        var response = await api.SendAsync(Message, Key);

        // Then
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal([AlertAddress, Recipient], api.Sent.Select(message => message.To));
    }

    [FunctionalFact]
    public async Task GivenAnAlert_WhenItIsSent_ThenItHoldsNoContentFromTheMessage()
    {
        // Given
        using var api = Api(Settings(perDay: "1"));

        // When
        await api.SendAsync(Message, Key);

        // Then
        var alert = Assert.Single(AlertsIn(api));
        Assert.DoesNotContain(Subject, alert.Subject + alert.Body);
        Assert.DoesNotContain(Body, alert.Subject + alert.Body);
        Assert.DoesNotContain(Recipient, alert.Subject + alert.Body);
    }

    [FunctionalFact]
    public async Task GivenAnAlreadyAlertedClient_WhenItKeepsSending_ThenNoFurtherAlertIsSent()
    {
        // Given
        using var api = Api(Settings(perDay: "1"));
        await api.SendAsync(Message, Key);

        // When
        var response = await api.SendAsync(Message, Key);

        // Then
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Single(AlertsIn(api));
    }

    [FunctionalFact]
    public async Task GivenTheAlertCannotBeDelivered_WhenSending_ThenTheClientsAnswerDoesNotChange()
    {
        // Given
        using var api = Api(Settings(perDay: "1"));
        api.Email.Setup(email => email.SendEmailAsync(It.Is<EmailMessage>(message => message.To == AlertAddress), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("unreachable"));

        // When
        var response = await api.SendAsync(Message, Key);

        // Then
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var own = Assert.Single(api.Sent);
        Assert.Equal(Recipient, own.To);
    }

    [FunctionalFact]
    public async Task GivenTheAlertIsRefused_WhenSending_ThenTheFailureIsLoggedWithoutTheAddress()
    {
        // Given
        using var console = new ConsoleCapture();
        using var api = Api(Settings(perDay: "1"));
        api.Email.Setup(email => email.SendEmailAsync(It.Is<EmailMessage>(message => message.To == AlertAddress), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessOutput().WithError($"refused {AlertAddress}"));

        // When
        await api.SendAsync(Message, Key);

        // Then
        await console.WaitForAsync("could not be delivered");
        await console.WaitForAsync("personal-page");
        Assert.DoesNotContain(AlertAddress, console.Text);
    }

    [FunctionalFact]
    public async Task GivenTheAlertRaisesAnExceptionHoldingTheAddress_WhenSending_ThenTheAddressIsNotLogged()
    {
        // Given
        using var console = new ConsoleCapture();
        using var api = Api(Settings(perDay: "1"));
        api.Email.Setup(email => email.SendEmailAsync(It.Is<EmailMessage>(message => message.To == AlertAddress), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException($"could not reach {AlertAddress}"));

        // When
        await api.SendAsync(Message, Key);

        // Then
        await console.WaitForAsync("could not be delivered");
        Assert.DoesNotContain(AlertAddress, console.Text);
    }

    [FunctionalFact]
    public async Task GivenTheRealMailgunService_WhenAlerting_ThenItGoesOutFromTheDefaultSenderAndAnEchoIsNotLogged()
    {
        // Given
        Environment.SetEnvironmentVariable("MAILGUN_API_KEY", "key");
        Environment.SetEnvironmentVariable("MAILGUN_DOMAIN", "mg.example.com");
        using var console = new ConsoleCapture();
        var mailgun = new StubMailgun(HttpStatusCode.BadRequest, $"'to' {AlertAddress} rejected");
        using var api = new HermesApi(Settings(perDay: "1").WithMailgun(), _clock, replaceMailgun: false, mailgunHandler: mailgun);

        try
        {
            // When
            await api.SendAsync(Message, Key);

            // Then
            Assert.Equal(2, mailgun.Calls);
            Assert.Contains("from=postmaster%40mg.example.com", mailgun.Requests[0]);
            Assert.Contains("to=alerts%40example.com", mailgun.Requests[0]);
            await console.WaitForAsync("could not be delivered");
            Assert.DoesNotContain(AlertAddress, console.Text);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MAILGUN_API_KEY", null);
            Environment.SetEnvironmentVariable("MAILGUN_DOMAIN", null);
        }
    }

    [FunctionalFact]
    public async Task GivenNoAlertAddressOutsideProduction_WhenTheDayIsUsedUp_ThenItLogsThatAnAlertWouldHaveBeenSent()
    {
        // Given
        using var console = new ConsoleCapture();
        using var api = Api(Settings(alertAddress: null, perDay: "1"));

        // When
        var response = await api.SendAsync(Message, Key);

        // Then
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var own = Assert.Single(api.Sent);
        Assert.Equal(Recipient, own.To);
        await console.WaitForAsync("would have been sent");
        await console.WaitForAsync("personal-page");
    }

    [FunctionalFact]
    public async Task GivenAnAlert_WhenItIsLogged_ThenNoContentOrAddressAppears()
    {
        // Given
        using var console = new ConsoleCapture();
        using var api = Api(Settings(perDay: "1"));

        // When
        await api.SendAsync(Message, Key);

        // Then
        await console.WaitForAsync(HermesApi.Route);
        Assert.DoesNotContain(AlertAddress, console.Text);
        Assert.DoesNotContain(Subject, console.Text);
        Assert.DoesNotContain(Body, console.Text);
    }
}
