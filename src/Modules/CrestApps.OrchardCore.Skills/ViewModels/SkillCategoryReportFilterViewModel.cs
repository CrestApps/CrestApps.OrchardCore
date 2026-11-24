using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Skills.ViewModels;

/// <summary>
/// The skill category (competency framework) filter of a report.
/// </summary>
public class SkillCategoryReportFilterViewModel
{
    /// <summary>
    /// Gets or sets the chosen category, or <see langword="null"/> for every category.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the categories to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Categories { get; set; } = [];
}
