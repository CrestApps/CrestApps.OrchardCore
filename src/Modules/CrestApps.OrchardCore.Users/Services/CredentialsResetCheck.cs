namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// What the sign-in cookie's validation does with a session, given the account's credentials resets and claims
/// refreshes (see <see cref="UserCredentialsResets"/>).
/// </summary>
public enum CredentialsResetCheck
{
    /// <summary>
    /// Nothing: the session is left to Identity's own security stamp check.
    /// </summary>
    None,

    /// <summary>
    /// The session started before the account's earlier credentials were reset, so it is refused.
    /// </summary>
    Refuse,

    /// <summary>
    /// The session started before the account was marked in a way its claims carry, so it is refreshed now, as
    /// Identity's security stamp check would at its next run.
    /// </summary>
    Refresh,
}
