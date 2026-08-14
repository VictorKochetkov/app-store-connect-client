using AppStoreConnect.Client.Models;

namespace AppStoreConnect.Client;

/// <summary>
/// Provides typed access to applications and TestFlight distribution data in App Store Connect.
/// </summary>
public interface IAppStoreConnectClient
{
    /// <summary>
    /// Finds an application by its bundle identifier.
    /// </summary>
    /// <param name="bundleId">Application bundle identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The application, or <see langword="null"/> when it does not exist.</returns>
    Task<AppStoreApp?> GetAppAsync(
        string bundleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a TestFlight beta group.
    /// </summary>
    /// <param name="betaGroupId">Beta-group identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Beta-group information.</returns>
    Task<TestFlightBetaGroup> GetBetaGroupAsync(
        string betaGroupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets valid, non-expired TestFlight builds assigned to a beta group.
    /// </summary>
    /// <param name="appId">App Store Connect application identifier.</param>
    /// <param name="betaGroupId">Beta-group identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>TestFlight builds ordered by upload time, newest first.</returns>
    Task<IReadOnlyList<TestFlightBuild>> GetBuildsAsync(
        string appId,
        string betaGroupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest iOS build currently distributed through an enabled public TestFlight link.
    /// </summary>
    /// <param name="bundleId">Application bundle identifier.</param>
    /// <param name="betaGroupId">Public beta-group identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest public TestFlight build, or <see langword="null"/> when no build is available.</returns>
    Task<TestFlightBuild?> GetLatestPublicTestFlightBuildAsync(
        string bundleId,
        string betaGroupId,
        CancellationToken cancellationToken = default);
}

