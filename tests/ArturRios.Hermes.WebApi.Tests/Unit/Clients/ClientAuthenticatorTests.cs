using System.Security.Cryptography;
using System.Text;
using ArturRios.Hermes.WebApi.Clients;
using ArturRios.Util.Test.Attributes;
using Bogus;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Clients;

public class ClientAuthenticatorTests
{
    private static readonly Randomizer Random = new();

    private readonly string _firstKey = Random.AlphaNumeric(40);
    private readonly string _secondKey = Random.AlphaNumeric(40);

    private static string Hash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private static Client ClientWith(string id, params string[] hashes) =>
        new(id, hashes, "client@example.com", RecipientPolicy.Any, 5, 50);

    private ClientAuthenticator Authenticator() =>
        new(new ClientDirectory([ClientWith("first", Hash(_firstKey), Hash(_secondKey)), ClientWith("other", Hash("other-key"))]));

    [UnitFact]
    public void GivenAnActiveKey_WhenAuthenticating_ThenItsClientIsReturned()
    {
        // When
        var client = Authenticator().Authenticate($"Bearer {_firstKey}");

        // Then
        Assert.Equal("first", client?.Id);
    }

    [UnitFact]
    public void GivenTheSecondActiveKey_WhenAuthenticating_ThenItsClientIsReturned()
    {
        // When
        var client = Authenticator().Authenticate($"Bearer {_secondKey}");

        // Then
        Assert.Equal("first", client?.Id);
    }

    [UnitFact]
    public void GivenAnotherClientsKey_WhenAuthenticating_ThenThatClientIsReturned()
    {
        // When
        var client = Authenticator().Authenticate("Bearer other-key");

        // Then
        Assert.Equal("other", client?.Id);
    }

    [UnitFact]
    public void GivenALowercaseScheme_WhenAuthenticating_ThenTheKeyIsStillRead()
    {
        // When
        var client = Authenticator().Authenticate($"bearer {_firstKey}");

        // Then
        Assert.Equal("first", client?.Id);
    }

    [UnitTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    [InlineData("Bearer unknown-key")]
    [InlineData("Basic dXNlcjpwYXNz")]
    public void GivenNoActiveKey_WhenAuthenticating_ThenNoClientIsReturned(string? authorization)
    {
        // When
        var client = Authenticator().Authenticate(authorization);

        // Then
        Assert.Null(client);
    }

    [UnitTheory]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    public void GivenAClientHoldingTheHashOfAnEmptyKey_WhenTheKeyIsEmpty_ThenNoClientIsReturned(string authorization)
    {
        // Given
        var authenticator = new ClientAuthenticator(new ClientDirectory([ClientWith("careless", Hash(string.Empty))]));

        // When
        var client = authenticator.Authenticate(authorization);

        // Then
        Assert.Null(client);
    }

    [UnitFact]
    public void GivenNoClients_WhenAuthenticating_ThenNoClientIsReturned()
    {
        // Given
        var authenticator = new ClientAuthenticator(new ClientDirectory([]));

        // When
        var client = authenticator.Authenticate($"Bearer {_firstKey}");

        // Then
        Assert.Null(client);
    }

    [UnitFact]
    public void GivenAnUppercaseScheme_WhenAuthenticating_ThenTheKeyIsStillRead()
    {
        // When
        var client = Authenticator().Authenticate($"BEARER {_firstKey}");

        // Then
        Assert.Equal("first", client?.Id);
    }

    [UnitFact]
    public void GivenAnActiveKeyWithACharacterMore_WhenAuthenticating_ThenNoClientIsReturned()
    {
        // When
        var client = Authenticator().Authenticate($"Bearer {_firstKey}x");

        // Then
        Assert.Null(client);
    }

    [UnitFact]
    public void GivenAKeyInTheHashesOwnForm_WhenAuthenticating_ThenNoClientIsReturned()
    {
        // When
        var client = Authenticator().Authenticate($"Bearer {Hash(_firstKey)}");

        // Then
        Assert.Null(client);
    }
}
