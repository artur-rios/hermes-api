using System.Net.Sockets;
using ArturRios.Hermes.WebApi.Platform;
using ArturRios.Util.Test.Attributes;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Platform;

public class HealthReasonTests
{
    private const string Secret = "secret-detail-that-must-not-leak";

    [UnitFact]
    public void GivenAnUnknownException_WhenDescribed_ThenItIsRequestFailedWithoutItsText()
    {
        // When
        var reason = HealthReason.From(new InvalidOperationException(Secret));

        // Then
        Assert.Equal("request failed", reason);
    }

    [UnitTheory]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(OperationCanceledException))]
    [InlineData(typeof(TimeoutException))]
    public void GivenACancellationOrATimeout_WhenDescribed_ThenItIsTimeout(Type exceptionType)
    {
        // Given
        var exception = (Exception)Activator.CreateInstance(exceptionType, Secret)!;

        // When
        var reason = HealthReason.From(exception);

        // Then
        Assert.Equal("timeout", reason);
    }

    [UnitTheory]
    [InlineData(SocketError.ConnectionRefused, "connection refused")]
    [InlineData(SocketError.HostNotFound, "name not resolved")]
    [InlineData(SocketError.TryAgain, "name not resolved")]
    [InlineData(SocketError.NoData, "name not resolved")]
    [InlineData(SocketError.HostUnreachable, "host unreachable")]
    [InlineData(SocketError.NetworkUnreachable, "host unreachable")]
    [InlineData(SocketError.ConnectionReset, "connection reset")]
    public void GivenASocketError_WhenDescribed_ThenItIsItsWord(SocketError error, string expected)
    {
        // When
        var reason = HealthReason.From(new HttpRequestException(Secret, new SocketException((int)error)));

        // Then
        Assert.Equal(expected, reason);
    }

    [UnitFact]
    public void GivenASocketErrorWithNoWordOfItsOwn_WhenDescribed_ThenTheRequestErrorDecides()
    {
        // Given
        var exception = new HttpRequestException(
            HttpRequestError.ConnectionError, Secret, new SocketException((int)SocketError.AccessDenied));

        // When
        var reason = HealthReason.From(exception);

        // Then
        Assert.Equal("connection failed", reason);
    }

    [UnitTheory]
    [InlineData(HttpRequestError.NameResolutionError, "name not resolved")]
    [InlineData(HttpRequestError.ConnectionError, "connection failed")]
    [InlineData(HttpRequestError.SecureConnectionError, "tls error")]
    [InlineData(HttpRequestError.ResponseEnded, "connection closed")]
    [InlineData(HttpRequestError.InvalidResponse, "invalid response")]
    [InlineData(HttpRequestError.Unknown, "request failed")]
    [InlineData(HttpRequestError.ProxyTunnelError, "request failed")]
    public void GivenARequestError_WhenDescribed_ThenItIsItsWord(HttpRequestError error, string expected)
    {
        // When
        var reason = HealthReason.From(new HttpRequestException(error, Secret));

        // Then
        Assert.Equal(expected, reason);
    }
}
