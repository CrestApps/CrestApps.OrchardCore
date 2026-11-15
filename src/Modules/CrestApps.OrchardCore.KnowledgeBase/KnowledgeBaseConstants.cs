namespace CrestApps.OrchardCore.KnowledgeBase;

/// <summary>
/// The names the Knowledge Base uses.
/// </summary>
public static class KnowledgeBaseConstants
{
    /// <summary>
    /// The Knowledge Base feature.
    /// </summary>
    public const string Feature = "CrestApps.OrchardCore.KnowledgeBase";

    /// <summary>
    /// The content type of an article.
    /// </summary>
    public const string ArticleType = "KnowledgeArticle";

    /// <summary>
    /// The part that makes a content item an article.
    /// </summary>
    public const string PartName = "KnowledgeArticlePart";

    /// <summary>
    /// The route of the help center's home.
    /// </summary>
    public const string HomeRouteName = "KnowledgeBaseHome";

    /// <summary>
    /// The route of the suggestions endpoint, <c>help/api/suggest?q=</c>.
    /// </summary>
    public const string SuggestRouteName = "KnowledgeBaseSuggest";
}
