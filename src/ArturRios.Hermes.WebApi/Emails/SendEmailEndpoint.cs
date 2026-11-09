using System.Globalization;
using ArturRios.Output;
using ArturRios.Util.WebApi.AspNetCore;

namespace ArturRios.Hermes.WebApi.Emails;

/// <summary><c>POST /api/emails</c>: the one endpoint clients call.</summary>
public static class SendEmailEndpoint
{
    public const string Route = "/api/emails";
    public const int MaximumBodyBytes = 64 * 1024;

    public static void Map(IEndpointRouteBuilder routes) => routes.MapPost(Route, HandleAsync);

    private static async Task<IResult> HandleAsync(HttpContext context, SendEmailHandler handler, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(context.Request, cancellationToken);

        if (body is null)
        {
            return ToResult(DataOutput<string?>.New.WithError(SendEmailOutcomes.TooLarge));
        }

        var result = await handler.HandleAsync(context.Request.Headers.Authorization, body, cancellationToken);

        if (result.RetryAfterSeconds is { } retryAfter)
        {
            context.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
        }

        return ToResult(result.Output);
    }

    private static IResult ToResult(DataOutput<string?> output) =>
        Results.Json(output, statusCode: output.ResolveStatusCode(statusMap: SendEmailOutcomes.StatusMap));

    /// <returns>The body, or <see langword="null"/> when it is over the limit, whether or not it declared its length.</returns>
    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaximumBodyBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;

        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaximumBodyBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
