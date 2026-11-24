namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// A section row of the outline editor. A row whose title is cleared is removed.
/// </summary>
public class CourseOutlineSectionViewModel
{
    public string Id { get; set; }

    public string Title { get; set; }

    public string Summary { get; set; }

    public int Order { get; set; }

    public int? OpensDaysAfterEnrollment { get; set; }
}
