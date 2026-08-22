using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Carries the data the outbound compliance gate needs to evaluate whether an activity may be dialed.
/// </summary>
public sealed class DialerEligibilityContext
{
    /// <summary>
    /// Gets or sets the dialer profile that governs the attempt.
    /// </summary>
    public DialerProfile Profile { get; set; }

    /// <summary>
    /// Gets or sets the CRM activity being considered for dialing.
    /// </summary>
    public OmnichannelActivity Activity { get; set; }
}
