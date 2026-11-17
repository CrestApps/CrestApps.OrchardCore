using OrchardCore.Sitemaps.Models;

namespace CrestApps.OrchardCore.AI.Agent.Sitemaps;

/// <summary>
/// Builds what the sitemap tools return for a sitemap and its sources, from an explicit list of fields.
/// </summary>
internal static class SitemapContent
{
    /// <summary>
    /// The name the tools give a source that lists content items by content type.
    /// </summary>
    public const string ContentTypesSource = "contentTypes";

    /// <summary>
    /// The name the tools give a source that is one custom path.
    /// </summary>
    public const string CustomPathSource = "customPath";

    /// <summary>
    /// The name the tools give the source of a sitemap index, which holds the sitemaps the index contains.
    /// </summary>
    public const string ContainedSitemapsSource = "containedSitemaps";

    /// <summary>
    /// The name the tools give a source of a kind they do not know.
    /// </summary>
    public const string OtherSource = "other";

    /// <summary>
    /// The kind of a sitemap that lists pages.
    /// </summary>
    public const string SitemapKind = "sitemap";

    /// <summary>
    /// The kind of a sitemap that lists other sitemaps.
    /// </summary>
    public const string SitemapIndexKind = "sitemapIndex";

    /// <summary>
    /// Gets the kind of a sitemap: a sitemap, or a sitemap index. The index class is in the Sitemaps module, so anything
    /// that is not a sitemap is an index.
    /// </summary>
    /// <param name="sitemap">The sitemap.</param>
    /// <returns>The kind.</returns>
    public static string GetKind(SitemapType sitemap)
    {
        return sitemap is Sitemap
            ? SitemapKind
            : SitemapIndexKind;
    }

    /// <summary>
    /// Gets the fields a sitemap tool returns for one sitemap.
    /// </summary>
    /// <param name="sitemap">The sitemap.</param>
    /// <returns>The fields of the sitemap.</returns>
    public static object Describe(SitemapType sitemap)
    {
        ArgumentNullException.ThrowIfNull(sitemap);

        return new
        {
            sitemapId = sitemap.SitemapId,
            kind = GetKind(sitemap),
            name = sitemap.Name,
            path = sitemap.Path,
            enabled = sitemap.Enabled,
            sources = sitemap.SitemapSources.Select(source => DescribeSource(sitemap, source)),
        };
    }

    /// <summary>
    /// Reads a change frequency as the tools write it: always, hourly, daily, weekly, monthly, yearly or never.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="changeFrequency">The change frequency.</param>
    /// <returns><see langword="false"/> when the text is not a change frequency.</returns>
    public static bool TryParseChangeFrequency(string text, out ChangeFrequency changeFrequency)
    {
        ChangeFrequency? parsed = text?.Trim().ToUpperInvariant() switch
        {
            "ALWAYS" => ChangeFrequency.Always,
            "HOURLY" => ChangeFrequency.Hourly,
            "DAILY" => ChangeFrequency.Daily,
            "WEEKLY" => ChangeFrequency.Weekly,
            "MONTHLY" => ChangeFrequency.Monthly,
            "YEARLY" => ChangeFrequency.Yearly,
            "NEVER" => ChangeFrequency.Never,
            _ => null,
        };

        changeFrequency = parsed ?? ChangeFrequency.Daily;

        return parsed.HasValue;
    }

    private static object DescribeSource(SitemapType sitemap, SitemapSource source)
    {
        if (source is ContentTypesSitemapSource contentTypes)
        {
            return new
            {
                sourceId = contentTypes.Id,
                type = ContentTypesSource,
                indexAll = contentTypes.IndexAll,
                limitItems = contentTypes.LimitItems,
                changeFrequency = Format(contentTypes.ChangeFrequency),
                priority = contentTypes.Priority,
                contentTypes = contentTypes.ContentTypes.Select(entry => new
                {
                    contentType = entry.ContentTypeName,
                    changeFrequency = Format(entry.ChangeFrequency),
                    priority = entry.Priority,
                }),
                limitedContentType = contentTypes.LimitItems && contentTypes.LimitedContentType is not null
                    ? new
                    {
                        contentType = contentTypes.LimitedContentType.ContentTypeName,
                        changeFrequency = Format(contentTypes.LimitedContentType.ChangeFrequency),
                        priority = contentTypes.LimitedContentType.Priority,
                        skip = contentTypes.LimitedContentType.Skip,
                        take = contentTypes.LimitedContentType.Take,
                    }
                    : null,
            };
        }

        if (source is CustomPathSitemapSource customPath)
        {
            return new
            {
                sourceId = customPath.Id,
                type = CustomPathSource,
                path = customPath.Path,
                changeFrequency = Format(customPath.ChangeFrequency),
                priority = customPath.Priority,
            };
        }

        // Any other source is a class this module cannot read: the one source of a sitemap index, which holds the
        // sitemaps it contains and is a class of the Sitemaps module, or a source another module adds.
        return new
        {
            sourceId = source.Id,
            type = sitemap is Sitemap
                ? OtherSource
                : ContainedSitemapsSource,
        };
    }

    private static string Format(ChangeFrequency changeFrequency)
        => changeFrequency.ToString().ToLowerInvariant();
}
