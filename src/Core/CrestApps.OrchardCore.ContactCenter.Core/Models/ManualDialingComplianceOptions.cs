namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the compliance policy applied to manual, agent-initiated outbound calls placed through the
/// soft phone. Manual dialing is treated separately from automated campaign dialing because the two are
/// governed differently, but a manual call must still be screened before it is placed. It is bound from
/// the <c>CrestApps_ContactCenter:Compliance:ManualDialing</c> configuration section.
/// </summary>
public sealed class ManualDialingComplianceOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether manual calls are screened against do-not-call — the
    /// contact's opt-out preference and any registered national do-not-call registry. Enabled by default.
    /// </summary>
    public bool RespectDoNotCall { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether manual calls are restricted to a calling window. Disabled
    /// by default because it requires a configured calling calendar.
    /// </summary>
    public bool EnforceCallingWindow { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the business-hours calendar that defines the permitted manual calling
    /// window. Only used when <see cref="EnforceCallingWindow"/> is enabled.
    /// </summary>
    public string CallingCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 region code used to canonicalize a destination that does not
    /// carry an international prefix. When empty, only destinations already in international form can be
    /// screened.
    /// </summary>
    public string DefaultRegionCode { get; set; }
}
