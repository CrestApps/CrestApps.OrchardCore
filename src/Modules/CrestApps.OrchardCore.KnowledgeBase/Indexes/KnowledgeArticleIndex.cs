using CrestApps.OrchardCore.KnowledgeBase.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.KnowledgeBase.Indexes;

/// <summary>
/// The published articles, for the help center's lists, its search and suggestions. Who may read each one is in
/// Content Access Control's <c>ContentAccessRoleIndex</c>, matched on the content item id (both hold only the published version).
/// </summary>
public sealed class KnowledgeArticleIndex : MapIndex
{
    public string ContentItemId { get; set; }

    public string Title { get; set; }

    public string Summary { get; set; }

    public string Category { get; set; }

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
                    Category = Cut(part.Category?.Trim(), 100),
                    PublishedUtc = item.PublishedUtc,
                    HelpfulCount = part.HelpfulCount,
                };
            });

    private static string Cut(string value, int length)
        => value?.Length > length ? value[..length] : value;
}
