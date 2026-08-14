using System.Text.Json;
using AppStoreConnect.Client.Infrastructure;
using AppStoreConnect.Client.Models;
using RestSharp;

namespace AppStoreConnect.Client;

/// <summary>
/// Provides typed access to App Store Connect applications and TestFlight distribution data.
/// </summary>
public sealed class AppStoreConnectClient : BaseRestService, IAppStoreConnectClient
{
    private const string AvailableExternalBetaState = "IN_BETA_TESTING";
    private const string ValidProcessingState = "VALID";
    private const string IosPlatform = "IOS";
    private readonly AppStoreConnectTokenProvider tokenProvider;

    /// <summary>
    /// Initializes an App Store Connect client over an application-managed HTTP pipeline.
    /// </summary>
    /// <param name="httpClient">HTTP client, optionally configured with a proxy or custom handlers.</param>
    /// <param name="options">App Store Connect credentials and client settings.</param>
    /// <param name="timeProvider">Optional current-time provider used to generate access tokens.</param>
    public AppStoreConnectClient(
        HttpClient httpClient,
        AppStoreConnectClientOptions options,
        TimeProvider? timeProvider = null)
        : base(CreateRestClient(httpClient, options))
    {
        tokenProvider = new AppStoreConnectTokenProvider(
            options,
            timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc />
    public async Task<AppStoreApp?> GetAppAsync(
        string bundleId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredValue(bundleId, nameof(bundleId));

        var response = await ExecuteAsync<AppStoreResourceCollection<AppStoreAppAttributes>>(
            new RestRequest("v1/apps", Method.Get)
                .AddQueryParameter("filter[bundleId]", bundleId)
                .AddQueryParameter("limit", 1),
            cancellationToken).ConfigureAwait(false);
        var app = response.Data.FirstOrDefault();

        return app == null
            ? null
            : new AppStoreApp(
                app.Id,
                app.Attributes.BundleId,
                app.Attributes.Name,
                app.Attributes.Sku,
                app.Attributes.PrimaryLocale);
    }

    /// <inheritdoc />
    public async Task<TestFlightBetaGroup> GetBetaGroupAsync(
        string betaGroupId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredValue(betaGroupId, nameof(betaGroupId));

        var response = await ExecuteAsync<AppStoreResourceResponse<BetaGroupAttributes>>(
            new RestRequest("v1/betaGroups/{groupId}", Method.Get)
                .AddUrlSegment("groupId", betaGroupId),
            cancellationToken).ConfigureAwait(false);

        return new TestFlightBetaGroup(
            response.Data.Id,
            response.Data.Attributes.Name,
            response.Data.Attributes.IsInternalGroup,
            response.Data.Attributes.PublicLinkEnabled);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TestFlightBuild>> GetBuildsAsync(
        string appId,
        string betaGroupId,
        CancellationToken cancellationToken = default)
    {
        var builds = await GetBuildCandidatesAsync(appId, betaGroupId, cancellationToken)
            .ConfigureAwait(false);

        return builds
            .Select(ToPublicBuild)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<TestFlightBuild?> GetLatestPublicTestFlightBuildAsync(
        string bundleId,
        string betaGroupId,
        CancellationToken cancellationToken = default)
    {
        var app = await GetAppAsync(bundleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"App Store Connect has no application with bundle ID '{bundleId}'.");
        var group = await GetBetaGroupAsync(betaGroupId, cancellationToken).ConfigureAwait(false);

        if (group.IsInternalGroup || !group.IsPublicLinkEnabled)
        {
            throw new InvalidOperationException(
                $"TestFlight beta group '{betaGroupId}' is not an external group with an enabled public link.");
        }

        var builds = await GetBuildCandidatesAsync(app.Id, betaGroupId, cancellationToken)
            .ConfigureAwait(false);

        return builds
            .Where(build => string.Equals(build.Platform, IosPlatform, StringComparison.Ordinal))
            .Where(build => string.Equals(
                build.ExternalBuildState,
                AvailableExternalBetaState,
                StringComparison.Ordinal))
            .OrderByDescending(build => build.UploadedDate)
            .Select(ToPublicBuild)
            .FirstOrDefault();
    }

    /// <inheritdoc />
    protected override Task BeforeRequestAsync(
        RestRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        request.AddOrUpdateHeader("Authorization", $"Bearer {tokenProvider.CreateToken()}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets valid, non-expired build resources and resolves their included relationships.
    /// </summary>
    /// <param name="appId">App Store Connect application identifier.</param>
    /// <param name="betaGroupId">TestFlight beta-group identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resolved build candidates ordered by upload time, newest first.</returns>
    private async Task<IReadOnlyList<TestFlightBuildCandidate>> GetBuildCandidatesAsync(
        string appId,
        string betaGroupId,
        CancellationToken cancellationToken)
    {
        ValidateRequiredValue(appId, nameof(appId));
        ValidateRequiredValue(betaGroupId, nameof(betaGroupId));

        var response = await ExecuteAsync<TestFlightBuildsResponse>(
            new RestRequest("v1/builds", Method.Get)
                .AddQueryParameter("filter[app]", appId)
                .AddQueryParameter("filter[betaGroups]", betaGroupId)
                .AddQueryParameter("filter[processingState]", ValidProcessingState)
                .AddQueryParameter("filter[expired]", false)
                .AddQueryParameter("include", "preReleaseVersion,buildBetaDetail")
                .AddQueryParameter("sort", "-uploadedDate")
                .AddQueryParameter("limit", 50),
            cancellationToken).ConfigureAwait(false);

        return response.Data
            .Where(build => !build.Attributes.Expired)
            .Where(build => string.Equals(
                build.Attributes.ProcessingState,
                ValidProcessingState,
                StringComparison.Ordinal))
            .Select(build => CreateBuildCandidate(build, response.Included))
            .Where(build => build != null)
            .Cast<TestFlightBuildCandidate>()
            .OrderByDescending(build => build.UploadedDate)
            .ToArray();
    }

    /// <summary>
    /// Resolves a build's version and beta-detail relationships.
    /// </summary>
    /// <param name="build">Build resource.</param>
    /// <param name="included">Included relationship resources.</param>
    /// <returns>Resolved build candidate, or <see langword="null"/> for incomplete data.</returns>
    private static TestFlightBuildCandidate? CreateBuildCandidate(
        AppStoreResource<TestFlightBuildAttributes> build,
        IReadOnlyList<JsonElement> included)
    {
        var version = GetIncludedAttributes<PreReleaseVersionAttributes>(
            included,
            "preReleaseVersions",
            build.Relationships?.PreReleaseVersion?.Data?.Id);
        var betaDetails = GetIncludedAttributes<BuildBetaDetailAttributes>(
            included,
            "buildBetaDetails",
            build.Relationships?.BuildBetaDetail?.Data?.Id);

        return version == null || string.IsNullOrWhiteSpace(version.Version)
            ? null
            : new TestFlightBuildCandidate(
                build.Id,
                version.Version,
                version.Platform,
                build.Attributes.BuildNumber,
                build.Attributes.UploadedDate,
                build.Attributes.ProcessingState,
                betaDetails?.ExternalBuildState);
    }

    /// <summary>
    /// Deserializes attributes of an included App Store Connect relationship resource.
    /// </summary>
    /// <typeparam name="TAttributes">Relationship attributes type.</typeparam>
    /// <param name="included">Included resources.</param>
    /// <param name="type">Expected resource type.</param>
    /// <param name="id">Expected resource identifier.</param>
    /// <returns>Relationship attributes, or <see langword="null"/>.</returns>
    private static TAttributes? GetIncludedAttributes<TAttributes>(
        IReadOnlyList<JsonElement> included,
        string type,
        string? id)
        where TAttributes : class
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (var resource in included)
        {
            if (resource.TryGetProperty("type", out var resourceType)
                && string.Equals(resourceType.GetString(), type, StringComparison.Ordinal)
                && resource.TryGetProperty("id", out var resourceId)
                && string.Equals(resourceId.GetString(), id, StringComparison.Ordinal)
                && resource.TryGetProperty("attributes", out var attributes))
            {
                return attributes.Deserialize<TAttributes>();
            }
        }

        return null;
    }

    /// <summary>
    /// Maps an internal App Store Connect build resource to the public contract.
    /// </summary>
    /// <param name="build">Resolved build resource.</param>
    /// <returns>Public TestFlight build model.</returns>
    private static TestFlightBuild ToPublicBuild(TestFlightBuildCandidate build)
        => new (
            build.Id,
            build.Version,
            build.BuildNumber,
            build.UploadedDate,
            build.ProcessingState,
            build.ExternalBuildState);

    /// <summary>
    /// Creates a RestSharp client without taking ownership of the supplied HTTP client.
    /// </summary>
    /// <param name="httpClient">Application-managed HTTP client.</param>
    /// <param name="options">Client configuration.</param>
    /// <returns>Configured REST client.</returns>
    private static RestClient CreateRestClient(
        HttpClient httpClient,
        AppStoreConnectClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.BaseUrl);

        if (!options.BaseUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("BaseUrl must be absolute.", nameof(options));
        }

        return new RestClient(
            httpClient,
            new RestClientOptions
            {
                BaseUrl = options.BaseUrl,
            },
            disposeHttpClient: false);
    }

    /// <summary>
    /// Validates a mandatory identifier.
    /// </summary>
    /// <param name="value">Identifier value.</param>
    /// <param name="parameterName">Method parameter name.</param>
    private static void ValidateRequiredValue(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }
    }
}
