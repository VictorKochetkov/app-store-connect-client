using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AppStoreConnect.Client.Infrastructure;

/// <summary>
/// Creates short-lived App Store Connect ES256 access tokens.
/// </summary>
internal sealed class AppStoreConnectTokenProvider
{
    private static readonly TimeSpan MaximumTokenLifetime = TimeSpan.FromMinutes(20);
    private readonly AppStoreConnectClientOptions options;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes the token provider.
    /// </summary>
    /// <param name="options">Client configuration.</param>
    /// <param name="timeProvider">Current-time provider.</param>
    public AppStoreConnectTokenProvider(
        AppStoreConnectClientOptions options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        ValidateRequiredValue(options.KeyId, nameof(options.KeyId));
        ValidateRequiredValue(options.IssuerId, nameof(options.IssuerId));
        ValidateRequiredValue(options.PrivateKey, nameof(options.PrivateKey));

        if (options.TokenLifetime <= TimeSpan.Zero
            || options.TokenLifetime > MaximumTokenLifetime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.TokenLifetime,
                "TokenLifetime must be greater than zero and must not exceed 20 minutes.");
        }

        this.options = options;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Creates a signed App Store Connect API token.
    /// </summary>
    /// <returns>Compact JWT representation.</returns>
    public string CreateToken()
    {
        var issuedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var expiresAt = issuedAt + (long)options.TokenLifetime.TotalSeconds;
        var header = EncodeJson(new
        {
            alg = "ES256",
            kid = options.KeyId,
            typ = "JWT",
        });
        var payload = EncodeJson(new
        {
            iss = options.IssuerId,
            iat = issuedAt,
            exp = expiresAt,
            aud = "appstoreconnect-v1",
        });
        var unsignedToken = $"{header}.{payload}";

        using var algorithm = ECDsa.Create();
        algorithm.ImportFromPem(NormalizePem(options.PrivateKey));
        var signature = algorithm.SignData(
            Encoding.ASCII.GetBytes(unsignedToken),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{unsignedToken}.{Base64UrlEncode(signature)}";
    }

    /// <summary>
    /// Serializes and Base64Url-encodes a JWT section.
    /// </summary>
    /// <param name="value">JWT section value.</param>
    /// <returns>Encoded JWT section.</returns>
    private static string EncodeJson<TValue>(TValue value)
        => Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(value));

    /// <summary>
    /// Encodes bytes using unpadded Base64Url.
    /// </summary>
    /// <param name="value">Raw bytes.</param>
    /// <returns>Base64Url value.</returns>
    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    /// <summary>
    /// Normalizes escaped line breaks from JSON and environment configuration.
    /// </summary>
    /// <param name="value">Configured PEM value.</param>
    /// <returns>Normalized PEM value.</returns>
    private static string NormalizePem(string value)
        => value.Replace("\\n", "\n", StringComparison.Ordinal);

    /// <summary>
    /// Validates a mandatory string option.
    /// </summary>
    /// <param name="value">Configured value.</param>
    /// <param name="propertyName">Option property name.</param>
    private static void ValidateRequiredValue(string? value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{propertyName} must not be empty.", propertyName);
        }
    }
}
