using CrestApps.Core.AI.Models;
using OrchardCore.Entities;
using YesSql;

namespace CrestApps.OrchardCore.AI.Chat.Models;

/// <summary>
/// Filter model for chat analytics reports.
/// Display drivers can add conditions to the query during the UpdateAsync phase.
/// </summary>
public sealed class AIChatAnalyticsFilter : Entity
{
    /// <summary>
    /// Gets or sets the start date in local time as entered by the user.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date in local time as entered by the user.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the start date converted to UTC for querying.
    /// </summary>
    public DateTime? StartDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the end date converted to UTC for querying.
    /// </summary>
    public DateTime? EndDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the profile ID to filter by.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the list of query conditions accumulated by display drivers.
    /// Each condition modifies the base query to add additional filters.
    /// </summary>
    public List<Func<IQuery<AIChatSessionEvent>, IQuery<AIChatSessionEvent>>> Conditions { get; set; } = [];
}
