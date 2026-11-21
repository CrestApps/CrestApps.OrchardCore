using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// The scoring key and feedback of a question. It is never sent to respondents.
/// </summary>
public sealed class SurveyQuestionScoring
{
    /// <summary>
    /// Gets or sets the points; null uses the survey default and 0 leaves the question unscored.
    /// </summary>
    public decimal? Points { get; set; }

    /// <summary>
    /// Gets or sets whether partly right answers earn part of the points.
    /// </summary>
    public bool PartialCredit { get; set; }

    /// <summary>
    /// Gets or sets the points taken away for each wrong choice when negative marking is on.
    /// </summary>
    public decimal? PenaltyPerWrong { get; set; }

    /// <summary>
    /// Gets or sets the id of the score category the points add to.
    /// </summary>
    public string CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the points of each choice by choice id (may be negative).
    /// </summary>
    public Dictionary<string, decimal> ChoicePoints { get; set; } = [];

    /// <summary>
    /// Gets or sets the ids of the right choices.
    /// </summary>
    public List<string> CorrectChoiceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the accepted text answers.
    /// </summary>
    public List<string> AcceptedAnswers { get; set; } = [];

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
    /// Gets or sets the right pairs of a matching question: row id, then choice id.
    /// </summary>
    public Dictionary<string, string> Pairs { get; set; } = [];

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
    /// Gets or sets the feedback of each choice by choice id.
    /// </summary>
    public Dictionary<string, string> ChoiceFeedback { get; set; } = [];

    /// <summary>
    /// Gets or sets guidance for graders of manually graded questions.
    /// </summary>
    public string GraderGuidance { get; set; }

    /// <summary>
    /// Gets or sets control-specific key data of custom controls.
    /// </summary>
    public JsonObject Key { get; set; } = [];
}
