using System.Net.Sockets;

namespace ArturRios.Hermes.WebApi.Platform;

/// <summary>
/// Turns a failed call into one of a fixed set of words, the same vocabulary as yggdrasil's status API. The report is
/// readable by every container on the internal networks, so no exception text ever reaches it.
/// </summary>
public static class HealthReason
{
    public static string From(Exception exception)
    {
        if (exception is OperationCanceledException or TimeoutException)
        {
            return "timeout";
        }

        if (exception is not HttpRequestException request)
        {
            return "request failed";
        }

        if (request.InnerException is SocketException socket)
        {
            switch (socket.SocketErrorCode)
            {
                case SocketError.ConnectionRefused:
                    return "connection refused";
                case SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData:
                    return "name not resolved";
                case SocketError.HostUnreachable or SocketError.NetworkUnreachable:
                    return "host unreachable";
                case SocketError.ConnectionReset:
                    return "connection reset";
            }
        }

        return request.HttpRequestError switch
        {
            HttpRequestError.NameResolutionError => "name not resolved",
            HttpRequestError.ConnectionError => "connection failed",
            HttpRequestError.SecureConnectionError => "tls error",
            HttpRequestError.ResponseEnded => "connection closed",
            HttpRequestError.InvalidResponse => "invalid response",
            _ => "request failed",
        };
    }
}
