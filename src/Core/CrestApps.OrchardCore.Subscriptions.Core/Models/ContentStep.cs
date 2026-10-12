using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Stores the content items collected by a content step in a subscription flow.
/// </summary>
public sealed class ContentStep
{
    /// <summary>
    /// Gets or sets the content items captured for the step.
    /// </summary>
    public List<ContentItem> ContentItems { get; set; }
}
