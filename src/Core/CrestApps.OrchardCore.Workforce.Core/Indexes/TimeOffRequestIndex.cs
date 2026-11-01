using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of time-off requests.
/// </summary>
public sealed class TimeOffRequestIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the team the request belongs to.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the time-off type.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the policy.
    /// </summary>
    public string PolicyId { get; set; }

    /// <summary>
    /// Gets or sets where the request stands.
    /// </summary>
    public TimeOffRequestStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the first day, as yyyyMMdd.
    /// </summary>
    public int StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day, as yyyyMMdd.
    /// </summary>
    public int EndDate { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the request was submitted.
    /// </summary>
    public DateTime SubmittedUtc { get; set; }
}
