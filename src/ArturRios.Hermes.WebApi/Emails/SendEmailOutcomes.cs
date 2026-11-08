namespace ArturRios.Hermes.WebApi.Emails;

/// <summary>What a send can end as, and the HTTP status of each. A failed validation falls to the default 400.</summary>
public static class SendEmailOutcomes
{
    public const string Accepted = "The message was accepted for delivery.";
    public const string TooLarge = "The request body is larger than 64 KB.";
    public const string Unauthorized = "The API key is missing or is not active.";
    public const string InvalidBody = "The request body is not a valid send request.";
    public const string RecipientNotPermitted = "This client may not send to that recipient.";
    public const string LimitReached = "This client has reached a sending limit.";
    public const string DeliveryFailed = "The message could not be delivered.";

    public static IReadOnlyDictionary<string, int> StatusMap { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [Accepted] = StatusCodes.Status202Accepted,
        [TooLarge] = StatusCodes.Status413PayloadTooLarge,
        [Unauthorized] = StatusCodes.Status401Unauthorized,
        [RecipientNotPermitted] = StatusCodes.Status403Forbidden,
        [LimitReached] = StatusCodes.Status429TooManyRequests,
        [DeliveryFailed] = StatusCodes.Status502BadGateway,
    };
}
