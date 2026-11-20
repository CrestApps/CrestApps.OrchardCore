using CrestApps.OrchardCore.Learning.Assessments.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.Assessments.ViewModels;

/// <summary>
/// The editor of the quiz part.
/// </summary>
public class QuizPartViewModel
{
    /// <summary>Gets or sets the fixed questions.</summary>
    public string[] QuestionIds { get; set; } = [];

    /// <summary>Gets or sets the draw rules.</summary>
    public IList<DrawRuleRowViewModel> DrawRules { get; set; } = [];

    /// <summary>Gets or sets whether questions are shuffled.</summary>
    public bool ShuffleQuestions { get; set; }

    /// <summary>Gets or sets the time limit.</summary>
    public int? TimeLimitMinutes { get; set; }

    /// <summary>Gets or sets the attempts allowed.</summary>
    public int? MaxAttempts { get; set; }

    /// <summary>Gets or sets the minutes between attempts.</summary>
    public int DelayMinutes { get; set; }

    /// <summary>Gets or sets the pass mark.</summary>
    public decimal? PassMark { get; set; }

    /// <summary>Gets or sets the grading method.</summary>
    public GradingMethod GradingMethod { get; set; }

    /// <summary>Gets or sets the navigation.</summary>
    public QuizNavigation Navigation { get; set; }

    /// <summary>Gets or sets when the quiz opens, in the editor time zone.</summary>
    public DateTime? Opens { get; set; }

    /// <summary>Gets or sets when the quiz closes, in the editor time zone.</summary>
    public DateTime? Closes { get; set; }

    /// <summary>Gets or sets a new access code.</summary>
    public string NewAccessCode { get; set; }

    /// <summary>Gets or sets whether to remove the access code.</summary>
    public bool ClearAccessCode { get; set; }

    /// <summary>Gets or sets whether an access code is set.</summary>
    [BindNever]
    public bool HasAccessCode { get; set; }

    /// <summary>Gets or sets the honor statement.</summary>
    public string HonorStatement { get; set; }

    /// <summary>
    /// Gets or sets what learners see after an attempt: a <see cref="ReviewPolicy"/> name, or <see cref="CustomReview"/>
    /// for the review options per moment.
    /// </summary>
    public string ReviewMode { get; set; }

    /// <summary>Gets or sets what learners see at each moment, used when the review mode is custom.</summary>
    public IList<ReviewMomentViewModel> ReviewMoments { get; set; } = [];

    /// <summary>Gets or sets how many questions a page shows; 0 for every question on one page.</summary>
    public int QuestionsPerPage { get; set; }

    /// <summary>Gets or sets when learners get feedback on an answer.</summary>
    public QuizFeedbackMode FeedbackMode { get; set; }

    /// <summary>The review mode of the options per moment.</summary>
    public const string CustomReview = "Custom";

    /// <summary>Gets or sets the chosen questions, as the <c>[{ value, text }]</c> the question picker starts with.</summary>
    [BindNever]
    public string InitialQuestionsJson { get; set; } = "[]";

    /// <summary>Gets or sets the banks to choose from.</summary>
    [BindNever]
    public IList<SelectListItem> BankOptions { get; set; } = [];
}

/// <summary>
/// What learners see at one moment of the review.
/// </summary>
public class ReviewMomentViewModel
{
    /// <summary>Gets or sets the moment.</summary>
    public ReviewMoment Moment { get; set; }

    /// <summary>Gets or sets whether the score shows.</summary>
    public bool Score { get; set; }

    /// <summary>Gets or sets whether the right answers show.</summary>
    public bool RightAnswers { get; set; }

    /// <summary>Gets or sets whether the feedback of the answers shows.</summary>
    public bool Feedback { get; set; }

    /// <summary>Gets or sets whether the general feedback shows.</summary>
    public bool GeneralFeedback { get; set; }
}

/// <summary>
/// A draw rule row.
/// </summary>
public class DrawRuleRowViewModel
{
    /// <summary>Gets or sets the bank.</summary>
    public string BankId { get; set; }

    /// <summary>Gets or sets the tag.</summary>
    public string Tag { get; set; }

    /// <summary>Gets or sets the difficulty.</summary>
    public int? Difficulty { get; set; }

    /// <summary>Gets or sets how many to draw.</summary>
    public int Count { get; set; }
}
