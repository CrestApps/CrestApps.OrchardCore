using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.DataSources.ViewModels;

/// <summary>
/// View model for editing all first-class properties of an AI data source.
/// </summary>
public class EditAIDataSourceViewModel
{
    public string DisplayText { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string SourceIndexProfileName { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string AIKnowledgeBaseIndexProfileName { get; set; }

    public string KeyFieldName { get; set; }

    public string TitleFieldName { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string ContentFieldName { get; set; }

    /// <summary>
    /// Whether the index configuration is locked (already created and cannot be changed).
    /// </summary>
    [BindNever]
    public bool IsLocked { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> SourceIndexProfileNames { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> AIKnowledgeBaseIndexProfileNames { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> FieldNames { get; set; }
}
