using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.DataSources.ViewModels;

/// <summary>
/// View model for the shared AI data source destination and field mapping editor fields.
/// </summary>
public class EditAIDataSourceSharedViewModel
{
    /// <summary>
    /// Gets or sets the selected source type.
    /// </summary>
    public string SourceType { get; set; }

    /// <summary>
    /// Gets or sets the AI knowledge base index profile name.
    /// </summary>
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
    public string ContentFieldName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the index configuration is locked.
    /// </summary>
    [BindNever]
    public bool IsConfigurationLocked { get; set; }

    /// <summary>
    /// Gets or sets the AI knowledge base index profile names.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AIKnowledgeBaseIndexProfileNames { get; set; }

    /// <summary>
    /// Gets or sets the available source field names.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> FieldNames { get; set; }
}
