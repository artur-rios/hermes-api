using ArturRios.Hermes.WebApi.Clients;
using ArturRios.Util.Test.Attributes;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Clients;

public class ClientConfigurationTests
{
    private static ClientDirectory Load(ClientSettings settings, bool isProduction = false) =>
        ClientConfiguration.Load(settings.Build(), isProduction);

    private static string Refusal(ClientSettings settings, bool isProduction = false) =>
        Assert.Throws<InvalidConfigurationException>(() => Load(settings, isProduction)).Message;

    [UnitFact]
    public void GivenAValidClient_WhenLoading_ThenTheDirectoryHoldsIt()
    {
        // Given
        var hash = ClientSettings.KeyHash();
        var settings = new ClientSettings().Client(0, "personal-page", hash);

        // When
        var client = Assert.Single(Load(settings).Clients);

        // Then
        Assert.Equal("personal-page", client.Id);
        Assert.Equal([hash], client.KeyHashes);
        Assert.Equal("Client <client@mg.example.com>", client.Sender);
        Assert.Equal(5, client.LimitPerMinute);
        Assert.Equal(50, client.LimitPerDay);
        Assert.True(client.Policy.Permits("owner@example.com"));
    }

    [UnitFact]
    public void GivenNoClients_WhenLoading_ThenTheDirectoryIsEmpty()
    {
        // When
        var directory = Load(new ClientSettings());

        // Then
        Assert.Empty(directory.Clients);
    }

