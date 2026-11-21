namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// One row of a matrix, prompt of a matching question or item of a multiple text question.
/// </summary>
public sealed class SurveySubQuestion
{
    /// <summary>
    /// Gets or sets the stable id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the variable code, unique within the question.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets whether it is hidden from new respondents but kept for results.
    /// </summary>
    public bool Retired { get; set; }
}
