using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ArturRios.Hermes.WebApi.Tests;

/// <summary>
/// Makes every request look as if it arrived on <paramref name="port"/>. The in-memory server has no sockets, so the
/// local port is set ahead of the real pipeline, which tells the metrics port apart by it.
/// </summary>
internal sealed class ArrivesOnPort(int port) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Connection.LocalPort = port;

            return nextMiddleware();
        });

        next(app);
    };
}
