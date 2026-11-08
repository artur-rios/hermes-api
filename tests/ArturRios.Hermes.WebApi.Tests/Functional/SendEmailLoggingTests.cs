using System.Net;
using ArturRios.Hermes.WebApi.Emails;
using ArturRios.Messaging.Email;
using ArturRios.Util.Test.Attributes;
using Moq;

namespace ArturRios.Hermes.WebApi.Tests.Functional;

/// <summary>FR-EM-19, FR-EM-20 and FR-EM-21: what Hermes writes about a message, on every path.</summary>
[Collection(ProcessStateCollection.Name)]
public class SendEmailLoggingTests
{
    private const string Key = "active-key";
    private const string Recipient = "owner@example.com";
    private const string ReplyTo = "visitor@example.com";
    private const string Subject = "A-very-private-subject";
    private const string Body = "A-very-private-body";

    private static readonly object Message = new { to = Recipient, subject = Subject, body = Body, replyTo = ReplyTo };

    private static ClientSettings Settings() => new ClientSettings().Client(0, "personal-page", HermesApi.HashOf(Key));

    private static void AssertNoContentIn(string logs)
    {
        Assert.DoesNotContain(Subject, logs);
        Assert.DoesNotContain(Body, logs);
        Assert.DoesNotContain(Recipient, logs);
        Assert.DoesNotContain(ReplyTo, logs);
        Assert.DoesNotContain(Key, logs);
    }

    [FunctionalFact]
    public async Task GivenAnAcceptedMessage_WhenItIsLogged_ThenNoContentAppears()
    {
        // Given
        using var console = new ConsoleCapture();
        using var api = new HermesApi(Settings());

        // When
        await api.SendAsync(Message, Key);

        // Then
        await console.WaitForAsync(HermesApi.Route);
        AssertNoContentIn(console.Text);
    }

    [FunctionalFact]
    public async Task GivenAnAcceptedMessage_WhenItIsLogged_ThenTheRecipientAppearsByReference()
    {
        // Given
        using var console = new ConsoleCapture();
        using var api = new HermesApi(Settings());

        // When
        await api.SendAsync(Message, Key);

        // Then
        await console.WaitForAsync(Mailbox.Reference(Recipient));
    }

    [FunctionalFact]
    public async Task GivenADeliveryFailure_WhenItIsLogged_ThenNoContentAppears()
    {
        // Given
        using var console = new ConsoleCapture();
        using var api = new HermesApi(Settings());
        api.Email.Setup(email => email.SendEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException($"failed for {Recipient}: {Subject}"));

        // When
        await api.SendAsync(Message, Key);

        // Then
        await console.WaitForAsync(HermesApi.Route);
        AssertNoContentIn(console.Text);
    }

    [FunctionalFact]
    public async Task GivenMailgunEchoesTheMessageInARefusal_WhenItIsLogged_ThenNoContentAppears()
    {
        // Given
        Environment.SetEnvironmentVariable("MAILGUN_API_KEY", "key");
        Environment.SetEnvironmentVariable("MAILGUN_DOMAIN", "mg.example.com");
        using var console = new ConsoleCapture();
        var refusal = new StubMailgun(HttpStatusCode.BadRequest, $"'to' {Recipient} rejected, subject {Subject}");
        using var api = new HermesApi(Settings().WithMailgun(), replaceMailgun: false, mailgunHandler: refusal);

        try
        {
            // When
            var response = await api.SendAsync(Message, Key);

            // Then
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            Assert.Equal(1, refusal.Calls);
            await console.WaitForAsync("Mailgun rejected");
            await console.WaitForAsync(HermesApi.Route);
            AssertNoContentIn(console.Text);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MAILGUN_API_KEY", null);
            Environment.SetEnvironmentVariable("MAILGUN_DOMAIN", null);
        }
    }

    [FunctionalFact]
    public async Task GivenAClientOverItsLimit_WhenItIsLogged_ThenNoContentAppears()
    {
        // Given
        using var console = new ConsoleCapture();
        using var api = new HermesApi(Settings().Set(0, "LIMITPERMINUTE", "1"));
        await api.SendAsync(Message, Key);

        // When
        await api.SendAsync(Message, Key);

        // Then
        await console.WaitForAsync("reached a sending limit");
        AssertNoContentIn(console.Text);
    }

    [FunctionalFact]
    public async Task GivenARefusedRecipient_WhenItIsLogged_ThenNoContentAppears()
    {
        // Given
        using var console = new ConsoleCapture();
        using var api = new HermesApi(Settings());

        // When
        await api.SendAsync(new { to = "stranger@example.com", subject = Subject, body = Body }, Key);

        // Then
        await console.WaitForAsync(HermesApi.Route);
        Assert.DoesNotContain("stranger@example.com", console.Text);
        AssertNoContentIn(console.Text);
    }

    [FunctionalFact]
    public async Task GivenNoMailgunOutsideProduction_WhenAMessageIsLogged_ThenItSaysSoWithoutContent()
    {
        // Given
        using var console = new ConsoleCapture();
        using var api = new HermesApi(Settings(), replaceMailgun: false);

        // When
        await api.SendAsync(Message, Key);

        // Then
        await console.WaitForAsync("would have been sent");
        await console.WaitForAsync(Mailbox.Reference(Recipient));
        AssertNoContentIn(console.Text);
    }
}
