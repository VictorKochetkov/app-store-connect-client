namespace AppStoreConnect.Client.Models;

/// <summary>
/// TestFlight beta group.
/// </summary>
/// <param name="Id">Beta-group identifier.</param>
/// <param name="Name">Beta-group name.</param>
/// <param name="IsInternalGroup">Whether the group is restricted to internal testers.</param>
/// <param name="IsPublicLinkEnabled">Whether distribution through a public link is enabled.</param>
public sealed record TestFlightBetaGroup(
    string Id,
    string? Name,
    bool IsInternalGroup,
    bool IsPublicLinkEnabled);

