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

    /// <summary>
    /// Gets or sets a value indicating whether the attempt being evaluated is already reflected in the
    /// activity's attempt counter. This is <see langword="true"/> when compliance is re-validated at dispatch
    /// time, after the dial attempt was recorded but before it reaches the provider: the maximum-attempts gate
    /// must then exclude the in-flight attempt so a profile that allows a single attempt still places its one
    /// call. It is <see langword="false"/> for the pre-attempt check, which decides whether a new attempt may
    /// start and so counts the attempt about to be made.
    /// </summary>
    public bool AttemptAlreadyCounted { get; set; }
}
