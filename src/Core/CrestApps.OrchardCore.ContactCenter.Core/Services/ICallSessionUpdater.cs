using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Changes a call's session (and its interaction) on a fresh read, committed on its own, retrying when another writer got
/// there first.
/// </summary>
/// <remarks>
/// A supervisor's request reads the call before it asks the provider for something, and the provider's answer comes back
/// as webhooks that write the same call while the request is still running. Writing the copy the request read first then
/// fails on commit: live, stopping an engagement answered 500 with a ConcurrencyException because the call.bridged of its
/// own restored bridge had updated the call in between. Each change is therefore applied to a copy read in a scope of its
/// own and committed there.
/// </remarks>
public interface ICallSessionUpdater
{
    /// <summary>
    /// Applies <paramref name="mutate"/> to the interaction's call session and saves it when it returns
    /// <see langword="true"/>.
    /// </summary>
    /// <param name="interactionId">The interaction whose call session changes.</param>
    /// <param name="mutate">The change; returns whether anything changed. It may run more than once.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a change was saved.</returns>
    Task<bool> UpdateAsync(string interactionId, Func<CallSession, bool> mutate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies <paramref name="mutate"/> to the interaction and its call session together and saves both when it returns
    /// <see langword="true"/>.
    /// </summary>
    /// <param name="interactionId">The interaction.</param>
    /// <param name="mutate">The change; returns whether anything changed. It may run more than once.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a change was saved.</returns>
    Task<bool> UpdateWithInteractionAsync(string interactionId, Func<CallSession, Interaction, bool> mutate, CancellationToken cancellationToken = default);
}
