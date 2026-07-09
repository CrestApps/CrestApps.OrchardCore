using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.DataSources.ViewModels;

/// <summary>
/// View model for editing all first-class properties of an AI data source.
/// </summary>
public class EditAIDataSourceViewModel
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the selected source type.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string SourceType { get; set; }

    /// <summary>
    /// Gets or sets the AI knowledge base index profile name.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string AIKnowledgeBaseIndexProfileName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the index configuration is locked (already created and cannot be changed).
    /// </summary>
    [BindNever]
    public bool IsConfigurationLocked { get; set; }

    /// <summary>
    /// Gets or sets the AI knowledge base index profile names.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AIKnowledgeBaseIndexProfileNames { get; set; }
}
