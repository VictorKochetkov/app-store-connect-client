namespace AppStoreConnect.Client.Models;

/// <summary>
/// TestFlight build and its distribution state.
/// </summary>
/// <param name="Id">Build identifier.</param>
/// <param name="Version">Application marketing version.</param>
/// <param name="BuildNumber">Application build number.</param>
/// <param name="UploadedDate">Build upload time.</param>
/// <param name="ProcessingState">App Store Connect processing state.</param>
/// <param name="ExternalBuildState">External TestFlight distribution state.</param>
/// <param name="ExpirationDate">Time when the build expires and is removed from TestFlight.</param>
public sealed record TestFlightBuild(
    string Id,
    string Version,
    string BuildNumber,
    DateTimeOffset UploadedDate,
    string ProcessingState,
    string? ExternalBuildState,
    DateTimeOffset? ExpirationDate = null);
