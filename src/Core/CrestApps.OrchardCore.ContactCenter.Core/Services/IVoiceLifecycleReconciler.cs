namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Revalidates active provider-backed interactions against the telephony server.
/// </summary>
/// <remarks>
/// A seam rather than a direct dependency, because the thing that knows how to ask a provider what a
/// call is doing lives with the provider wiring, and the sweep that asks it periodically does not.
/// </remarks>
public interface IVoiceLifecycleReconciler
{
    /// <summary>
    /// Brings the recorded state of active interactions back in line with the provider's.
    /// </summary>
    /// <remarks>
    /// Needed because a restart or a missed live event leaves queued voice work claiming a state the
    /// provider has already moved on from, and nothing else notices.
    /// </remarks>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ReconcileProviderStateAsync(CancellationToken cancellationToken = default);
}
