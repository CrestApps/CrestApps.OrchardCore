namespace CrestApps.OrchardCore.Surveys.Core.Logic;

/// <summary>
/// The names of the condition sources a survey rule may read besides question ids.
/// </summary>
public static class SurveyConditionSources
{
    /// <summary>
    /// The prefix of a variable source: <c>var:{name}</c>.
    /// </summary>
    public const string VariablePrefix = "var:";

    /// <summary>
    /// The total score.
    /// </summary>
    public const string Score = "score";

    /// <summary>
    /// The prefix of a category score source: <c>score:{category}</c>.
    /// </summary>
    public const string ScorePrefix = "score:";

    /// <summary>
    /// The prefix of a consumer source: <c>context:{key}</c>.
    /// </summary>
    public const string ContextPrefix = "context:";

    /// <summary>
    /// The id of the collector the response came through.
    /// </summary>
    public const string Collector = "collector";

    /// <summary>
    /// The culture of the response.
    /// </summary>
    public const string Culture = "culture";

    /// <summary>
    /// The rule key of a page: <c>page:{id}</c>.
    /// </summary>
    /// <param name="pageId">The page id.</param>
    /// <returns>The key.</returns>
    public static string PageKey(string pageId)
        => "page:" + pageId;

    /// <summary>
    /// The rule key of a question: <c>q:{id}</c>.
    /// </summary>
    /// <param name="questionId">The question id.</param>
    /// <returns>The key.</returns>
    public static string QuestionKey(string questionId)
        => "q:" + questionId;

    /// <summary>
    /// The rule key of a choice: <c>c:{questionId}:{choiceId}</c>.
    /// </summary>
    /// <param name="questionId">The question id.</param>
    /// <param name="choiceId">The choice id.</param>
    /// <returns>The key.</returns>
    public static string ChoiceKey(string questionId, string choiceId)
        => "c:" + questionId + ":" + choiceId;
}
