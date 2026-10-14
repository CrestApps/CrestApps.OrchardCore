using CrestApps.OrchardCore.TenantHierarchy.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Decides whether a delegated access session is still active.
/// </summary>
public static class DelegatedSessionRules
{
    /// <summary>
    /// The session was ended earlier.
    /// </summary>
    public const string Ended = "Ended";

    /// <summary>
    /// The session was idle longer than the policy allows.
    /// </summary>
    public const string IdleTimeout = "IdleTimeout";

    /// <summary>
    /// The session is older than the policy allows.
    /// </summary>
    public const string LifetimeExceeded = "LifetimeExceeded";

    /// <summary>
    /// The parent user no longer exists or is disabled.
    /// </summary>
    public const string UserDisabled = "UserDisabled";

    /// <summary>
    /// The security stamp of the parent user changed, for example after a password change.
    /// </summary>
    public const string SecurityStampChanged = "SecurityStampChanged";

    /// <summary>
    /// No grant covers the child tenant any more.
    /// </summary>
    public const string GrantRemoved = "GrantRemoved";

    /// <summary>
    /// The child tenant is no longer ready or no longer belongs to the parent.
    /// </summary>
    public const string ChildUnavailable = "ChildUnavailable";

    /// <summary>
    /// The parent user signed out.
    /// </summary>
    public const string SignedOut = "SignedOut";

    /// <summary>
    /// The parent user signed out everywhere.
    /// </summary>
    public const string SignedOutEverywhere = "SignedOutEverywhere";

    /// <summary>
    /// The user signed out of the child tenant.
    /// </summary>
    public const string ChildSignOut = "ChildSignOut";

    /// <summary>
    /// The child tenant was removed.
    /// </summary>
    public const string ChildRemoved = "ChildRemoved";

    /// <summary>
    /// Returns why a session is no longer active, or <see langword="null"/> when it is active.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="policy">The parent policy.</param>
    /// <param name="utcNow">The current time.</param>
    /// <param name="userIsEnabled">Whether the parent user exists and is enabled.</param>
    /// <param name="securityStamp">The current security stamp of the parent user.</param>
    /// <param name="childIsReady">Whether the child tenant is ready and belongs to the parent.</param>
    /// <param name="grantedRoles">The child roles the grants give the user now.</param>
    public static string GetEndReason(
        DelegatedAccessSession session,
        ParentTenantPolicy policy,
        DateTime utcNow,
        bool userIsEnabled,
        string securityStamp,
        bool childIsReady,
        IReadOnlyCollection<string> grantedRoles)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(policy);

        if (session.EndedUtc.HasValue)
        {
            return Ended;
        }

        if (policy.SessionIdleTimeout > TimeSpan.Zero && utcNow - session.LastSeenUtc > policy.SessionIdleTimeout)
        {
            return IdleTimeout;
        }

        if (policy.SessionLifetime > TimeSpan.Zero && utcNow - session.CreatedUtc > policy.SessionLifetime)
        {
            return LifetimeExceeded;
        }

        if (!userIsEnabled)
        {
            return UserDisabled;
        }

        if (!string.Equals(session.SecurityStamp, securityStamp, StringComparison.Ordinal))
        {
            return SecurityStampChanged;
        }

        if (!childIsReady)
        {
            return ChildUnavailable;
        }

        if (grantedRoles is null || grantedRoles.Count == 0)
        {
            return GrantRemoved;
        }

        return null;
    }
}
