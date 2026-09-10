using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Screens an outbound origination before it is dispatched to a telephony provider. Implementations are
/// discovered as a collection so any module can contribute a compliance policy (for example, do-not-call
/// or calling-window enforcement) without the shared telephony boundary depending on it. A screener that
/// denies an origination prevents the call from being placed.
/// </summary>
public interface IOutboundCallScreener
{
    /// <summary>
    /// Screens the supplied origination.
    /// </summary>
    /// <param name="context">The origination to screen.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A result describing whether the origination may proceed and, if not, why.</returns>
    Task<OutboundCallScreeningResult> ScreenAsync(OutboundCallScreeningContext context, CancellationToken cancellationToken = default);
}
