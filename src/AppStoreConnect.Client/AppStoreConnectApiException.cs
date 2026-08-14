using System.Net;

namespace AppStoreConnect.Client;

/// <summary>
/// Represents an unsuccessful response returned by App Store Connect.
/// </summary>
public sealed class AppStoreConnectApiException : HttpRequestException
{
    /// <summary>
    /// Initializes a new App Store Connect API exception.
    /// </summary>
    /// <param name="statusCode">HTTP status code.</param>
    /// <param name="responseBody">Provider response body.</param>
    /// <param name="requestUri">Request URI.</param>
    /// <param name="innerException">Optional transport or deserialization exception.</param>
    internal AppStoreConnectApiException(
        HttpStatusCode statusCode,
        string? responseBody,
        Uri? requestUri,
        Exception? innerException = null)
        : base(
            $"App Store Connect request to '{requestUri}' failed with HTTP {(int)statusCode}: {responseBody}",
            innerException,
            statusCode)
    {
        ResponseBody = responseBody;
        RequestUri = requestUri;
    }

    /// <summary>
    /// Gets the response body returned by App Store Connect.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Gets the failed request URI.
    /// </summary>
    public Uri? RequestUri { get; }
}

