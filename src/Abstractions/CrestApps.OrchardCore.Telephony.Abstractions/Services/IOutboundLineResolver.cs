using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Resolves the line a user dials out from, so the calls they place present that line's number as the caller ID.
/// </summary>
/// <remarks>
/// Telephony registers a resolver that assigns no lines, so every call presents the provider's default caller ID.
/// A feature that manages the tenant's numbers replaces it to give each user their own line. The caller ID of a
/// soft-phone call is always taken from here, never from the browser, so an agent cannot present a number that
/// was not assigned to them.
/// </remarks>
public interface IOutboundLineResolver
{
    /// <summary>
    /// Resolves the line assigned to a user.
    /// </summary>
    /// <param name="userId">The identifier of the user placing the call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The user's line, or <see langword="null"/> when they have none and the provider default applies.</returns>
    Task<OutboundLine> ResolveAsync(string userId, CancellationToken cancellationToken = default);
}
