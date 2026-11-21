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

    /// <summary>
    /// Gets the position of a category in the question type picker: the built-in categories in the order people look
    /// for them, then any other category.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <returns>The position; lower comes first.</returns>
    public static int Order(string category)
        => category switch
        {
            Choice => 0,
            Text => 1,
            Number => 2,
            Rating => 3,
            Matrix => 4,
            Date => 5,
            Content => 6,
            Files => 7,
            Assessment => 8,
            Advanced => 10,
            _ => 9,
        };
}
