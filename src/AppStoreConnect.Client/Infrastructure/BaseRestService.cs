using System.Net;
using System.Text.Json;

namespace AppStoreConnect.Client.Infrastructure;

/// <summary>
/// Provides common execution and error handling for REST requests.
/// </summary>
public abstract class BaseRestService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new ()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient httpClient;
    private readonly Uri baseUrl;

    /// <summary>
    /// Initializes the REST service.
    /// </summary>
    /// <param name="httpClient">Application-managed HTTP client.</param>
    /// <param name="baseUrl">REST API base address.</param>
    protected BaseRestService(HttpClient httpClient, Uri baseUrl)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(baseUrl);

        if (!baseUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("Base URL must be absolute.", nameof(baseUrl));
        }

        this.httpClient = httpClient;
        this.baseUrl = baseUrl.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl
            : new Uri($"{baseUrl.AbsoluteUri}/", UriKind.Absolute);
    }

    /// <summary>
    /// Executes a request and deserializes its response.
    /// </summary>
    /// <typeparam name="TResponse">Response body type.</typeparam>
    /// <param name="request">REST request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deserialized response body.</returns>
    /// <exception cref="AppStoreConnectApiException">The provider returned an unsuccessful response.</exception>
    /// <exception cref="JsonException">The provider returned a successful response without a JSON body.</exception>
    protected async Task<TResponse> ExecuteAsync<TResponse>(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await BeforeRequestAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);

                throw new AppStoreConnectApiException(
                    response.StatusCode,
                    responseBody,
                    request.RequestUri);
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            var result = await JsonSerializer.DeserializeAsync<TResponse>(
                    responseStream,
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            return result
                ?? throw new JsonException("App Store Connect returned an empty JSON response.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AppStoreConnectApiException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new AppStoreConnectApiException(
                exception.StatusCode ?? HttpStatusCode.ServiceUnavailable,
                responseBody: null,
                request.RequestUri,
                exception);
        }
    }

    /// <summary>
    /// Creates an HTTP request relative to the configured API base address.
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="relativePath">Relative request path.</param>
    /// <param name="queryParameters">Optional query-string parameters.</param>
    /// <returns>HTTP request.</returns>
    protected HttpRequestMessage CreateRequest(
        HttpMethod method,
        string relativePath,
        params (string Name, string Value)[] queryParameters)
    {
        ArgumentNullException.ThrowIfNull(method);

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Relative path must not be empty.", nameof(relativePath));
        }

        var requestUri = new Uri(baseUrl, relativePath);

        if (queryParameters.Length > 0)
        {
            var query = string.Join(
                "&",
                queryParameters.Select(parameter =>
                    $"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(parameter.Value)}"));
            var uriBuilder = new UriBuilder(requestUri)
            {
                Query = query,
            };
            requestUri = uriBuilder.Uri;
        }

        return new HttpRequestMessage(method, requestUri);
    }

    /// <summary>
    /// Customizes a request immediately before it is sent.
    /// </summary>
    /// <param name="request">REST request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents request customization.</returns>
    protected virtual Task BeforeRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Completes the client lifetime without disposing the caller-provided
    /// <see cref="HttpClient"/>.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
