using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.KnowledgeBase.Models;

/// <summary>
/// The help center's search box, as a widget.
/// </summary>
public sealed class HelpCenterSearchPart : ContentPart
{
}

/// <summary>
/// The help center's categories, with how many articles the reader may open in each, as a widget.
/// </summary>
public sealed class KnowledgeBaseCategoriesPart : ContentPart
{
}

/// <summary>
/// On an article's page, other articles about the same thing, as a widget.
/// </summary>
public sealed class RelatedArticlesPart : ContentPart
{
    /// <summary>
    /// The most articles the widget may list.
    /// </summary>
    public const int MaxCount = 20;

    /// <summary>
    /// Gets or sets how many articles to list.
    /// </summary>
    public int Count { get; set; } = 5;
}
