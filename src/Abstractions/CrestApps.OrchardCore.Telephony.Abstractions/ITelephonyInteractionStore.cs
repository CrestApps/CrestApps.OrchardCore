using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Persists and queries telephony interactions for local history and reporting.
/// </summary>
public interface ITelephonyInteractionStore
{
    /// <summary>
    /// Creates a new interaction.
    /// </summary>
    /// <param name="interaction">The interaction to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CreateAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing interaction.
    /// </summary>
    /// <param name="interaction">The interaction to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpdateAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the interaction for the given user and provider call identifier.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="callId">The provider-specific call identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The interaction, or <see langword="null"/> when none matches.</returns>
    Task<TelephonyInteraction> FindByCallIdAsync(string userId, string callId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the interaction for the given provider and provider call identifier, regardless of the
    /// current user's connection state.
    /// </summary>
    /// <param name="providerName">The technical provider name.</param>
    /// <param name="callId">The provider-specific call identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The interaction, or <see langword="null"/> when none matches.</returns>
    Task<TelephonyInteraction> FindByProviderCallIdAsync(string providerName, string callId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent interactions for the given user, newest first.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="count">The maximum number of interactions to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The most recent interactions.</returns>
    Task<IReadOnlyList<TelephonyInteraction>> GetRecentAsync(string userId, int count, CancellationToken cancellationToken = default);
}
