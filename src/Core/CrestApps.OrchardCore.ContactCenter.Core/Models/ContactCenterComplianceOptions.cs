namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the tenant-level compliance configuration for outbound dialing. It is bound from the
/// <c>CrestApps_ContactCenter:Compliance</c> configuration section and validated on start.
/// </summary>
public sealed class ContactCenterComplianceOptions
{
    /// <summary>
    /// Gets or sets the length, in minutes, of the rolling window used to measure the outbound abandonment
    /// rate. Must be between 1 and 1440 minutes.
    /// </summary>
    public int AbandonmentRollingWindowMinutes { get; set; } = 30;
}
