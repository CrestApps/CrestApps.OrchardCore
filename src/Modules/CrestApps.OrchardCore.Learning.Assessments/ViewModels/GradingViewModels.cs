using CrestApps.OrchardCore.Learning.Assessments.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Learning.Assessments.ViewModels;

/// <summary>
/// The list of work that waits for a grader: quiz attempts and the work of every other grading queue source.
/// </summary>
public class GradingListViewModel
{
    /// <summary>Gets or sets the work.</summary>
    [BindNever]
    public AdminListContext<GradingQueueRow> List { get; set; }
}

/// <summary>
/// A row of the grading list.
/// </summary>
public class GradingQueueRow
{
    /// <summary>Gets or sets the work.</summary>
    public GradingQueueItem Item { get; set; }

    /// <summary>Gets or sets the name of the learner.</summary>
    public string LearnerName { get; set; }

    /// <summary>Gets or sets the address of the page that grades the work.</summary>
    public string Url { get; set; }
}
