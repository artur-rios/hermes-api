using ArturRios.Hermes.WebApi.Clients;
using ArturRios.Util.Test.Attributes;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Clients;

public class RecipientPolicyTests
{
    [UnitFact]
    public void GivenAFixedPolicy_WhenTheRecipientIsListed_ThenItIsPermitted()
    {
        // Given
        var policy = RecipientPolicy.Fixed(["owner@example.com"]);

        // When
        var permitted = policy.Permits("owner@example.com");

        // Then
        Assert.True(permitted);
    }

    [UnitFact]
    public void GivenAFixedPolicy_WhenTheRecipientDiffersOnlyInCase_ThenItIsPermitted()
    {
        // Given
        var policy = RecipientPolicy.Fixed(["owner@example.com"]);

        // When
        var permitted = policy.Permits("Owner@Example.com");

        // Then
        Assert.True(permitted);
    }

    [UnitFact]
    public void GivenAFixedPolicy_WhenTheRecipientIsNotListed_ThenItIsNotPermitted()
    {
        // Given
        var policy = RecipientPolicy.Fixed(["owner@example.com"]);

        // When
        var permitted = policy.Permits("stranger@example.com");

        // Then
        Assert.False(permitted);
    }

    [UnitFact]
    public void GivenAFixedPolicyOfSeveralRecipients_WhenAnyOfThemIsChecked_ThenItIsPermitted()
    {
        // Given
        var policy = RecipientPolicy.Fixed(["owner@example.com", "Other@Example.com"]);

        // When
        var permitted = policy.Permits("other@example.com");

        // Then
        Assert.True(permitted);
    }

    [UnitFact]
    public void GivenAFixedPolicyOfNoRecipients_WhenCheckingARecipient_ThenItIsNotPermitted()
    {
        // Given
        var policy = RecipientPolicy.Fixed([]);

        // When
        var permitted = policy.Permits("owner@example.com");

        // Then
        Assert.False(permitted);
    }

    [UnitFact]
    public void GivenAFixedPolicy_WhenTheRecipientOnlyStartsWithAListedOne_ThenItIsNotPermitted()
    {
        // Given
        var policy = RecipientPolicy.Fixed(["owner@example.com"]);

        // When
        var permitted = policy.Permits("owner@example.com.evil.example");

        // Then
        Assert.False(permitted);
    }

    [UnitFact]
    public void GivenAnAnyPolicy_WhenCheckingARecipient_ThenItIsPermitted()
    {
        // Given
        var policy = RecipientPolicy.Any;

        // When
        var permitted = policy.Permits("stranger@example.com");

        // Then
        Assert.True(permitted);
    }
}
