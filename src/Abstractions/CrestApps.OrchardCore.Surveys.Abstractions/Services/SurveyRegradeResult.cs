namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The result of scoring the responses of older versions again with the key of another version.
/// </summary>
public sealed class SurveyRegradeResult
{
    /// <summary>
    /// Gets or sets how many responses were scored again.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets the questions that kept their scores because the key names choices or rows the response's version does not
    /// have, such as a bank imported again before choices kept their ids.
    /// </summary>
    public List<SurveyRegradeSkippedQuestion> SkippedQuestions { get; } = [];
}
