using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The editor of the course part.
/// </summary>
public class CoursePartViewModel
{
    public string Code { get; set; }

    public string Summary { get; set; }

    public string Level { get; set; }

    public int? EstimatedMinutes { get; set; }

    public EnrollmentMode EnrollmentMode { get; set; }

    public string NewEnrollmentKey { get; set; }

    public bool ClearEnrollmentKey { get; set; }

    public int? AccessDays { get; set; }

    public decimal? PassMark { get; set; }

    public decimal? MinimumGrade { get; set; }

    public int? MinimumMinutes { get; set; }

    public bool RequireStaffApproval { get; set; }

    public string FinalItemId { get; set; }

    public string[] PrerequisiteCourseIds { get; set; } = [];

    public string[] InstructorIds { get; set; } = [];

    public string[] AssistantIds { get; set; } = [];

    public string[] GraderIds { get; set; } = [];

    public string[] ObserverIds { get; set; } = [];

    [BindNever]
    public bool HasEnrollmentKey { get; set; }

    [BindNever]
    public IList<SelectListItem> ItemOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> CourseOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> EnrollmentModeOptions { get; set; } = [];
}
