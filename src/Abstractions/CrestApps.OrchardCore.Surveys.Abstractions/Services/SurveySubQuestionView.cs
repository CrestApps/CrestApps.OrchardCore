namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// What a respondent may see of a row.
/// </summary>
public sealed class SurveySubQuestionView
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the variable code.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string Text { get; set; }
}
