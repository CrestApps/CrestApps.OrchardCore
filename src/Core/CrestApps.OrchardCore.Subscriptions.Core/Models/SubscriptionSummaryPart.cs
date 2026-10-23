using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// The part attached to the <c>SubscriptionSummaryWidget</c> dashboard widget.
/// Its display driver computes and renders subscription summary statistics, and keeps them for a while.
/// </summary>
public sealed class SubscriptionSummaryPart : ContentPart
{
    /// <summary>
    /// Gets or sets how many minutes the widget keeps its figures before it counts the subscriptions again; 0 counts them
    /// on every visit to the dashboard.
    /// </summary>
    public int CacheMinutes { get; set; } = 30;
}
