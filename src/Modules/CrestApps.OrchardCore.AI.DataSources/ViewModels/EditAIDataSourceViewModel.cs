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
    /// Gets or sets the source index profile name.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string SourceIndexProfileName { get; set; }

    /// <summary>
    /// Gets or sets the AI knowledge base index profile name.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string AIKnowledgeBaseIndexProfileName { get; set; }

    /// <summary>
    /// Gets or sets the key field name.
    /// </summary>
    public string KeyFieldName { get; set; }

    /// <summary>
    /// Gets or sets the title field name.
    /// </summary>
    public string TitleFieldName { get; set; }

    /// <summary>
    /// Gets or sets the content field name.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ContentFieldName { get; set; }

    /// <summary>
    /// Whether the index configuration is locked (already created and cannot be changed).
    /// </summary>
    [BindNever]
    public bool IsLocked { get; set; }

    /// <summary>
    /// Gets or sets the source index profile names.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SourceIndexProfileNames { get; set; }

    /// <summary>
    /// Gets or sets the AI knowledge base index profile names.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AIKnowledgeBaseIndexProfileNames { get; set; }

    /// <summary>
    /// Gets or sets the field names.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> FieldNames { get; set; }
}
