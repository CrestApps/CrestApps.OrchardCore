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

    /// <summary>Gets or sets the review policy.</summary>
    public ReviewPolicy ReviewPolicy { get; set; }

    /// <summary>Gets or sets the chosen questions, as the <c>[{ value, text }]</c> the question picker starts with.</summary>
    [BindNever]
    public string InitialQuestionsJson { get; set; } = "[]";

    /// <summary>Gets or sets the banks to choose from.</summary>
    [BindNever]
    public IList<SelectListItem> BankOptions { get; set; } = [];
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
