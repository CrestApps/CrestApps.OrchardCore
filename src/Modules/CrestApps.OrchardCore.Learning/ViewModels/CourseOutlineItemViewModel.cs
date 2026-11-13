using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// An item row of the outline editor.
/// </summary>
public class CourseOutlineItemViewModel
{
    public string ContentItemId { get; set; }

    public string SectionId { get; set; }

    public int Order { get; set; }

    public bool Required { get; set; } = true;

    [BindNever]
    public string Title { get; set; }

    [BindNever]
    public string TypeDisplayName { get; set; }

    [BindNever]
    public bool Published { get; set; }
}
