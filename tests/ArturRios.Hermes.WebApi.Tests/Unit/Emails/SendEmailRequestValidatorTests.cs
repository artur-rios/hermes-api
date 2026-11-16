using ArturRios.Hermes.WebApi.Emails;
using ArturRios.Util.Test.Attributes;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Emails;

public class SendEmailRequestValidatorTests
{
    private const string Sensitive = "sensitive-value";

    private static SendEmailRequest Valid() => new("to@example.com", "Subject", "Body", null);

    private static bool IsValid(SendEmailRequest request) => new SendEmailRequestValidator().Validate(request).IsValid;

    private static IEnumerable<string> Errors(SendEmailRequest request) =>
        new SendEmailRequestValidator().Validate(request).Errors.Select(error => error.ErrorMessage);

    [UnitFact]
    public void GivenAValidRequest_WhenValidated_ThenItPasses()
    {
        // When
        var valid = IsValid(Valid());

        // Then
        Assert.True(valid);
    }

    [UnitFact]
    public void GivenAValidReplyTo_WhenValidated_ThenItPasses()
    {
        // When
        var valid = IsValid(Valid() with { ReplyTo = "visitor@example.com" });

        // Then
        Assert.True(valid);
    }

    [UnitTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-address")]
    [InlineData("a@example.com, b@example.com")]
    [InlineData("Someone <a@example.com>")]
    public void GivenAnInvalidRecipient_WhenValidated_ThenItFails(string? to)
    {
        // When
        var valid = IsValid(Valid() with { To = to });

        // Then
        Assert.False(valid);
    }

    [UnitTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("First line\nBcc: other@example.com")]
    [InlineData("First line\r\nSecond")]
    public void GivenAnInvalidSubject_WhenValidated_ThenItFails(string? subject)
    {
        // When
        var valid = IsValid(Valid() with { Subject = subject });

        // Then
        Assert.False(valid);
    }

    [UnitFact]
    public void GivenASubjectAtTheLimit_WhenValidated_ThenItPasses()
    {
        // When
        var valid = IsValid(Valid() with { Subject = new string('s', 200) });

        // Then
        Assert.True(valid);
    }

    [UnitFact]
    public void GivenASubjectOneCharacterOverTheLimit_WhenValidated_ThenItFails()
    {
        // When
        var valid = IsValid(Valid() with { Subject = new string('s', 201) });

        // Then
        Assert.False(valid);
    }

    [UnitTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenAMissingBody_WhenValidated_ThenItFails(string? body)
    {
        // When
        var valid = IsValid(Valid() with { Body = body });

        // Then
        Assert.False(valid);
    }

    [UnitFact]
    public void GivenABodyAtTheLimit_WhenValidated_ThenItPasses()
    {
        // When
        var valid = IsValid(Valid() with { Body = new string('b', 10_000) });

        // Then
        Assert.True(valid);
    }

    [UnitFact]
    public void GivenABodyOneCharacterOverTheLimit_WhenValidated_ThenItFails()
    {
        // When
        var valid = IsValid(Valid() with { Body = new string('b', 10_001) });

        // Then
        Assert.False(valid);
    }

    [UnitFact]
    public void GivenABodyWithLineBreaks_WhenValidated_ThenItPasses()
    {
        // When
        var valid = IsValid(Valid() with { Body = "First line\r\nSecond line" });

        // Then
        Assert.True(valid);
    }

    [UnitTheory]
    [InlineData("")]
    [InlineData("not-an-address")]
    [InlineData("a@example.com, b@example.com")]
    public void GivenAnInvalidReplyTo_WhenValidated_ThenItFails(string replyTo)
    {
        // When
        var valid = IsValid(Valid() with { ReplyTo = replyTo });

        // Then
        Assert.False(valid);
    }

    [UnitTheory]
    [InlineData(null, "to is required.")]
    [InlineData("", "to is required.")]
    [InlineData("not-an-address", "to must be one valid e-mail address.")]
    public void GivenAnInvalidRecipient_WhenValidated_ThenOneMessageNamesTheField(string? to, string expected)
    {
        // When
        var errors = Errors(Valid() with { To = to });

        // Then
        Assert.Equal([expected], errors);
    }

    [UnitTheory]
    [InlineData(null, "subject is required.")]
    [InlineData("Line\nBreak", "subject must be a single line.")]
    public void GivenAnInvalidSubject_WhenValidated_ThenOneMessageNamesTheField(string? subject, string expected)
    {
        // When
        var errors = Errors(Valid() with { Subject = subject });

        // Then
        Assert.Equal([expected], errors);
    }

    [UnitFact]
    public void GivenASubjectOverTheLimitWithALineBreak_WhenValidated_ThenOnlyTheLengthIsReported()
    {
        // When
        var errors = Errors(Valid() with { Subject = new string('s', 201) + "\n" });

        // Then
        Assert.Equal(["subject must be at most 200 characters."], errors);
    }

    [UnitFact]
    public void GivenABodyOverTheLimit_WhenValidated_ThenOneMessageNamesTheField()
    {
        // When
        var errors = Errors(Valid() with { Body = new string('b', 10_001) });

        // Then
        Assert.Equal(["body must be at most 10000 characters."], errors);
    }

    [UnitFact]
    public void GivenAnInvalidReplyTo_WhenValidated_ThenOneMessageNamesTheField()
    {
        // When
        var errors = Errors(Valid() with { ReplyTo = "not-an-address" });

        // Then
        Assert.Equal(["replyTo must be one valid e-mail address."], errors);
    }

    [UnitFact]
    public void GivenEveryFieldMissing_WhenValidated_ThenEachRequiredFieldIsNamed()
    {
        // When
        var errors = Errors(new SendEmailRequest(null, null, null, null));

        // Then
        Assert.Equal(["to is required.", "subject is required.", "body is required."], errors);
    }

    [UnitFact]
    public void GivenInvalidFields_WhenValidated_ThenNoMessageRepeatsAValue()
    {
        // Given
        var request = new SendEmailRequest(Sensitive, new string('s', 201) + Sensitive, new string('b', 10_001) + Sensitive, Sensitive);

        // When
        var result = new SendEmailRequestValidator().Validate(request);

        // Then
        Assert.DoesNotContain(result.Errors, error => error.ErrorMessage.Contains(Sensitive));
    }
}
