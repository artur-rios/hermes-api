using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ArturRios.Hermes.WebApi.Emails;
using ArturRios.Messaging.Email;
using ArturRios.Output;
using ArturRios.Util.Test.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ArturRios.Hermes.WebApi.Tests.Functional;

[Collection(ProcessStateCollection.Name)]
public class SendEmailTests
{
    private const string Key = "first-active-key";
    private const string SecondKey = "second-active-key";
    private const string Recipient = "owner@example.com";

    private static readonly object Valid = new { to = Recipient, subject = "Subject", body = "Body" };

    private readonly TestClock _clock = new(new DateTimeOffset(2026, 11, 8, 12, 0, 20, TimeSpan.Zero));

    private static ClientSettings Settings(string perMinute = "5", string perDay = "50") =>
        new ClientSettings().Client(0, "personal-page", HermesApi.HashOf(Key))
            .Set(0, "KEYHASHES:1", HermesApi.HashOf(SecondKey))
            .Set(0, "LIMITPERMINUTE", perMinute)
            .Set(0, "LIMITPERDAY", perDay);

    private HermesApi Api(ClientSettings? settings = null) => new(settings ?? Settings(), _clock);

    private static async Task<JsonElement> BodyOf(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;

    private static void FailDeliveryWith(HermesApi api, Exception exception) =>
        api.Email.Setup(email => email.SendEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

    [FunctionalFact]
    public async Task GivenAValidRequest_WhenSending_ThenAnswers202()
    {
        // Given
        using var api = Api();

        // When
        var response = await api.SendAsync(Valid, Key);

        // Then
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [FunctionalFact]
    public async Task GivenAValidRequest_WhenSending_ThenTheResponseIsADataOutput()
    {
        // Given
        using var api = Api();

        // When
        var body = await BodyOf(await api.SendAsync(Valid, Key));

        // Then
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.Empty(body.GetProperty("errors").EnumerateArray());
    }

    [FunctionalFact]
    public async Task GivenAValidRequest_WhenSending_ThenMailgunReceivesItFromTheClientsSender()
    {
        // Given
        using var api = Api();

        // When
        await api.SendAsync(new { to = Recipient, subject = "Subject", body = "Body", replyTo = "visitor@example.com" }, Key);

        // Then
        var message = Assert.Single(api.Sent);
        Assert.Equal(Recipient, message.To);
        Assert.Equal("Subject", message.Subject);
        Assert.Equal("Body", message.Body);
        Assert.Equal("Client <client@mg.example.com>", message.From);
        Assert.Equal("visitor@example.com", message.ReplyTo);
    }

    [FunctionalFact]
    public async Task GivenABodyOverTheLimit_WhenSending_ThenAnswers413AndSendsNothing()
    {
        // Given
        using var api = Api();
        var oversized = new ByteArrayContent(new byte[SendEmailEndpoint.MaximumBodyBytes + 1]);

        // When
        var response = await api.PostAsync(oversized, Key);

        // Then
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenABodyOverTheLimitAndNoKey_WhenSending_ThenTheSizeIsCheckedFirst()
    {
        // Given
        using var api = Api();
        var oversized = new ByteArrayContent(new byte[SendEmailEndpoint.MaximumBodyBytes + 1]);

        // When
        var response = await api.PostAsync(oversized, key: null);

        // Then
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenAStreamedBodyOverTheLimit_WhenSending_ThenAnswers413()
    {
        // Given
        using var api = Api();
        var streamed = new StreamContent(new MemoryStream(new byte[SendEmailEndpoint.MaximumBodyBytes + 1]));
        streamed.Headers.ContentLength = null;

        // When
        var response = await api.PostAsync(streamed, Key);

        // Then
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenABodyExactlyAtTheLimit_WhenSending_ThenItIsNotRefusedForItsSize()
    {
        // Given
        using var api = Api();
        var atTheLimit = new ByteArrayContent(new byte[SendEmailEndpoint.MaximumBodyBytes]);

        // When
        var response = await api.PostAsync(atTheLimit, Key);

        // Then
        Assert.NotEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [FunctionalFact]
    public async Task GivenNoKey_WhenSending_ThenAnswers401AndSendsNothing()
    {
        // Given
        using var api = Api();

        // When
        var response = await api.SendAsync(Valid, key: null);

        // Then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenAnUnknownKey_WhenSending_ThenAnswers401AndSendsNothing()
    {
        // Given
        using var api = Api();

        // When
        var response = await api.SendAsync(Valid, "unknown-key");

        // Then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenAnUnknownKeyAndInvalidFields_WhenSending_ThenTheKeyIsCheckedFirst()
    {
        // Given
        using var api = Api();

        // When
        var response = await api.SendAsync(new { to = "not-an-address" }, "unknown-key");

        // Then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenTheSecondActiveKey_WhenSending_ThenAnswers202()
    {
        // Given
        using var api = Api();

        // When
        var response = await api.SendAsync(Valid, SecondKey);

        // Then
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Single(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenAnInvalidField_WhenSending_ThenAnswers400AndSendsNothing()
    {
        // Given
        using var api = Api();

        // When
        var response = await api.SendAsync(new { to = "not-an-address", subject = "Subject", body = "Body" }, Key);

        // Then
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenMalformedJson_WhenSending_ThenAnswers400AndSendsNothing()
    {
        // Given
        using var api = Api();

        // When
        var response = await api.PostAsync(new StringContent("{ not json", Encoding.UTF8, "application/json"), Key);

        // Then
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenAJsonNull_WhenSending_ThenAnswers400()
    {
        // Given
        using var api = Api();

        // When
        var response = await api.PostAsync(new StringContent("null", Encoding.UTF8, "application/json"), Key);

        // Then
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenASenderInTheRequest_WhenSending_ThenAnswers400AndSendsNothing()
    {
        // Given
        using var api = Api();

        // When
        var response = await api.SendAsync(new { to = Recipient, subject = "Subject", body = "Body", sender = "x@example.com" }, Key);

        // Then
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenAClientIdentifierInTheRequest_WhenSending_ThenAnswers400()
    {
        // Given
        using var api = Api();

        // When
        var response = await api.SendAsync(new { to = Recipient, subject = "Subject", body = "Body", clientId = "heimdall" }, Key);

        // Then
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenAnInvalidField_WhenSending_ThenNothingIsCounted()
    {
        // Given
        using var api = Api(Settings(perMinute: "1"));
        await api.SendAsync(new { to = "not-an-address", subject = "Subject", body = "Body" }, Key);

        // When
        var response = await api.SendAsync(Valid, Key);

        // Then
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [FunctionalFact]
    public async Task GivenARecipientThePolicyDoesNotPermit_WhenSending_ThenAnswers403AndSendsNothing()
    {
        // Given
        using var api = Api();

        // When
        var response = await api.SendAsync(new { to = "stranger@example.com", subject = "Subject", body = "Body" }, Key);

        // Then
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenARecipientThePolicyDoesNotPermit_WhenSending_ThenNothingIsCounted()
    {
        // Given
        using var api = Api(Settings(perMinute: "1"));
        await api.SendAsync(new { to = "stranger@example.com", subject = "Subject", body = "Body" }, Key);

        // When
        var response = await api.SendAsync(Valid, Key);

        // Then
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [FunctionalFact]
    public async Task GivenTheMinuteUsedUp_WhenSending_ThenAnswers429UntilTheMinuteEndsAndSendsNothing()
    {
        // Given
        using var api = Api(Settings(perMinute: "1"));
        await api.SendAsync(Valid, Key);

        // When
        var response = await api.SendAsync(Valid, Key);

        // Then
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(40), response.Headers.RetryAfter?.Delta);
        Assert.Single(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenTheDayUsedUp_WhenSending_ThenAnswers429UntilMidnightUtcAndSendsNothing()
    {
        // Given
        _clock.Now = new DateTimeOffset(2026, 11, 8, 23, 0, 0, TimeSpan.Zero);
        using var api = Api(Settings(perDay: "1"));
        await api.SendAsync(Valid, Key);

        // When
        var response = await api.SendAsync(Valid, Key);

        // Then
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(TimeSpan.FromHours(1), response.Headers.RetryAfter?.Delta);
        Assert.Single(api.Sent);
    }

    [FunctionalFact]
    public async Task GivenMailgunRefusesTheMessage_WhenSending_ThenAnswers502WithoutRetrying()
    {
        // Given
        using var api = Api();
        api.Email.Setup(email => email.SendEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessOutput().WithError("Mailgun said no to owner@example.com"));

        // When
        var response = await api.SendAsync(Valid, Key);

        // Then
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        api.Email.Verify(email => email.SendEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [FunctionalFact]
    public async Task GivenMailgunRefusesTheMessage_WhenSending_ThenItsErrorIsNotPassedOn()
    {
        // Given
        using var api = Api();
        api.Email.Setup(email => email.SendEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessOutput().WithError("provider detail"));

        // When
        var response = await api.SendAsync(Valid, Key);

        // Then
        Assert.DoesNotContain("provider detail", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [FunctionalFact]
    public async Task GivenMailgunRefusesTheMessage_WhenSendingAgain_ThenTheFailedMessageStayedCounted()
    {
        // Given
        using var api = Api(Settings(perMinute: "1"));
        api.Email.Setup(email => email.SendEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessOutput().WithError("refused"));
        await api.SendAsync(Valid, Key);

        // When
        var response = await api.SendAsync(Valid, Key);

        // Then
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [FunctionalFact]
    public async Task GivenMailgunCannotBeReached_WhenSending_ThenAnswers502WithoutRetrying()
    {
        // Given
        using var api = Api();
        FailDeliveryWith(api, new HttpRequestException("unreachable"));

        // When
        var response = await api.SendAsync(Valid, Key);

        // Then
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        api.Email.Verify(email => email.SendEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [FunctionalFact]
    public async Task GivenMailgunDoesNotAnswerInTime_WhenSending_ThenAnswers502AndStaysCounted()
    {
        // Given
        using var api = Api(Settings(perMinute: "1"));
        FailDeliveryWith(api, new TaskCanceledException("timed out"));

        // When
        var response = await api.SendAsync(Valid, Key);
        var next = await api.SendAsync(Valid, Key);

        // Then
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, next.StatusCode);
    }

    [FunctionalFact]
    public async Task GivenDeliveryFailsInAnUnexpectedWay_WhenSending_ThenAnswers502()
    {
        // Given
        using var api = Api();
        FailDeliveryWith(api, new InvalidOperationException("unexpected"));

        // When
        var response = await api.SendAsync(Valid, Key);

        // Then
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Empty(api.Sent);
    }

    [FunctionalFact]
    public void GivenTheMailgunClient_WhenResolved_ThenItGivesUpAfterTenSeconds()
    {
        // Given
        using var api = Api();

        // When
        var client = api.Services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(MailgunEmailService));

        // Then
        Assert.Equal(TimeSpan.FromSeconds(10), client.Timeout);
    }

    [FunctionalFact]
    public async Task GivenNoMailgunOutsideProduction_WhenSending_ThenAnswers202()
    {
        // Given
        using var api = new HermesApi(Settings(), _clock, replaceMailgun: false);

        // When
        var response = await api.SendAsync(Valid, Key);

        // Then
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [FunctionalFact]
    public void GivenMailgunIsConfigured_WhenResolvingTheService_ThenItIsMailgun()
    {
        // Given
        using var api = new HermesApi(Settings().WithMailgun(), _clock, replaceMailgun: false);

        // When
        using var scope = api.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEmailService>();

        // Then
        Assert.IsType<MailgunEmailService>(service);
    }
}
