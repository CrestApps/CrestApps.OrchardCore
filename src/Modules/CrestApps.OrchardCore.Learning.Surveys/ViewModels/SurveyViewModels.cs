using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Learning.Surveys.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Surveys.ViewModels;

/// <summary>
/// The editor of the survey part.
/// </summary>
public class SurveyPartViewModel
{
    /// <summary>Gets or sets the questions.</summary>
    public List<SurveyQuestionRow> Questions { get; set; } = [];

    /// <summary>Gets or sets whether staff see the answers without names.</summary>
    public bool Anonymous { get; set; }

    /// <summary>Gets or sets whether a learner may change their answers.</summary>
    public bool AllowChangeAnswer { get; set; }

    /// <summary>Gets or sets when the survey closes, in the editor's time zone.</summary>
    public DateTime? Close { get; set; }

    /// <summary>Gets or sets when learners see the totals.</summary>
    public SurveyResultsVisibility ResultsVisibility { get; set; }

    /// <summary>Gets or sets the survey item.</summary>
    [BindNever]
    public string SurveyId { get; set; }

    /// <summary>Gets or sets whether the item is not saved yet.</summary>
    [BindNever]
    public bool IsNew { get; set; }

    /// <summary>Gets or sets whether the survey has one choice question, as a choice does.</summary>
    [BindNever]
    public bool SingleQuestion { get; set; }

    /// <summary>Gets or sets the kinds of question.</summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];

    /// <summary>Gets or sets the results visibility options.</summary>
    [BindNever]
    public IList<SelectListItem> ResultsVisibilityOptions { get; set; } = [];
}

/// <summary>
/// A question row of the survey editor.
/// </summary>
public class SurveyQuestionRow
{
    /// <summary>Gets or sets the id; empty for a new question.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the question.</summary>
    public string Text { get; set; }

    /// <summary>Gets or sets the kind of answer.</summary>
    public SurveyQuestionType Type { get; set; }

    /// <summary>Gets or sets the choices, one a line.</summary>
    public string Options { get; set; }

    /// <summary>Gets or sets whether the question must be answered.</summary>
    public bool Required { get; set; }
}

/// <summary>
/// A question row rendered in the editor, with its index and the kinds of question.
/// </summary>
/// <param name="Index">The index of the row.</param>
/// <param name="Row">The row.</param>
/// <param name="TypeOptions">The kinds of question.</param>
/// <param name="SingleQuestion">Whether the survey has one question.</param>
public sealed record SurveyQuestionRowModel(int Index, SurveyQuestionRow Row, IList<SelectListItem> TypeOptions, bool SingleQuestion);

/// <summary>
/// The survey panel of the learner player.
/// </summary>
public class SurveyPanelViewModel
{
    /// <summary>Gets or sets the survey item.</summary>
    public string SurveyId { get; set; }

    /// <summary>Gets or sets the survey.</summary>
    public SurveyPart Part { get; set; }

    /// <summary>Gets or sets whether someone is signed in.</summary>
    public bool SignedIn { get; set; }

    /// <summary>Gets or sets whether the learner has an enrollment in the course.</summary>
    public bool Enrolled { get; set; }

    /// <summary>Gets or sets whether the enrollment is active.</summary>
    public bool EnrollmentActive { get; set; }

    /// <summary>Gets or sets whether the survey stopped taking answers.</summary>
    public bool Closed { get; set; }

    /// <summary>Gets or sets the learner's response.</summary>
    public SurveyResponse Response { get; set; }

    /// <summary>Gets or sets whether the learner may answer now.</summary>
    public bool CanAnswer { get; set; }

    /// <summary>
    /// Gets or sets why the learner cannot answer, when they cannot: <c>Pending</c>, <c>Suspended</c>,
    /// <c>AccessEnded</c>, <c>Closed</c>, <c>Answered</c> or <c>NotOpen</c>.
    /// </summary>
    public string CannotAnswerReason { get; set; }

    /// <summary>Gets or sets the totals, when the learner may see them.</summary>
    public SurveySummary Summary { get; set; }
}

/// <summary>
/// The totals page of a survey.
/// </summary>
public class SurveyResultsViewModel
{
    /// <summary>Gets or sets the survey item.</summary>
    public ContentItem Survey { get; set; }

    /// <summary>Gets or sets the survey.</summary>
    public SurveyPart Part { get; set; }

    /// <summary>Gets or sets the totals.</summary>
    public SurveySummary Summary { get; set; }

    /// <summary>Gets or sets the written answers with the learner's name, by question id, for a survey that is not anonymous.</summary>
    public IDictionary<string, IList<(string Learner, string Answer)>> NamedTextAnswers { get; set; } = new Dictionary<string, IList<(string Learner, string Answer)>>(StringComparer.Ordinal);
}
