using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppStoreConnect.Client.Infrastructure;

internal sealed class AppStoreResourceCollection<TAttributes>
{
    [JsonPropertyName("data")]
    public IReadOnlyList<AppStoreResource<TAttributes>> Data { get; init; } = [];
}

internal sealed class AppStoreResourceResponse<TAttributes>
{
    [JsonPropertyName("data")]
    public AppStoreResource<TAttributes> Data { get; init; } = default!;
}

internal sealed class AppStoreResource<TAttributes>
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("attributes")]
    public TAttributes Attributes { get; init; } = default!;

    [JsonPropertyName("relationships")]
    public TestFlightBuildRelationships? Relationships { get; init; }
}

internal sealed class AppStoreAppAttributes
{
    [JsonPropertyName("bundleId")]
    public string BundleId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("sku")]
    public string? Sku { get; init; }

    [JsonPropertyName("primaryLocale")]
    public string? PrimaryLocale { get; init; }
}

internal sealed class BetaGroupAttributes
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("isInternalGroup")]
    public bool IsInternalGroup { get; init; }

    [JsonPropertyName("publicLinkEnabled")]
    public bool PublicLinkEnabled { get; init; }
}

internal sealed class TestFlightBuildsResponse
{
    [JsonPropertyName("data")]
    public IReadOnlyList<AppStoreResource<TestFlightBuildAttributes>> Data { get; init; } = [];

    [JsonPropertyName("included")]
    public IReadOnlyList<JsonElement> Included { get; init; } = [];
}

internal sealed class TestFlightBuildAttributes
{
    [JsonPropertyName("version")]
    public string BuildNumber { get; init; } = string.Empty;

    [JsonPropertyName("uploadedDate")]
    public DateTimeOffset UploadedDate { get; init; }

    [JsonPropertyName("expired")]
    public bool Expired { get; init; }

    [JsonPropertyName("processingState")]
    public string ProcessingState { get; init; } = string.Empty;
}

internal sealed class TestFlightBuildRelationships
{
    [JsonPropertyName("preReleaseVersion")]
    public AppStoreRelationship? PreReleaseVersion { get; init; }

    [JsonPropertyName("buildBetaDetail")]
    public AppStoreRelationship? BuildBetaDetail { get; init; }
}

internal sealed class AppStoreRelationship
{
    [JsonPropertyName("data")]
    public AppStoreResourceIdentifier? Data { get; init; }
}

internal sealed class AppStoreResourceIdentifier
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}

internal sealed class PreReleaseVersionAttributes
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("platform")]
    public string Platform { get; init; } = string.Empty;
}

internal sealed class BuildBetaDetailAttributes
{
    [JsonPropertyName("externalBuildState")]
    public string ExternalBuildState { get; init; } = string.Empty;
}

internal sealed record TestFlightBuildCandidate(
    string Id,
    string Version,
    string Platform,
    string BuildNumber,
    DateTimeOffset UploadedDate,
    string ProcessingState,
    string? ExternalBuildState);
