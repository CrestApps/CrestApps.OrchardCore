namespace CrestApps.Core.ContactCenter.Models;

/// <summary>
/// Represents the tenant-level compliance configuration for outbound dialing. It is bound from the
/// <c>CrestApps:ContactCenter:Compliance</c> configuration section and validated on start.
/// </summary>
public sealed class ContactCenterComplianceOptions
{
    /// <summary>
    /// Gets or sets the length, in minutes, of the rolling window used to measure the outbound abandonment
    /// rate. Must be between 1 and 1440 minutes.
    /// </summary>
    public int AbandonmentRollingWindowMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether an outbound call is refused when no national do-not-call
    /// registry is configured.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With no registry registered there is nothing to ask, and the screening step has no answer. It
    /// currently reads that as "not listed" and the call proceeds, which is a deployment dialing with no
    /// suppression check at all and no way to say that is unacceptable.
    /// </para>
    /// <para>
    /// The default is <see langword="false"/>, which keeps the behaviour every existing deployment already
    /// has. Turning it on is a deliberate choice by an operator who would rather place no outbound call than
    /// place an unscreened one - and it stops outbound dialing entirely until a registry is configured, which
    /// is the point of it.
    /// </para>
    /// </remarks>
    public bool FailClosedWithoutNationalRegistry { get; set; }
}
