namespace ArturRios.Hermes.WebApi.Platform;

/// <summary>
/// Liveness for Docker and yggdrasil's status API. It needs no credentials: Hermes has no public route, so only the
/// internal networks can reach it.
/// </summary>
public static class HealthEndpoint
{
    public const string Route = "/healthcheck";

    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapGet(Route, () => Results.Ok(new { status = "healthy" }));
}
