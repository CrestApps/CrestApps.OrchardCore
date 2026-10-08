using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the management contract for call sessions.
/// </summary>
public interface ICallSessionManager : ICatalogManager<CallSession>
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
}
