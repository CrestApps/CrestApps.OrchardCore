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

    public DateTime? Starts { get; set; }

    public DateTime? Ends { get; set; }

    public EnrollmentMode EnrollmentMode { get; set; }

    public string NewEnrollmentKey { get; set; }

    public bool ClearEnrollmentKey { get; set; }

    public int? AccessDays { get; set; }

    public decimal? PassMark { get; set; }

    public decimal? MinimumGrade { get; set; }

    public int? MinimumMinutes { get; set; }

    public bool RequireStaffApproval { get; set; }

    public string ApprovalRole { get; set; }

    public bool NeverCompletes { get; set; }

    public string FinalItemId { get; set; }

    public string[] PrerequisiteCourseIds { get; set; } = [];

    public string[] InstructorIds { get; set; } = [];

    public string[] AssistantIds { get; set; } = [];

    public string[] GraderIds { get; set; } = [];

    public string[] ObserverIds { get; set; } = [];

    public List<GradeCategoryRowViewModel> GradeCategories { get; set; } = [];

    [BindNever]
    public bool HasEnrollmentKey { get; set; }

    [BindNever]
    public bool CanChangeEnrollmentSettings { get; set; }

    [BindNever]
    public IList<SelectListItem> ItemOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> ApprovalRoleOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> CourseOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> EnrollmentModeOptions { get; set; } = [];
}

/// <summary>
/// A gradebook category row of the course editor.
/// </summary>
public class GradeCategoryRowViewModel
{
    /// <summary>Gets or sets the id; empty for a new category, which gets an id when the course is saved.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the weight in the course grade.</summary>
    public decimal? Weight { get; set; }

    /// <summary>Gets or sets how many of the lowest scores are dropped.</summary>
    public int? DropLowest { get; set; }
}

/// <summary>
/// The model of the gradebook category row partial.
/// </summary>
public class GradeCategoryRowModel
{
    /// <summary>Gets or sets the prefix of the field names.</summary>
    public string Prefix { get; set; }

    /// <summary>Gets or sets the index of the row.</summary>
    public string Index { get; set; }

    /// <summary>Gets or sets the category.</summary>
    public GradeCategoryRowViewModel Category { get; set; }
}
