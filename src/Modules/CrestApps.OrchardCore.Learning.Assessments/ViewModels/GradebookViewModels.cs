using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.ViewModels;

/// <summary>
/// The grade scales list.
/// </summary>
public class GradeScaleListViewModel
{
    /// <summary>Gets or sets the scales.</summary>
    [BindNever]
    public AdminListContext<GradeScale> List { get; set; }
}

/// <summary>
/// A row of the grade scales list.
/// </summary>
public class GradeScaleRowViewModel
{
    /// <summary>Gets or sets the scale.</summary>
    public GradeScale Scale { get; set; }
}

/// <summary>
/// The grade scale editor.
/// </summary>
public class GradeScaleEditViewModel
{
    /// <summary>Gets or sets the id; empty for a new scale.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the bands.</summary>
    public IList<GradeBandRowViewModel> Bands { get; set; } = [];

    /// <summary>Gets or sets the editor the display drivers build: its fields in cards.</summary>
    [BindNever]
    public IShape Editor { get; set; }
}

/// <summary>
/// A band row.
/// </summary>
public class GradeBandRowViewModel
{
    /// <summary>Gets or sets the lowest percent.</summary>
    public decimal? MinPercent { get; set; }

    /// <summary>Gets or sets the label.</summary>
    public string Label { get; set; }

    /// <summary>Gets or sets whether it passes.</summary>
    public bool Passing { get; set; }
}

/// <summary>
/// The gradebook of a course.
/// </summary>
public class GradebookViewModel
{
    /// <summary>Gets or sets the course.</summary>
    public string CourseId { get; set; }

    /// <summary>Gets or sets the scale shown.</summary>
    public string ScaleId { get; set; }

    /// <summary>Gets or sets the course title.</summary>
    [BindNever]
    public string CourseTitle { get; set; }

    /// <summary>Gets or sets the courses to choose from.</summary>
    [BindNever]
    public IList<SelectListItem> CourseOptions { get; set; } = [];

    /// <summary>Gets or sets the scales to choose from.</summary>
    [BindNever]
    public IList<SelectListItem> ScaleOptions { get; set; } = [];

    /// <summary>Gets or sets the scale shown.</summary>
    [BindNever]
    public GradeScale Scale { get; set; }

    /// <summary>Gets or sets the graded items, in outline order.</summary>
    [BindNever]
    public IList<(string Id, string Title, decimal Weight)> Items { get; set; } = [];

    /// <summary>Gets or sets the gradebook categories, with the items without a category last; empty when the course has none.</summary>
    [BindNever]
    public IReadOnlyList<GradeCategoryResult> Categories { get; set; } = [];

    /// <summary>Gets or sets one row a learner.</summary>
    [BindNever]
    public IList<GradebookRowViewModel> Rows { get; set; } = [];

    /// <summary>Gets or sets whether only the first enrollments are shown.</summary>
    [BindNever]
    public bool Truncated { get; set; }
}

/// <summary>
/// A learner in the gradebook.
/// </summary>
public class GradebookRowViewModel
{
    /// <summary>Gets or sets the enrollment.</summary>
    public Enrollment Enrollment { get; set; }

    /// <summary>Gets or sets the learner name.</summary>
    public string LearnerName { get; set; }

    /// <summary>Gets or sets the score of each graded item, by id.</summary>
    public IDictionary<string, CourseItemProgress> Items { get; set; } = new Dictionary<string, CourseItemProgress>(StringComparer.Ordinal);

    /// <summary>Gets or sets the score of each category and the items it dropped.</summary>
    public CourseGradeBreakdown Breakdown { get; set; } = new();
}
