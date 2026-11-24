using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The form that enrolls a learner by hand.
/// </summary>
public class EnrollViewModel
{
    public string UserId { get; set; }

    public string CourseId { get; set; }

    public DateTime? AccessEnds { get; set; }

    public DateTime? Due { get; set; }

    public bool IgnorePrerequisites { get; set; }

    [BindNever]
    public IList<SelectListItem> CourseOptions { get; set; } = [];
}