    [UnitTheory]
    [InlineData("SENDER")]
    [InlineData("RECIPIENTPOLICY")]
    [InlineData("LIMITPERMINUTE")]
    [InlineData("LIMITPERDAY")]
    public void GivenAMissingSetting_WhenLoading_ThenItRefusesAndNamesTheClientAndTheSetting(string setting)
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page").Remove(0, setting);

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("personal-page", message);
        Assert.Contains(setting, message);
    }

    [UnitFact]
    public void GivenAClientWithoutAnId_WhenLoading_ThenItRefusesAndNamesItsPosition()
    {
        // Given
        var settings = new ClientSettings().Client(0, "first").Client(1, "second").Remove(1, "ID");

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("#1", message);
        Assert.Contains("ID", message);
    }

    [UnitTheory]
    [InlineData("SENDER", "not-an-address")]
    [InlineData("SENDER", "a@mg.example.com, b@mg.example.com")]
    [InlineData("RECIPIENTPOLICY", "everyone")]
    [InlineData("RECIPIENTPOLICY", "FIXED")]
    [InlineData("LIMITPERMINUTE", "0")]
    [InlineData("LIMITPERMINUTE", "-1")]
    [InlineData("LIMITPERMINUTE", "many")]
    [InlineData("LIMITPERDAY", "0")]
    [InlineData("LIMITPERDAY", "1.5")]
    public void GivenAnInvalidSetting_WhenLoading_ThenItRefusesAndNamesTheClientAndTheSetting(string setting, string value)
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page").Set(0, setting, value);

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("personal-page", message);
        Assert.Contains(setting, message);
    }

    [UnitFact]
    public void GivenAClientWithNoKey_WhenLoading_ThenItRefuses()
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page").Remove(0, "KEYHASHES:0");

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("KEYHASHES", message);
    }

    [UnitFact]
    public void GivenAClientWithThreeKeys_WhenLoading_ThenItRefuses()
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page")
            .Set(0, "KEYHASHES:1", ClientSettings.KeyHash())
            .Set(0, "KEYHASHES:2", ClientSettings.KeyHash());

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("KEYHASHES", message);
    }

    [UnitFact]
    public void GivenAClientReplacingItsKey_WhenLoading_ThenBothKeysAreActive()
    {
        // Given
        var second = ClientSettings.KeyHash();
        var settings = new ClientSettings().Client(0, "personal-page").Set(0, "KEYHASHES:1", second);

        // When
        var client = Assert.Single(Load(settings).Clients);

        // Then
        Assert.Equal(2, client.KeyHashes.Count);
        Assert.Contains(second, client.KeyHashes);
    }

    [UnitTheory]
    [InlineData("abc123")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void GivenAKeyHashThatIsNotASha256_WhenLoading_ThenItRefuses(string hash)
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page", hash);

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("KEYHASHES", message);
    }

    [UnitFact]
    public void GivenAnUppercaseKeyHash_WhenLoading_ThenItIsHeldInLowercase()
    {
        // Given
        var hash = ClientSettings.KeyHash();
        var settings = new ClientSettings().Client(0, "personal-page", hash.ToUpperInvariant());

        // When
        var client = Assert.Single(Load(settings).Clients);

        // Then
        Assert.Equal([hash], client.KeyHashes);
    }

    [UnitFact]
    public void GivenARefusedKeyHash_WhenLoading_ThenTheMessageDoesNotRepeatIt()
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page", "abc123");

        // When
        var message = Refusal(settings);

        // Then
        Assert.DoesNotContain("abc123", message);
    }

    [UnitFact]
    public void GivenAFixedPolicyWithNoRecipients_WhenLoading_ThenItRefuses()
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page").Remove(0, "ALLOWEDRECIPIENTS:0");

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("ALLOWEDRECIPIENTS", message);
    }

    [UnitFact]
    public void GivenAFixedPolicyWithAnInvalidRecipient_WhenLoading_ThenItRefuses()
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page").Set(0, "ALLOWEDRECIPIENTS:0", "not-an-address");

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("ALLOWEDRECIPIENTS", message);
    }

    [UnitFact]
    public void GivenAnAnyPolicyWithNoRecipients_WhenLoading_ThenItPermitsAnyone()
    {
        // Given
        var settings = new ClientSettings().Client(0, "heimdall")
            .Set(0, "RECIPIENTPOLICY", "any")
            .Remove(0, "ALLOWEDRECIPIENTS:0");

        // When
        var client = Assert.Single(Load(settings).Clients);

        // Then
        Assert.True(client.Policy.Permits("anyone@example.org"));
    }

    [UnitFact]
    public void GivenTwoClientsWithTheSameId_WhenLoading_ThenItRefuses()
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page").Client(1, "personal-page");

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("personal-page", message);
    }

    [UnitFact]
    public void GivenTwoClientsSharingAKeyHash_WhenLoading_ThenItRefusesWithoutRepeatingTheHash()
    {
        // Given
        var hash = ClientSettings.KeyHash();
        var settings = new ClientSettings().Client(0, "first", hash).Client(1, "second", hash);

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("KEYHASHES", message);
        Assert.DoesNotContain(hash, message);
    }

    [UnitFact]
    public void GivenMailgunAndASenderOffItsDomain_WhenLoading_ThenItRefuses()
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page", sender: "client@other.example.com").WithMailgun();

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("personal-page", message);
        Assert.Contains("SENDER", message);
    }

    [UnitFact]
    public void GivenMailgunAndASenderOnItsDomain_WhenLoading_ThenItIsAccepted()
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page", sender: "Client <client@MG.example.com>").WithMailgun();

        // When
        var directory = Load(settings);

        // Then
        Assert.Single(directory.Clients);
    }

    [UnitFact]
    public void GivenNoMailgun_WhenASenderIsOnAnyDomain_ThenItIsAccepted()
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page", sender: "client@other.example.com");

        // When
        var directory = Load(settings);

        // Then
        Assert.Single(directory.Clients);
    }

    [UnitTheory]
    [InlineData("MAILGUN_API_KEY")]
    [InlineData("MAILGUN_DOMAIN")]
    public void GivenProductionWithoutMailgun_WhenLoading_ThenItRefuses(string missing)
    {
        // Given
        var settings = new ClientSettings().ReadyForProduction().Global(missing, null);

        // When
        var message = Refusal(settings, isProduction: true);

        // Then
        Assert.Contains(missing, message);
    }

    [UnitFact]
    public void GivenProductionWithoutAnAlertAddress_WhenLoading_ThenItRefuses()
    {
        // Given
        var settings = new ClientSettings().ReadyForProduction().Global("HERMES_ALERT_TO", " ");

        // When
        var message = Refusal(settings, isProduction: true);

        // Then
        Assert.Contains("HERMES_ALERT_TO", message);
    }

    [UnitFact]
    public void GivenAnInvalidAlertAddress_WhenLoading_ThenItRefuses()
    {
        // Given
        var settings = new ClientSettings().Global("HERMES_ALERT_TO", "not-an-address");

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("HERMES_ALERT_TO", message);
    }

    [UnitFact]
    public void GivenProductionFullyConfigured_WhenLoading_ThenItIsAccepted()
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page").ReadyForProduction();

        // When
        var directory = Load(settings, isProduction: true);

        // Then
        Assert.Single(directory.Clients);
    }

    [UnitFact]
    public void GivenAnInvalidSecondKeyHash_WhenLoading_ThenItRefuses()
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page").Set(0, "KEYHASHES:1", "abc123");

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("KEYHASHES", message);
    }

    [UnitFact]
    public void GivenASecondKeySharedWithAnotherClient_WhenLoading_ThenItRefuses()
    {
        // Given
        var shared = ClientSettings.KeyHash();
        var settings = new ClientSettings().Client(0, "first", shared).Client(1, "second").Set(1, "KEYHASHES:1", shared);

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("second", message);
        Assert.Contains("KEYHASHES", message);
    }

    [UnitFact]
    public void GivenAnInvalidSecondRecipient_WhenLoading_ThenItRefuses()
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page").Set(0, "ALLOWEDRECIPIENTS:1", "not-an-address");

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("ALLOWEDRECIPIENTS", message);
    }

    [UnitFact]
    public void GivenAGapInTheIndexes_WhenAClientHasNoId_ThenItIsNamedByItsOwnIndex()
    {
        // Given
        var settings = new ClientSettings().Client(0, "first").Client(2, "third").Remove(2, "ID");

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("#2", message);
    }

    [UnitFact]
    public void GivenIdsDifferingOnlyInSurroundingSpaces_WhenLoading_ThenItRefuses()
    {
        // Given
        var settings = new ClientSettings().Client(0, "personal-page").Client(1, "personal-page ");

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("ID", message);
    }

    [UnitFact]
    public void GivenSeveralProblems_WhenLoading_ThenTheRefusalListsThemAll()
    {
        // Given
        var settings = new ClientSettings().Client(0, "first").Remove(0, "SENDER").Client(1, "second").Remove(1, "LIMITPERDAY");

        // When
        var message = Refusal(settings);

        // Then
        Assert.Contains("SENDER", message);
        Assert.Contains("LIMITPERDAY", message);
    }
}
