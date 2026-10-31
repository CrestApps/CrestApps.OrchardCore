using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Skills.ViewModels;

/// <summary>
/// A list page of the Skills module.
/// </summary>
public class SkillsListViewModel
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the certification filter.
    /// </summary>
    public string CertificationId { get; set; }

    /// <summary>
    /// Gets or sets the status filter.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets whether only certificates expiring in the next 60 days are listed.
    /// </summary>
    public bool Expiring { get; set; }

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets the certifications to filter by.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> CertificationOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the statuses to filter by.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StatusOptions { get; set; } = [];
}
