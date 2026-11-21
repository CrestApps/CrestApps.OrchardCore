namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// The shape names and alternates of question rendering, so the runner, the review and the reports build them alike.
/// </summary>
public static class SurveyControlShapes
{
    /// <summary>
    /// The shape that renders a question type.
    /// </summary>
    public const string Control = "SurveyControl";

    /// <summary>
    /// The settings editor shape of a question type.
    /// </summary>
    public const string Settings = "SurveyControlSettings";

    /// <summary>
    /// The display type respondents answer in.
    /// </summary>
    public const string Respond = "Respond";

    /// <summary>
    /// The read-only display type with correct marks and feedback.
    /// </summary>
    public const string Review = "Review";

    /// <summary>
    /// The printable display type.
    /// </summary>
    public const string Print = "Print";

    /// <summary>
    /// The results display type.
    /// </summary>
    public const string Summary = "Summary";

    /// <summary>
    /// Gets the alternates of a question shape, most specific last: <c>SurveyControl_{DisplayType}</c> (the fallback of
    /// every type), <c>SurveyControl_{DisplayType}__{ControlType}</c>, then the survey and question overrides.
    /// </summary>
    /// <param name="displayType">The display type.</param>
    /// <param name="controlType">The question type.</param>
    /// <param name="surveyName">The survey name, or <see langword="null"/>.</param>
    /// <param name="questionName">The question name, or <see langword="null"/>.</param>
    /// <returns>The alternates.</returns>
    public static IReadOnlyList<string> GetAlternates(string displayType, string controlType, string surveyName, string questionName)
    {
        var baseName = $"{Control}_{displayType}__{Encode(controlType)}";
        var alternates = new List<string> { $"{Control}_{displayType}", baseName };

        if (!string.IsNullOrEmpty(surveyName))
        {
            alternates.Add($"{baseName}__{Encode(surveyName)}");
        }

        if (!string.IsNullOrEmpty(questionName))
        {
            alternates.Add($"{baseName}__{Encode(questionName)}");
        }

        return alternates;
    }

    /// <summary>
    /// Makes a name safe for a shape alternate: dots and dashes become underscores (a dot in a view name means a display
    /// type, a dash means an alternate).
    /// </summary>
    /// <param name="value">The name.</param>
    /// <returns>The encoded name.</returns>
    public static string Encode(string value)
        => (value ?? string.Empty).Replace('.', '_').Replace('-', '_');
}
