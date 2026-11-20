using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Surveys.Models;

/// <summary>
/// A survey in a course outline: the questions a learner answers, whether the answers are anonymous to staff, whether
/// a learner may change their answers, when the survey closes and when learners see the totals. Feedback surveys and
/// choices are both surveys; a choice is a survey of one question. The introduction is the item's HTML body.
/// </summary>
public sealed class SurveyPart : ContentPart
{
    /// <summary>
    /// Gets or sets the questions, in the order learners see them.
    /// </summary>
    public List<SurveyQuestion> Questions { get; set; } = [];

    /// <summary>
    /// Gets or sets whether staff see the answers without the names of the people who gave them. The response still
    /// records the learner, so each learner answers once and their answers can be exported or erased for them.
    /// </summary>
    public bool Anonymous { get; set; }

    /// <summary>
    /// Gets or sets whether a learner may change their answers until the survey closes.
    /// </summary>
    public bool AllowChangeAnswer { get; set; }

    /// <summary>
    /// Gets or sets when the survey stops taking answers, in UTC, or <see langword="null"/> to keep it open.
    /// </summary>
    public DateTime? CloseUtc { get; set; }

    /// <summary>
    /// Gets or sets when learners see the totals.
    /// </summary>
    public SurveyResultsVisibility ResultsVisibility { get; set; } = SurveyResultsVisibility.Never;
}

/// <summary>
/// A question of a survey.
/// </summary>
public sealed class SurveyQuestion
{
    /// <summary>
    /// Gets or sets the id of the question, which the answers refer to; it stays the same when the text changes.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the question.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the kind of answer.
    /// </summary>
    public SurveyQuestionType Type { get; set; } = SurveyQuestionType.SingleChoice;

    /// <summary>
    /// Gets or sets the choices of a single or multiple choice question.
    /// </summary>
    public List<string> Options { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the question must be answered.
    /// </summary>
    public bool Required { get; set; }
}

/// <summary>
/// The kind of answer a survey question takes.
/// </summary>
public enum SurveyQuestionType
{
    /// <summary>
    /// One of the choices.
    /// </summary>
    SingleChoice,

    /// <summary>
    /// Any of the choices.
    /// </summary>
    MultipleChoice,

    /// <summary>
    /// A rating from 1 to 5.
    /// </summary>
    Rating,

    /// <summary>
    /// A written answer.
    /// </summary>
    Text,
}

/// <summary>
/// When learners see the totals of a survey.
/// </summary>
public enum SurveyResultsVisibility
{
    /// <summary>
    /// Only staff see them.
    /// </summary>
    Never,

    /// <summary>
    /// A learner sees them once they answered.
    /// </summary>
    AfterAnswer,

    /// <summary>
    /// Learners see them once the survey closed.
    /// </summary>
    AfterClose,

    /// <summary>
    /// Learners see them at any time.
    /// </summary>
    Always,
}
