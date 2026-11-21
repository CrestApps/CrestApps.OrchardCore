namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// How an embedded survey shows.
/// </summary>
public enum SurveyEmbedDisplay
{
    /// <summary>
    /// The survey's title, introduction and Start button in the page.
    /// </summary>
    Inline,

    /// <summary>
    /// A button that opens the survey.
    /// </summary>
    Button,

    /// <summary>
    /// A link that opens the survey.
    /// </summary>
    Link,
}
