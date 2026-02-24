using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Models;

/// <summary>
/// Context object passed to analytics report display drivers.
/// Contains the filtered list of events for rendering report sections.
/// </summary>
public sealed class AIChatAnalyticsReport : Entity
{
    /// <summary>
    /// Gets or sets the filtered events for the report.
    /// </summary>
    public IReadOnlyList<AIChatSessionEvent> Events { get; set; } = [];

    /// <summary>
    /// Gets or sets the filter that was applied.
    /// </summary>
    public AIChatAnalyticsFilter Filter { get; set; }
}
