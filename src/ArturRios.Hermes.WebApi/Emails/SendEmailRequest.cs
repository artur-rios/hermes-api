using System.Text.Json.Serialization;

namespace ArturRios.Hermes.WebApi.Emails;

/// <summary>
/// One message to send. It exists only while the request is handled. An unknown field is refused, so a request can
/// never appear to set a sender or a client: those come from the key.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SendEmailRequest(string? To, string? Subject, string? Body, string? ReplyTo);
