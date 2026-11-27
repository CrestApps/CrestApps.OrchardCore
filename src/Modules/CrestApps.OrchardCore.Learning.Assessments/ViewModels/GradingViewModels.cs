using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;

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

/// <summary>
/// The page of one attempt for graders.
/// </summary>
public class AttemptGradingViewModel
{
    /// <summary>Gets or sets the attempt.</summary>
    public AssessmentAttempt Attempt { get; set; }

    /// <summary>Gets or sets the name of the learner.</summary>
    public string LearnerName { get; set; }

    /// <summary>Gets or sets the guidance of each essay question, by question id.</summary>
    public IDictionary<string, string> Guidance { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets or sets whether the person may regrade, change a score of a graded attempt and void.</summary>
    public bool CanOverride { get; set; }

    /// <summary>Gets or sets whether the person may give overrides.</summary>
    public bool CanManageOverrides { get; set; }

    /// <summary>Gets or sets the names of the people in the history.</summary>
    public IDictionary<string, string> ActorNames { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// The overrides of a quiz and the form that adds one.
/// </summary>
public class QuizOverridesViewModel
{
    /// <summary>Gets or sets the quiz.</summary>
    [BindNever]
    public string QuizId { get; set; }

    /// <summary>Gets or sets the quiz title.</summary>
    [BindNever]
    public string QuizTitle { get; set; }

    /// <summary>Gets or sets the overrides.</summary>
    [BindNever]
    public IList<AssessmentOverride> Overrides { get; set; } = [];

    /// <summary>Gets or sets the learner names, by user id.</summary>
    [BindNever]
    public IDictionary<string, string> Names { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets or sets the user name of the learner.</summary>
    public string UserName { get; set; }

    /// <summary>Gets or sets the extra minutes.</summary>
    public int ExtraMinutes { get; set; }

    /// <summary>Gets or sets the extra attempts.</summary>
    public int ExtraAttempts { get; set; }

    /// <summary>Gets or sets another opening time, in the editor time zone.</summary>
    public DateTime? Opens { get; set; }

    /// <summary>Gets or sets another closing time, in the editor time zone.</summary>
    public DateTime? Closes { get; set; }

    /// <summary>Gets or sets why.</summary>
    public string Reason { get; set; }

    /// <summary>Gets or sets the editor the display drivers build: its fields in cards.</summary>
    [BindNever]
    public IShape Editor { get; set; }
}
