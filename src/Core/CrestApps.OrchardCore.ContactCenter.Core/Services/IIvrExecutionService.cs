using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Runs a caller through an entry point's phone menu.
/// </summary>
public interface IIvrExecutionService
{
    /// <summary>
    /// Answers the caller and plays the first menu, or reports that this entry point has none.
    /// </summary>
    /// <param name="interaction">The caller's interaction.</param>
    /// <param name="flow">The menu on the entry point, when it has one.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IvrStep> StartAsync(Interaction interaction, IvrFlow flow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a key press and returns what the caller should hear or be routed to next.
    /// </summary>
    /// <param name="interaction">The caller's interaction.</param>
    /// <param name="flow">The menu on the entry point.</param>
    /// <param name="digits">What the caller pressed, or <see langword="null"/> when they pressed nothing.</param>
    /// <param name="deliveryId">The provider's identifier for this delivery, so a redelivery is recognised.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IvrStep> HandleDigitsAsync(
        Interaction interaction,
        IvrFlow flow,
        string digits,
        string deliveryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the caller has left the menu without choosing, such as by hanging up, so nothing reported
    /// afterwards moves them.
    /// </summary>
    /// <param name="interaction">The caller's interaction.</param>
    /// <param name="reason">Why they left.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task EndAsync(Interaction interaction, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Answers the caller and speaks the entry point's welcome or closed message. The message is recorded as being
    /// spoken, and committed, before the provider is asked to say it, so the provider's report that it has ended can
    /// never arrive for a call that does not yet say it is waiting on one.
    /// </summary>
    /// <param name="interaction">The caller's interaction.</param>
    /// <param name="text">What to say.</param>
    /// <param name="endCallAfter">Whether the provider ends the call once the message has been said.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the provider accepted the message; <see langword="false"/> when it cannot be said.</returns>
    Task<bool> AnnounceAsync(Interaction interaction, string text, bool endCallAfter, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
