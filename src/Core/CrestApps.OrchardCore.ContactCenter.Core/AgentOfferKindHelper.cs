using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core;

/// <summary>
/// Classifies a work offer into an <see cref="AgentOfferKind"/> from the offered activity's source, so both the
/// real-time offer broadcast and the agent workspace poll agree on the call-to-action the agent should see.
/// </summary>
public static class AgentOfferKindHelper
{
    /// <summary>
    /// Resolves the offer kind for the supplied activity source.
    /// </summary>
    /// <param name="activitySource">The offered activity's source identifier.</param>
    /// <returns>
    /// <see cref="AgentOfferKind.PreviewDial"/> for a preview dial, <see cref="AgentOfferKind.AutoDial"/> for a
    /// system-paced dial, and <see cref="AgentOfferKind.InboundCall"/> for anything else (an inbound call).
    /// </returns>
    public static AgentOfferKind FromActivitySource(string activitySource)
    {
        if (string.Equals(activitySource, ActivitySources.PreviewDial, StringComparison.OrdinalIgnoreCase))
        {
            return AgentOfferKind.PreviewDial;
        }

        // The remaining dialer sources (power, progressive, predictive, and the generic dialer pool) are
        // system-paced: the dialer places and connects the call, so the agent only needs the record popped.
        if (DialerActivitySourceHelper.IsDialerSource(activitySource))
        {
            return AgentOfferKind.AutoDial;
        }

        return AgentOfferKind.InboundCall;
    }
}
