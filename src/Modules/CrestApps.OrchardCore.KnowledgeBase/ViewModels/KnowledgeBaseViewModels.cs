using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;

namespace CrestApps.OrchardCore.KnowledgeBase.ViewModels;

public class KnowledgeArticlePartViewModel
{
    public KnowledgeArticleAudience Audience { get; set; }

    public string Summary { get; set; }
}

public class KnowledgeArticleFeedbackViewModel
{
    public string ContentItemId { get; set; }

    public int HelpfulCount { get; set; }
}

/// <summary>
/// The help center's home (the articles by category), a category's page, or search results.
/// </summary>
public class HelpCenterViewModel
{
    public string Query { get; set; }

    /// <summary>
    /// Gets or sets the category whose page this is.
    /// </summary>
    public string CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the category's name, with the categories above it, when it is one.
    /// </summary>
    public string CategoryName { get; set; }

    /// <summary>
    /// Gets or sets the categories with articles the reader may read, in the taxonomy's order.
    /// </summary>
    public IReadOnlyList<HelpCenterCategory> Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets, on the home page, each category's most helpful articles.
    /// </summary>
    public IReadOnlyList<HelpCenterGroup> Groups { get; set; } = [];

    /// <summary>
    /// Gets or sets the articles found, or a category's articles.
    /// </summary>
    public IReadOnlyList<KnowledgeArticleIndex> Articles { get; set; } = [];
}

/// <summary>
/// A category in the help center's list.
/// </summary>
/// <param name="Id">The term's content item id.</param>
/// <param name="Name">Its name.</param>
/// <param name="Level">How deep it is: 0 for a top category.</param>
/// <param name="Count">The articles in it and in the categories under it.</param>
public sealed record HelpCenterCategory(string Id, string Name, int Level, int Count);

/// <summary>
/// A category on the help center's home page, with its most helpful articles.
/// </summary>
/// <param name="Id">The term's content item id.</param>
/// <param name="Name">Its name, with the categories above it.</param>
/// <param name="Count">The articles in it.</param>
/// <param name="Articles">Its most helpful articles.</param>
public sealed record HelpCenterGroup(string Id, string Name, int Count, IReadOnlyList<KnowledgeArticleIndex> Articles);
