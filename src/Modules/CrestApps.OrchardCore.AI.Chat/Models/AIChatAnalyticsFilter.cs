using CrestApps.OrchardCore.AI.Models;
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
    /// Gets or sets the start date for the report range.
    /// </summary>
    public DateTime? StartDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the end date for the report range.
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
