namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// The result of summarizing the answers to a written question.
/// </summary>
public sealed class SurveyAnswerSummaryResult
{
    /// <summary>
    /// The survey or question does not exist, or the user may not read its responses.
    /// </summary>
    public const string NotFound = "NotFound";

    /// <summary>
    /// No AI model is set up.
    /// </summary>
    public const string NoModel = "NoModel";

    /// <summary>
    /// There are too few answers to summarize without risking identifying someone.
    /// </summary>
    public const string TooFew = "TooFew";

    /// <summary>
    /// Gets or sets the error, or <see langword="null"/>.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the summary, in Markdown.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets how many answers were read.
    /// </summary>
    public int AnswerCount { get; set; }
}
