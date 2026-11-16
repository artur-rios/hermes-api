using ArturRios.Hermes.WebApi.Emails;
using ArturRios.Messaging.Email;
using ArturRios.Util.Test.Attributes;
using Microsoft.Extensions.Logging;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Emails;

public class LoggingEmailServiceTests
{
    private const string Recipient = "owner@example.com";
    private const string Subject = "A-very-private-subject";
    private const string Body = "A-very-private-body";

    private readonly RecordingLogger<LoggingEmailService> _logger = new();

    private LoggingEmailService Service() => new(_logger);

    [UnitFact]
    public async Task GivenAMessage_WhenSent_ThenItSucceedsWithoutSendingAnything()
    {
        // When
        var output = await Service().SendEmailAsync(new EmailMessage(Recipient, Subject, Body), TestContext.Current.CancellationToken);

        // Then
        Assert.True(output.Success);
        Assert.Empty(output.Errors);
    }

    [UnitFact]
    public async Task GivenTheThreeFieldOverload_WhenSent_ThenItSucceedsToo()
    {
        // When
        var output = await Service().SendEmailAsync(Recipient, Subject, Body);

        // Then
        Assert.True(output.Success);
    }

    [UnitFact]
    public async Task GivenAMessage_WhenSent_ThenItLogsTheRecipientByReferenceAndNothingElse()
    {
        // When
        await Service().SendEmailAsync(new EmailMessage(Recipient, Subject, Body), TestContext.Current.CancellationToken);

        // Then
        var (level, message) = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, level);
        Assert.Contains(Mailbox.Reference(Recipient), message);
        Assert.DoesNotContain(Recipient, message);
        Assert.DoesNotContain(Subject, message);
        Assert.DoesNotContain(Body, message);
    }

    [UnitFact]
    public async Task GivenTheThreeFieldOverload_WhenSent_ThenItLogsTheSameReference()
    {
        // When
        await Service().SendEmailAsync(Recipient, Subject, Body);

        // Then
        var (_, message) = Assert.Single(_logger.Entries);
        Assert.Contains(Mailbox.Reference(Recipient), message);
        Assert.DoesNotContain(Recipient, message);
    }
}
