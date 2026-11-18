namespace CrestApps.OrchardCore.KnowledgeBase;

/// <summary>
/// The names the Knowledge Base uses.
/// </summary>
public static class KnowledgeBaseConstants
{
    /// <summary>
    /// The rate-limit group of readers' "was this helpful?" answers, held when Orchard Core's Rate Limits feature is on.
    /// </summary>
    public const string FeedbackRateLimitGroup = "knowledge-base-feedback";

    /// <summary>
    /// The Knowledge Base feature.
    /// </summary>
    public const string Feature = "CrestApps.OrchardCore.KnowledgeBase";

    /// <summary>
    /// The YesSql collection the Knowledge Base keeps its own records in (readers' feedback).
    /// </summary>
    public const string CollectionName = "KnowledgeBase";

    /// <summary>
    /// The content type of an article.
    /// </summary>
    public const string ArticleType = "KnowledgeArticle";

    /// <summary>
    /// The part that makes a content item an article.
    /// </summary>
    public const string PartName = "KnowledgeArticlePart";

    /// <summary>
    /// The part, Content Access Control's restricted role picker, that holds who may read an article.
    /// </summary>
    public const string WhoCanReadItPart = "RolePickerPart";

    /// <summary>
    /// The taxonomy field, on the Knowledge Article type, that holds an article's category.
    /// </summary>
    public const string CategoryField = "Category";

    /// <summary>
    /// The content type of the terms of the Knowledge Base Categories taxonomy.
    /// </summary>
    public const string CategoryTermType = "KnowledgeCategory";

    /// <summary>
    /// The widget type of the help center's search box.
    /// </summary>
    public const string SearchWidgetType = "HelpCenterSearchWidget";

    /// <summary>
    /// The widget type of the help center's categories.
    /// </summary>
    public const string CategoriesWidgetType = "KnowledgeBaseCategoriesWidget";

    /// <summary>
    /// The widget type of an article's related articles.
    /// </summary>
    public const string RelatedArticlesWidgetType = "RelatedArticlesWidget";

    /// <summary>
    /// The zones the help center's page and an article's page show widgets in, whatever the theme: above the page, beside
    /// it, and after the article.
    /// </summary>
    public static class Zones
    {
        public const string Top = "KnowledgeBaseTop";

        public const string Sidebar = "KnowledgeBaseSidebar";

        public const string AfterContent = "KnowledgeBaseAfterContent";
    }

    /// <summary>
    /// Gets the widget type made of a widget part.
    /// </summary>
    /// <param name="partName">The part.</param>
    /// <returns>The widget type.</returns>
    public static string WidgetTypeOf(string partName)
        => partName switch
        {
            "HelpCenterSearchPart" => SearchWidgetType,
            "KnowledgeBaseCategoriesPart" => CategoriesWidgetType,
            "RelatedArticlesPart" => RelatedArticlesWidgetType,
            _ => throw new ArgumentOutOfRangeException(nameof(partName), partName, null),
        };

    /// <summary>
    /// The route of the help center's home.
    /// </summary>
    public const string HomeRouteName = "KnowledgeBaseHome";

    /// <summary>
    /// The route of the suggestions endpoint, <c>help/api/suggest?q=</c>.
    /// </summary>
    public const string SuggestRouteName = "KnowledgeBaseSuggest";
}
