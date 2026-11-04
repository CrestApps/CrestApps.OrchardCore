using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of open shifts.
/// </summary>
public sealed class OpenShiftIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the schedule it came from.
    /// </summary>
    public string PeriodId { get; set; }

    /// <summary>
    /// Gets or sets the shift's key.
    /// </summary>
    public string ShiftKey { get; set; }

    /// <summary>
    /// Gets or sets the day as <c>yyyyMMdd</c>.
    /// </summary>
    public int Date { get; set; }

    /// <summary>
    /// Gets or sets when it starts.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets where it stands.
    /// </summary>
    public OpenShiftStatus Status { get; set; }

    /// <summary>
    /// Gets or sets how it is given.
    /// </summary>
    public OpenShiftClaimPolicy ClaimPolicy { get; set; }

    /// <summary>
    /// Gets or sets when it is awarded by seniority.
    /// </summary>
    public DateTime? ClaimDeadlineUtc { get; set; }
}
