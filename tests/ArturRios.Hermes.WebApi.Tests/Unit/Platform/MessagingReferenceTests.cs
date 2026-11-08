using ArturRios.Messaging.Email;
using ArturRios.Util.Test.Attributes;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Platform;

public class MessagingReferenceTests
{
    [UnitFact]
    public void GivenTheMessagingPackage_WhenAMessageIsBuilt_ThenItCarriesASenderAndAReplyTo()
    {
        // Given
        const string sender = "Client <client@example.com>";
        const string replyTo = "visitor@example.com";

        // When
        var message = new EmailMessage("to@example.com", "Subject", "Body") { From = sender, ReplyTo = replyTo };

        // Then
        Assert.Equal(sender, message.From);
        Assert.Equal(replyTo, message.ReplyTo);
    }
}
