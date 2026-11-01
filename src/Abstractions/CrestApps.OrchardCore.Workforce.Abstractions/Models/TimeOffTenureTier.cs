namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A rate that applies once a worker has been employed for a number of months.
/// </summary>
public sealed class TimeOffTenureTier
{
    /// <summary>
    /// Gets or sets the months of service the rate starts at.
    /// </summary>
    public int FromMonths { get; set; }

    /// <summary>
    /// Gets or sets the rate, in the same terms as the policy's rate.
    /// </summary>
    public decimal Rate { get; set; }
}
