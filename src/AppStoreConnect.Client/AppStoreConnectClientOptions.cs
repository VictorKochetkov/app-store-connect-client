namespace AppStoreConnect.Client;

/// <summary>
/// App Store Connect API client configuration.
/// </summary>
public sealed class AppStoreConnectClientOptions
{
    /// <summary>
    /// App Store Connect API key identifier.
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    /// App Store Connect issuer identifier.
    /// </summary>
    public required string IssuerId { get; init; }

    /// <summary>
    /// PKCS#8 private key from the App Store Connect <c>.p8</c> file.
    /// </summary>
    public required string PrivateKey { get; init; }

    /// <summary>
    /// App Store Connect API base address.
    /// </summary>
    public Uri BaseUrl { get; init; } = new ("https://api.appstoreconnect.apple.com/");

    /// <summary>
    /// Lifetime of generated App Store Connect API tokens.
    /// Must not exceed Apple's 20-minute limit.
    /// </summary>
    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromMinutes(10);
}

