using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Wizard.Core.Models;

/// <summary>
/// A content part that turns a content item into a wizard definition. Each contained content item is one
/// authored step. When a visitor starts the wizard, the host clones these step-definition items into a
/// per-session response so authored content is never mutated by a running wizard.
/// </summary>
public sealed class WizardPart : ContentPart
{
    /// <summary>
    /// Gets or sets the ordered step-definition content items that make up the wizard.
    /// </summary>
    [BindNever]
    public List<ContentItem> Steps { get; set; } = [];
}
