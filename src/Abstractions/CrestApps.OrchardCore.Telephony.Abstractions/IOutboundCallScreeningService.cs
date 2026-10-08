using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Evaluates every registered <see cref="IOutboundCallScreener"/> at the shared telephony boundary and
/// aggregates their verdicts. It is the single gate every outbound origination passes through, so a
/// compliance policy attached as a screener applies to all origination paths, not only the campaign
/// dialer. When no screener is registered the origination is permitted.
/// </summary>
public interface IOutboundCallScreeningService
{
    /// <summary>
    /// Screens the supplied origination against every registered screener.
    /// </summary>
    /// <param name="context">The origination to screen.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The first denial, or an allowing result when every screener permits the origination.</returns>
    Task<OutboundCallScreeningResult> ScreenAsync(OutboundCallScreeningContext context, CancellationToken cancellationToken = default);
}
