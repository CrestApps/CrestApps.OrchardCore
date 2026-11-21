namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// The feedback of one answer: right or wrong, the right answer and the feedback texts. The review policy decides what
/// a respondent sees of it.
/// </summary>
public sealed class SurveyAnswerFeedback
{
    /// <summary>
    /// Gets or sets whether the answer is right, or <see langword="null"/> when that does not apply.
    /// </summary>
    public bool? Correct { get; set; }

    /// <summary>
    /// Gets or sets the right answer as text.
    /// </summary>
    public string RightAnswer { get; set; }

    /// <summary>
    /// Gets or sets the ids of the right choices.
    /// </summary>
    public List<string> CorrectChoiceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the feedback of the answer (right or wrong).
    /// </summary>
    public string Feedback { get; set; }

    /// <summary>
    /// Gets or sets the general feedback of the question.
    /// </summary>
    public string GeneralFeedback { get; set; }

    /// <summary>
    /// Gets or sets the feedback of each chosen choice by choice id.
    /// </summary>
    public Dictionary<string, string> ChoiceFeedback { get; set; } = [];
}
