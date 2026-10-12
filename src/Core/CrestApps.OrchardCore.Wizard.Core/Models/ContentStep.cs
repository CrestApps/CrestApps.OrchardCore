using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Wizard.Core.Models;

/// <summary>
/// The saved state of a content-driven wizard step. It stores the per-session response content items that a
/// visitor filled in for the step, separate from the authored step-definition items.
/// </summary>
public sealed class ContentStep
{
    /// <summary>
    /// Gets or sets the response content items collected for the step.
    /// </summary>
    public List<ContentItem> ContentItems { get; set; } = [];
}
