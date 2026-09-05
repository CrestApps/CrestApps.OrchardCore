using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Owns the first-response clock: when it starts, when it stops, and what happens when it runs out. Without one
/// a customer who texts a queue and gets no reply has no way to tell whether they were heard, and neither has a
/// supervisor.
/// </summary>
public interface ISmsFirstResponseSlaService
{
    /// <summary>
    /// Starts the first-response clock for a thread that has just been placed on a queue, when that queue has a
    /// target. An existing deadline is left alone: the clock starts when the customer first waited, and
    /// restarting it on every re-placement would mean a thread bounced between agents never breaches.
    /// </summary>
    /// <param name="conversation">The conversation being placed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ApplyFirstResponseTargetAsync(SmsConversation conversation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the clock because someone has replied.
    /// </summary>
    /// <param name="conversation">The conversation that was replied to.</param>
    void MarkResponded(SmsConversation conversation);

    /// <summary>
    /// Announces every thread whose first-response deadline has passed and that has not been announced already.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of threads announced by this pass.</returns>
    Task<int> EscalateOverdueAsync(CancellationToken cancellationToken = default);
}
