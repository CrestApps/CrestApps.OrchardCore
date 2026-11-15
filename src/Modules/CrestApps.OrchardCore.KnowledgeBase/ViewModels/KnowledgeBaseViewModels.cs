using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;

namespace CrestApps.OrchardCore.KnowledgeBase.ViewModels;

public class KnowledgeArticlePartViewModel
{
    public KnowledgeArticleAudience Audience { get; set; }

    public string Category { get; set; }

    public string Summary { get; set; }
}

public class KnowledgeArticleFeedbackViewModel
{
    public string ContentItemId { get; set; }

    public string Category { get; set; }

    public int HelpfulCount { get; set; }
}

/// <summary>
/// The help center's home, a category, or search results.
/// </summary>
public class HelpCenterViewModel
{
    public string Query { get; set; }

    public string Category { get; set; }

    public IReadOnlyList<(string Category, int Count)> Categories { get; set; } = [];

    public IReadOnlyList<KnowledgeArticleIndex> Articles { get; set; } = [];
}
