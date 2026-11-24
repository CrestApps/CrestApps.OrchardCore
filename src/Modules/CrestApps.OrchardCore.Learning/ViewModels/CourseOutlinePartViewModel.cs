using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The editor of the course outline: the sections, then each item of the course with its section, order and whether it
/// is required.
/// </summary>
public class CourseOutlinePartViewModel
{
    public IList<CourseOutlineSectionViewModel> Sections { get; set; } = [];

    public IList<CourseOutlineItemViewModel> Items { get; set; } = [];

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public string CourseId { get; set; }
}
