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
    /// Updates an existing interaction. The write is guarded by an optimistic-concurrency check, so a
    /// caller that mutated a stale copy fails loudly instead of silently discarding a concurrent update.
    /// </summary>
    /// <param name="interaction">The interaction to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpdateAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a mutation to the interaction with the given identifier inside a dedicated session, re-reading
    /// and reapplying the mutation whenever a concurrent writer commits first.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="mutate">
    /// The mutation to apply to the freshly read interaction. Returning <see langword="false"/> abandons the
    /// attempt without writing, which lets a caller decline based on state it can only observe after the read.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The interaction as it was read and mutated, or <see langword="null"/> when no interaction matches.
    /// </returns>
    Task<TelephonyInteraction> UpdateByIdAsync(
        string interactionId,
        Func<TelephonyInteraction, bool> mutate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a mutation to the interaction for the given provider and provider call identifier inside a
    /// dedicated session, re-reading and reapplying the mutation whenever a concurrent writer commits first.
    /// </summary>
    /// <param name="providerName">The technical provider name.</param>
    /// <param name="callId">The provider-specific call identifier.</param>
    /// <param name="mutate">
    /// The mutation to apply to the freshly read interaction. Returning <see langword="false"/> abandons the
    /// attempt without writing, which lets a caller decline based on state it can only observe after the read.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The interaction as it was read and mutated, or <see langword="null"/> when no interaction matches.
    /// </returns>
    Task<TelephonyInteraction> UpdateByProviderCallIdAsync(
        string providerName,
        string callId,
        Func<TelephonyInteraction, bool> mutate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an interaction that no longer exists at the telephony provider.
    /// </summary>
    /// <param name="interaction">The interaction to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeleteAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default);

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
    /// Finds the most recent in-progress interaction for the given user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The active interaction, or <see langword="null"/> when none matches.</returns>
    Task<TelephonyInteraction> FindActiveByUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all in-progress interactions for the given user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user's active interactions, newest first.</returns>
    Task<IReadOnlyList<TelephonyInteraction>> GetActiveByUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists in-progress interactions that can be reconciled against their providers, oldest first and bounded for reconciliation sweeps.
    /// </summary>
    /// <param name="maxCount">The maximum number of interactions to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The oldest active interactions bounded by <paramref name="maxCount"/>.</returns>
    Task<IReadOnlyList<TelephonyInteraction>> GetActiveAsync(int maxCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists in-progress interactions for the specified provider, oldest first and bounded for reconciliation sweeps.
    /// </summary>
    /// <param name="providerName">The technical provider name.</param>
    /// <param name="maxCount">The maximum number of interactions to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The oldest active interactions for the provider bounded by <paramref name="maxCount"/>.</returns>
    Task<IReadOnlyList<TelephonyInteraction>> GetActiveAsync(string providerName, int maxCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent interactions for the given user, newest first.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="count">The maximum number of interactions to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The most recent interactions.</returns>
    Task<IReadOnlyList<TelephonyInteraction>> GetRecentAsync(string userId, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the given user's unread voicemails (voicemail interactions that have not yet been listened to).
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of unread voicemails.</returns>
    Task<int> GetUnreadVoicemailCountAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the voicemail identified by its provider call id as read for the given user. Marking an already-read
    /// (or non-voicemail) interaction is a no-op.
    /// </summary>
    /// <param name="userId">The user identifier that owns the voicemail.</param>
    /// <param name="callId">The provider call id of the voicemail interaction.</param>
    /// <param name="readUtc">The time, in UTC, the voicemail was read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated interaction, or <see langword="null"/> when no matching interaction exists.</returns>
    Task<TelephonyInteraction> MarkVoicemailReadAsync(string userId, string callId, DateTime readUtc, CancellationToken cancellationToken = default);
}
