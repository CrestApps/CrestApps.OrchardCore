using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for call sessions.
/// </summary>
public interface ICallSessionStore : ICatalog<CallSession>
{
    /// <summary>
    /// Finds the call session with the specified provider call identifier.
    /// </summary>
    /// <param name="providerCallId">The provider call identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching call session, or <see langword="null"/> when none is found.</returns>
    Task<CallSession> FindByProviderCallIdAsync(string providerCallId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the call session with the specified provider and provider call identifier.
    /// </summary>
    /// <param name="providerName">The provider technical name.</param>
    /// <param name="providerCallId">The provider call identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching call session, or <see langword="null"/> when none is found.</returns>
    Task<CallSession> FindByProviderCallIdAsync(string providerName, string providerCallId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the most recent call session linked to the specified interaction.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching call session, or <see langword="null"/> when none is found.</returns>
    Task<CallSession> FindByInteractionIdAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the call sessions that have not yet ended, using an aggregate query without materializing the
    /// rows. A session is active while it has no recorded end time, so this is the number of live calls the
    /// tenant is currently handling.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of active call sessions.</returns>
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);
}
