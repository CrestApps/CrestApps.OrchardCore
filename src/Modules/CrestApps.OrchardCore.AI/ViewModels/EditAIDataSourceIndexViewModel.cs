using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// View model for AI data source index selection and field mapping.
/// </summary>
public class EditAIDataSourceIndexViewModel
{
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
