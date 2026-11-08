using System.Text.Json;
using ArturRios.Hermes.WebApi.Clients;
using ArturRios.Hermes.WebApi.Limits;
using ArturRios.Messaging.Email;
using ArturRios.Output;

namespace ArturRios.Hermes.WebApi.Emails;

/// <summary>The outcome of one send, and how long to wait when a limit was reached.</summary>
public sealed record SendEmailResult(DataOutput<string?> Output, int? RetryAfterSeconds = null);

/// <summary>
/// Handles one send, with each check cheaper than or a precondition for the next: the key, the fields, the recipient,
/// the limits, then delivery. Only a request that reaches delivery uses up quota. Logs carry the client and a
/// pseudonymous reference to the recipient, and never a subject, a body or an address.
/// </summary>
public sealed class SendEmailHandler(
    ClientAuthenticator authenticator,
    SendEmailRequestValidator validator,
    UsageCounters counters,
    IEmailService emailService,
    ILogger<SendEmailHandler> logger)
{
    public async Task<SendEmailResult> HandleAsync(string? authorizationHeader, byte[] body, CancellationToken cancellationToken)
    {
        var client = authenticator.Authenticate(authorizationHeader);

        if (client is null)
        {
            return Failure(SendEmailOutcomes.Unauthorized);
        }

        var request = Parse(body);

        if (request is null)
        {
            return Failure(SendEmailOutcomes.InvalidBody);
        }

        var validation = validator.Validate(request);

        if (!validation.IsValid)
        {
            return new SendEmailResult(DataOutput<string?>.New.WithErrors(validation.Errors.Select(error => error.ErrorMessage)));
        }

        var to = request.To!;
        var recipientReference = Mailbox.Reference(to);

        if (!client.Policy.Permits(to))
        {
            logger.LogWarning("Client {ClientId} may not send to {RecipientRef}", client.Id, recipientReference);

            return Failure(SendEmailOutcomes.RecipientNotPermitted);
        }

        var decision = counters.TryCount(client);

        if (!decision.Allowed)
        {
            logger.LogWarning("Client {ClientId} has reached a sending limit", client.Id);

            return new SendEmailResult(
                DataOutput<string?>.New.WithError(SendEmailOutcomes.LimitReached),
                decision.RetryAfterSeconds);
        }

        var message = new EmailMessage(to, request.Subject!, request.Body!) { From = client.Sender, ReplyTo = request.ReplyTo };

        if (!await DeliverAsync(message, cancellationToken))
        {
            logger.LogError("Delivery failed for client {ClientId} to {RecipientRef}", client.Id, recipientReference);

            return Failure(SendEmailOutcomes.DeliveryFailed);
        }

        logger.LogInformation("Accepted a message from client {ClientId} to {RecipientRef}", client.Id, recipientReference);

        return new SendEmailResult(DataOutput<string?>.New.WithMessage(SendEmailOutcomes.Accepted));
    }

    private static SendEmailResult Failure(string outcome) => new(DataOutput<string?>.New.WithError(outcome));

    private static SendEmailRequest? Parse(byte[] body)
    {
        try
        {
            return JsonSerializer.Deserialize<SendEmailRequest>(body, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// One attempt. Anything that goes wrong is a delivery failure, except the caller going away. Only the exception's
    /// type is logged, because its message may hold what was being sent.
    /// </summary>
    private async Task<bool> DeliverAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var output = await emailService.SendEmailAsync(message, cancellationToken);

            return output.Success;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Delivery raised {FailureType}", exception.GetType().Name);

            return false;
        }
    }
}
