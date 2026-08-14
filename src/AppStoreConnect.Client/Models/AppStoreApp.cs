namespace AppStoreConnect.Client.Models;

/// <summary>
/// App Store Connect application.
/// </summary>
/// <param name="Id">App Store Connect application identifier.</param>
/// <param name="BundleId">Application bundle identifier.</param>
/// <param name="Name">Application display name.</param>
/// <param name="Sku">Application SKU.</param>
/// <param name="PrimaryLocale">Primary App Store locale.</param>
public sealed record AppStoreApp(
    string Id,
    string BundleId,
    string? Name,
    string? Sku,
    string? PrimaryLocale);

