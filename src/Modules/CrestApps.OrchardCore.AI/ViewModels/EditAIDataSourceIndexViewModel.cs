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
    public string IndexName { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string MasterIndexName { get; set; }

    public string TitleFieldName { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string ContentFieldName { get; set; }

    /// <summary>
    /// Whether the index configuration is locked (already created and cannot be changed).
    /// </summary>
    [BindNever]
    public bool IsLocked { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> IndexNames { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> MasterIndexNames { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> FieldNames { get; set; }
}
