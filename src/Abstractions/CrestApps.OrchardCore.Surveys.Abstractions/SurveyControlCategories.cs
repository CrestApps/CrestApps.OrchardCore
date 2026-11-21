namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// The categories of the question type picker. A category is an open string.
/// </summary>
public static class SurveyControlCategories
{
    /// <summary>
    /// Choice questions.
    /// </summary>
    public const string Choice = "Choice";

    /// <summary>
    /// Text questions.
    /// </summary>
    public const string Text = "Text";

    /// <summary>
    /// Number questions.
    /// </summary>
    public const string Number = "Number";

    /// <summary>
    /// Date and time questions.
    /// </summary>
    public const string Date = "Date";

    /// <summary>
    /// Rating and scale questions.
    /// </summary>
    public const string Rating = "Rating";

    /// <summary>
    /// Grid questions.
    /// </summary>
    public const string Matrix = "Matrix";

    /// <summary>
    /// Text and media that need no answer.
    /// </summary>
    public const string Content = "Content";

    /// <summary>
    /// File uploads.
    /// </summary>
    public const string Files = "Files";

    /// <summary>
    /// Quiz questions such as matching and blanks.
    /// </summary>
    public const string Assessment = "Assessment";

    /// <summary>
    /// Other questions, such as hidden and calculated values.
    /// </summary>
    public const string Advanced = "Advanced";
}
