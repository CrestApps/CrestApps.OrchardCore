namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Brings Contact Center agent state into line when a user's session ends.
/// </summary>
/// <remarks>
/// Whatever ends the session - an explicit log off, a programmatic or front-channel sign-out, or a
/// security-stamp rejection - the agent's presence has to become signed-out and their soft-phone
/// credentials have to be revoked, or the agent stays falsely available and their browser keeps live
/// SIP credentials.
/// <para>
/// The contract takes a user id rather than a <see cref="System.Security.Claims.ClaimsPrincipal"/>
/// because one of the two paths that needs it has no principal left: a security-stamp rejection
/// nulls the principal inside cookie authentication, and only the id captured before the rejection
/// survives.
/// </para>
/// <para>
/// An implementation must not propagate. It runs from inside the host's sign-out pipeline, before the
/// authentication cookie is deleted, so an exception escaping would abort the sign-out and leave the
/// user logged in. Failing to synchronise is recoverable; failing to sign out is not.
/// </para>
/// <para>
/// There is deliberately no cancellation token. The caller's token is the request's, and a client
/// that disconnects mid-sign-out is exactly when this work must still happen; an implementation
/// bounds itself instead.
/// </para>
/// </remarks>
public interface IAgentSignOutHandler
{
    /// <summary>
    /// Signs the agent out of presence and revokes their soft-phone credentials.
    /// </summary>
    /// <param name="userId">The user whose session ended. Ignored when empty.</param>
    Task HandleAsync(string userId);
}
