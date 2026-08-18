using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Wizard.ViewModels;

/// <summary>
/// Represents a single authored step item together with the current user's access to it.
/// </summary>
public class WizardPartWidgetViewModel
{
    /// <summary>
    /// Gets or sets the authored step content item.
    /// </summary>
    public ContentItem ContentItem { get; set; }

    /// <summary>
    /// Gets or sets the content type definition of the step item.
    /// </summary>
    public ContentTypeDefinition ContentTypeDefinition { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current user may edit the step item.
    /// </summary>
    public bool Editable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current user may view the step item.
    /// </summary>
    public bool Viewable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current user may delete the step item.
    /// </summary>
    public bool Deletable { get; set; }
}
