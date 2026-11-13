using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The editor of the course item part.
/// </summary>
public class CourseItemPartViewModel
{
    public string CompletionMode { get; set; }

    public int? MinimumMinutes { get; set; }

    public int? EstimatedMinutes { get; set; }

    public bool Gradable { get; set; }

    public decimal Weight { get; set; } = 1;

    public decimal? PassMark { get; set; }

    public bool FreePreview { get; set; }

    public bool AllowSelfReported { get; set; }

    public string AfterItemId { get; set; }

    public decimal? AfterMinimumScore { get; set; }

    public DateTime? OpensFrom { get; set; }

    public int? OpensDaysAfterEnrollment { get; set; }

    [BindNever]
    public string KindDisplayName { get; set; }

    [BindNever]
    public bool ProducesGrade { get; set; }

    [BindNever]
    public IList<SelectListItem> CompletionModeOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> ItemOptions { get; set; } = [];
}
