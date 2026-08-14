using System.Net;
using System.Text.Json;
using RestSharp;

namespace AppStoreConnect.Client.Infrastructure;

/// <summary>
/// Provides common execution and error handling for REST requests.
/// </summary>
public abstract class BaseRestService : IDisposable
{
    private readonly RestClient restClient;

    /// <summary>
    /// Initializes the REST service.
    /// </summary>
    /// <param name="restClient">Configured REST client.</param>
    protected BaseRestService(RestClient restClient)
    {
        this.restClient = restClient;
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
        RestRequest request,
        CancellationToken cancellationToken = default)
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await BeforeRequestAsync(request, cancellationToken).ConfigureAwait(false);
        var response = await restClient.ExecuteAsync<TResponse>(request, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (!response.IsSuccessful)
        {
            throw new AppStoreConnectApiException(
                response.StatusCode == 0 ? HttpStatusCode.ServiceUnavailable : response.StatusCode,
                response.Content,
                response.ResponseUri ?? restClient.BuildUri(request),
                response.ErrorException);
        }

        return response.Data
            ?? throw new JsonException("App Store Connect returned an empty JSON response.");
    }

    /// <summary>
    /// Customizes a request immediately before it is sent.
    /// </summary>
    /// <param name="request">REST request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents request customization.</returns>
    protected virtual Task BeforeRequestAsync(
        RestRequest request,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Releases resources owned by the REST client.
    /// The caller-provided <see cref="HttpClient"/> is not disposed.
    /// </summary>
    public void Dispose()
    {
        restClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
