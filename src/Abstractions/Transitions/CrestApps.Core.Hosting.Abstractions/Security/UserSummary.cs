namespace CrestApps.Core.Security;

/// <summary>
/// What the suite needs to know about a user: enough to show who they are and decide whether they
/// can still work.
/// </summary>
/// <remarks>
/// Deliberately a projection rather than the host's own user type. The suite never mutates a user
/// through this, and never needs a field the host has not chosen to expose.
/// </remarks>
/// <param name="Id">The user's stable identifier, which is what the suite stores against agents and interactions.</param>
/// <param name="UserName">The sign-in name.</param>
/// <param name="DisplayName">The name to show. Falls back to the sign-in name when the host has nothing better.</param>
/// <param name="Email">The email address, when the host has one.</param>
public sealed record UserSummary(
    string Id,
    string UserName,
    string DisplayName,
    string Email);
