using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using ArturRios.Messaging.Email;
using ArturRios.Output;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ArturRios.Hermes.WebApi.Tests;

/// <summary>
/// The real application in process, with test clients supplied through configuration and Mailgun replaced by a double.
/// <c>WebApiTest</c> does not expose the host builder that takes those two.
/// </summary>
internal sealed class HermesApi : IDisposable
{
    public const string Route = "/api/emails";

    private readonly WebApplicationFactory<Program> _factory;

    public HermesApi(
        ClientSettings settings, TestClock? clock = null, bool replaceMailgun = true, HttpMessageHandler? mailgunHandler = null)
    {
        Email.Setup(email => email.SendEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((message, _) => Sent.Add(message))
            .ReturnsAsync(new ProcessOutput());

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("local")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings.Values))
            .ConfigureTestServices(services =>
            {
                if (replaceMailgun)
                {
                    services.AddTransient(_ => Email.Object);
                }

                if (mailgunHandler is not null)
                {
                    services.AddHttpClient<MailgunEmailService>().ConfigurePrimaryHttpMessageHandler(() => mailgunHandler);
                }

                if (clock is not null)
                {
                    services.AddSingleton(clock.Provider);
                }
            }));
    }

    public Mock<IEmailService> Email { get; } = new();

    public List<EmailMessage> Sent { get; } = [];

    public IServiceProvider Services => _factory.Services;

    public static string HashOf(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public Task<HttpResponseMessage> PostAsync(HttpContent content, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = content };

        if (key is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        return _factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    public Task<HttpResponseMessage> SendAsync(object body, string? key) => PostAsync(JsonContent.Create(body), key);

    public void Dispose() => _factory.Dispose();
}

/// <summary>Answers every request as Mailgun would, so the real service runs without reaching the network.</summary>
internal sealed class StubMailgun(HttpStatusCode status, string responseBody) : HttpMessageHandler
{
    public int Calls => Requests.Count;

    public List<string> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

        return new HttpResponseMessage(status) { Content = new StringContent(responseBody) };
    }
}
