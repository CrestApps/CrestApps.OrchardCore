using System.Text.Json.Nodes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.KnowledgeBase.Indexes;

/// <summary>
/// The published articles, for the help center's lists, its search and suggestions.
/// </summary>
public sealed class KnowledgeArticleIndex : MapIndex
{
    public string ContentItemId { get; set; }

    public string Title { get; set; }

    public string Summary { get; set; }

    public string CategoryId { get; set; }

    public KnowledgeArticleAudience Audience { get; set; }

    public DateTime? PublishedUtc { get; set; }

    public int HelpfulCount { get; set; }
}

/// <summary>
/// Maps published articles to <see cref="KnowledgeArticleIndex"/>.
/// </summary>
public sealed class KnowledgeArticleIndexProvider : IndexProvider<ContentItem>
{
    /// <summary>
    /// The longest title and summary kept in the index.
    /// </summary>
    public const int MaxTitleLength = 255;

    /// <inheritdoc cref="MaxTitleLength"/>
    public const int MaxSummaryLength = 1000;

    public override void Describe(DescribeContext<ContentItem> context)
        => context.For<KnowledgeArticleIndex>()
            .When(item => item.Published && item.Has<KnowledgeArticlePart>())
            .Map(item =>
            {
                var part = item.GetOrCreate<KnowledgeArticlePart>();

                return new KnowledgeArticleIndex
                {
                    ContentItemId = item.ContentItemId,
                    Title = Cut(item.DisplayText, MaxTitleLength),
                    Summary = Cut(part.Summary, MaxSummaryLength),
                    CategoryId = CategoryOf(item),
                    Audience = part.Audience,
                    PublishedUtc = item.PublishedUtc,
                    HelpfulCount = part.HelpfulCount,
                };
            });

    /// <summary>
    /// Gets the term an article's category taxonomy field holds (the field of its type, <see
    /// cref="KnowledgeBaseConstants.CategoryField"/>), or <see langword="null"/>.
    /// </summary>
    /// <param name="item">The article.</param>
    /// <returns>The term's content item id.</returns>
    public static string CategoryOf(ContentItem item)
        => ((JsonObject)item?.Content)?[item.ContentType]?[KnowledgeBaseConstants.CategoryField]?["TermContentItemIds"] is JsonArray { Count: > 0 } terms
            ? terms[0]?.GetValue<string>()
            : null;

    private static string Cut(string value, int length)
        => value?.Length > length ? value[..length] : value;
}
