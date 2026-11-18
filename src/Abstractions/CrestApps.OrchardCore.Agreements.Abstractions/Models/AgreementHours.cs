namespace CrestApps.OrchardCore.Agreements.Models;

/// <summary>
/// The hours of work an agreement covers.
/// </summary>
public sealed class AgreementHours
{
    /// <summary>
    /// Gets or sets how it covers hours.
    /// </summary>
    public AgreementHoursKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the hours of the block, or of each month's allowance.
    /// </summary>
    public decimal Hours { get; set; }

    /// <summary>
    /// Gets or sets what each entry of time is rounded up to, in minutes, one of
    /// <see cref="AgreementsConstants.RoundingMinutes"/>; 0 counts it as it is.
    /// </summary>
    public int RoundingMinutes { get; set; }
}
