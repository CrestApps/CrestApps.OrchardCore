namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The totals of one choice question.
/// </summary>
public class SurveyResultsQuestion
{
    /// <summary>
    /// Gets or sets the question text.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the choices with their counts and percents.
    /// </summary>
    public IList<(string Text, long Count, decimal Percent)> Choices { get; set; } = [];
}
