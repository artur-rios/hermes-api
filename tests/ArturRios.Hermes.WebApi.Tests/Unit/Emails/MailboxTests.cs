using ArturRios.Hermes.WebApi.Emails;
using ArturRios.Util.Test.Attributes;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Emails;

public class MailboxTests
{
    private const string Address = "a@mg.example.com";

    [UnitTheory]
    [InlineData(Address)]
    [InlineData("Acme <a@mg.example.com>")]
    [InlineData("\"Acme, Inc\" <a@mg.example.com>")]
    public void GivenOneMailbox_WhenReadingTheSender_ThenItsAddressIsReturned(string sender)
    {
        // When
        var address = Mailbox.AddressOfSender(sender);

        // Then
        Assert.Equal(Address, address);
    }

    [UnitTheory]
    [InlineData("not-an-address")]
    [InlineData("")]
    [InlineData("<a@mg.example.com>")]
    [InlineData("x@other.example.com, y <a@mg.example.com>")]
    [InlineData("\"x\" <e@other.example.com>, \"y\" <a@mg.example.com>")]
    [InlineData("a@mg.example.com\r\nBcc: c@example.com")]
    public void GivenAnythingButOneMailbox_WhenReadingTheSender_ThenThereIsNoAddress(string sender)
    {
        // When
        var address = Mailbox.AddressOfSender(sender);

        // Then
        Assert.Null(address);
    }

    [UnitFact]
    public void GivenOneAddress_WhenChecked_ThenItIsAnAddress()
    {
        // When
        var isAddress = Mailbox.IsAddress(Address);

        // Then
        Assert.True(isAddress);
    }

    [UnitTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-address")]
    [InlineData("Acme <a@mg.example.com>")]
    [InlineData("a@mg.example.com, b@mg.example.com")]
    [InlineData(" a@mg.example.com")]
    public void GivenAnythingButOneBareAddress_WhenChecked_ThenItIsNotAnAddress(string? value)
    {
        // When
        var isAddress = Mailbox.IsAddress(value);

        // Then
        Assert.False(isAddress);
    }

    [UnitFact]
    public void GivenAnAddress_WhenReadingItsDomain_ThenItIsWhatFollowsTheAt()
    {
        // When
        var domain = Mailbox.DomainOf(Address);

        // Then
        Assert.Equal("mg.example.com", domain);
    }
}
