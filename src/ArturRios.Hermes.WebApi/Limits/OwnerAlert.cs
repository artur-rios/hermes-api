using ArturRios.Hermes.WebApi.Clients;
using ArturRios.Messaging.Email;

namespace ArturRios.Hermes.WebApi.Limits;

/// <summary>
/// Tells the owner by email that a client has used up its daily limit. It names the client and the limit and holds
/// nothing from any message. It is not counted against any client, and sent from Mailgun's default sender rather than
/// the client's. A failure is logged and never changes the client's answer.
/// </summary>
public sealed class OwnerAlert(IConfiguration configuration, IEmailService emailService, ILogger<OwnerAlert> logger)
{
    public async Task SendAsync(Client client)
    {
        var address = configuration[ClientConfiguration.AlertAddressVariable];

        if (string.IsNullOrWhiteSpace(address))
        {
            logger.LogInformation("No alert address is configured: an alert for client {ClientId} would have been sent", client.Id);

            return;
        }

        var alert = new EmailMessage(
            address,
            $"Hermes: {client.Id} reached its daily limit",
            $"Client {client.Id} has used up its daily limit of {client.LimitPerDay}. "
            + "This means likely abuse or a limit set too low. It can send again after midnight UTC.");

        if (await DeliverAsync(alert))
        {
            logger.LogInformation("Handed over the alert that client {ClientId} reached its daily limit", client.Id);
        }
        else
        {
            logger.LogError("The alert for client {ClientId} could not be delivered", client.Id);
        }
    }

    /// <summary>The caller's request does not cancel this: the owner should hear of it even if the client hangs up.</summary>
    private async Task<bool> DeliverAsync(EmailMessage alert)
    {
        try
        {
            var output = await emailService.SendEmailAsync(alert, CancellationToken.None);

            return output.Success;
        }
        catch (Exception exception)
        {
            logger.LogWarning("Delivering an alert raised {FailureType}", exception.GetType().Name);

            return false;
        }
    }
}
