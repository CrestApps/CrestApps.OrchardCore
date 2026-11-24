using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The enrollment list with its filters.
/// </summary>
public class EnrollmentListViewModel
{
    public string Search { get; set; }

    public string CourseId { get; set; }

    public string Status { get; set; }

    [BindNever]
    public bool CanManage { get; set; }

    [BindNever]
    public IList<SelectListItem> CourseOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> StatusOptions { get; set; } = [];

    [BindNever]
    public AdminListContext List { get; set; }
}
