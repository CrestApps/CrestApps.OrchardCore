using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The scoring key and feedback of a question.
/// </summary>
public class SurveyScoringEditViewModel
{
    /// <summary>
    /// Gets or sets the points, or <see langword="null"/> for the survey default.
    /// </summary>
    public decimal? Points { get; set; }

    /// <summary>
    /// Gets or sets whether partly right answers earn part of the points.
    /// </summary>
    public bool PartialCredit { get; set; }

    /// <summary>
    /// Gets or sets the points taken away for each wrong choice.
    /// </summary>
    public decimal? PenaltyPerWrong { get; set; }

    /// <summary>
    /// Gets or sets the score category.
    /// </summary>
    public string CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the key of each choice.
    /// </summary>
    public List<SurveyChoiceKeyEditViewModel> KeyChoices { get; set; } = [];

    /// <summary>
    /// Gets or sets the accepted text answers, one per line.
    /// </summary>
    public string AcceptedAnswers { get; set; }

    /// <summary>
    /// Gets or sets whether text answers compare with case.
    /// </summary>
    public bool CaseSensitive { get; set; }

    /// <summary>
    /// Gets or sets the right number.
    /// </summary>
    public decimal? NumberAnswer { get; set; }

    /// <summary>
    /// Gets or sets how far a number may be from the right number.
    /// </summary>
    public decimal Tolerance { get; set; }

    /// <summary>
    /// Gets or sets the right pairs of a matching question.
    /// </summary>
    public List<SurveyPairEditViewModel> Pairs { get; set; } = [];

    /// <summary>
    /// Gets or sets the feedback shown for any answer.
    /// </summary>
    public string GeneralFeedback { get; set; }

    /// <summary>
    /// Gets or sets the feedback shown for a right answer.
    /// </summary>
    public string CorrectFeedback { get; set; }

    /// <summary>
    /// Gets or sets the feedback shown for a wrong answer.
    /// </summary>
    public string IncorrectFeedback { get; set; }

    /// <summary>
    /// Gets or sets guidance for graders.
    /// </summary>
    public string GraderGuidance { get; set; }

    /// <summary>
    /// Gets or sets the score categories.
    /// </summary>
    [BindNever]
    public IReadOnlyList<SelectListItem> Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets the choices pairs may name.
    /// </summary>
    [BindNever]
    public IReadOnlyList<SelectListItem> PairChoices { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the question has choices.
    /// </summary>
    [BindNever]
    public bool HasChoices { get; set; }

    /// <summary>
    /// Gets or sets whether the question has rows and choices to pair.
    /// </summary>
    [BindNever]
    public bool HasPairs { get; set; }

    /// <summary>
    /// Gets or sets whether a grader scores the question.
    /// </summary>
    [BindNever]
    public bool ManuallyGraded { get; set; }

    /// <summary>
    /// Gets or sets the survey's default points.
    /// </summary>
    [BindNever]
    public decimal DefaultPoints { get; set; }
}
