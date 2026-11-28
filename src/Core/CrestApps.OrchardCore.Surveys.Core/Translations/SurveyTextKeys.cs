namespace CrestApps.OrchardCore.Surveys.Core.Translations;

/// <summary>
/// The stable keys of the translatable texts of a survey definition. A key names the item by its stable id, so a
/// translation keeps working when the item moves or its text changes.
/// </summary>
public static class SurveyTextKeys
{
    /// <summary>
    /// The key of the respondent-facing title.
    /// </summary>
    public const string SurveyTitle = "survey:title";

    /// <summary>
    /// The key of the introduction.
    /// </summary>
    public const string SurveyIntroduction = "survey:introduction";

    /// <summary>
    /// Gets the key of a page title.
    /// </summary>
    /// <param name="pageId">The page id.</param>
    /// <returns>The key.</returns>
    public static string PageTitle(string pageId)
        => $"page-title:{pageId}";

    /// <summary>
    /// Gets the key of a page description.
    /// </summary>
    /// <param name="pageId">The page id.</param>
    /// <returns>The key.</returns>
    public static string PageDescription(string pageId)
        => $"page-description:{pageId}";

    /// <summary>
    /// Gets the key of a question title.
    /// </summary>
    /// <param name="questionId">The question id.</param>
    /// <returns>The key.</returns>
    public static string QuestionTitle(string questionId)
        => $"title:{questionId}";

    /// <summary>
    /// Gets the key of a question description.
    /// </summary>
    /// <param name="questionId">The question id.</param>
    /// <returns>The key.</returns>
    public static string QuestionDescription(string questionId)
        => $"description:{questionId}";

    /// <summary>
    /// Gets the key of a question placeholder.
    /// </summary>
    /// <param name="questionId">The question id.</param>
    /// <returns>The key.</returns>
    public static string QuestionPlaceholder(string questionId)
        => $"placeholder:{questionId}";

    /// <summary>
    /// Gets the key of a choice text. Question types read it through <c>SurveyFormatContext.Text</c>.
    /// </summary>
    /// <param name="questionId">The question id.</param>
    /// <param name="choiceId">The choice id.</param>
    /// <returns>The key.</returns>
    public static string Choice(string questionId, string choiceId)
        => $"choice:{questionId}:{choiceId}";

    /// <summary>
    /// Gets the key of a row text.
    /// </summary>
    /// <param name="questionId">The question id.</param>
    /// <param name="rowId">The row id.</param>
    /// <returns>The key.</returns>
    public static string Row(string questionId, string rowId)
        => $"row:{questionId}:{rowId}";

    /// <summary>
    /// Gets the key of an ending title.
    /// </summary>
    /// <param name="endingId">The ending id.</param>
    /// <returns>The key.</returns>
    public static string EndingTitle(string endingId)
        => $"ending-title:{endingId}";

    /// <summary>
    /// Gets the key of an ending message.
    /// </summary>
    /// <param name="endingId">The ending id.</param>
    /// <returns>The key.</returns>
    public static string EndingMessage(string endingId)
        => $"ending-message:{endingId}";
}
