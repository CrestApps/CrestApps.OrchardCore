using CrestApps.OrchardCore.Classifieds.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Classifieds.Indexes;

/// <summary>
/// Posted listings: their status, poster, term, upgrades and place, for moderation, a poster's own listings and a
/// search by distance. Every version is indexed, with whether it's the latest or the published one.
/// </summary>
public sealed class ClassifiedIndex : MapIndex
{
    public string ContentItemId { get; set; }

    public string ContentType { get; set; }

    public string Owner { get; set; }

    public string DisplayText { get; set; }

    public ClassifiedStatus Status { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public DateTime? FeaturedUntilUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was posted or last bumped, which listings are sorted by.
    /// </summary>
    public DateTime? SortUtc { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public bool Published { get; set; }

    public bool Latest { get; set; }
}

public sealed class ClassifiedIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
        => context.For<ClassifiedIndex>()
            .When(item => item.Has<ClassifiedPart>())
            .Map(item =>
            {
                item.TryGet<ClassifiedPart>(out var part);

                return new ClassifiedIndex
                {
                    ContentItemId = item.ContentItemId,
                    ContentType = item.ContentType,
                    Owner = item.Owner,
                    DisplayText = item.DisplayText?.Length > 255 ? item.DisplayText[..255] : item.DisplayText,
                    Status = part.Status,
                    ExpiresUtc = part.ExpiresUtc,
                    FeaturedUntilUtc = part.FeaturedUntilUtc,
                    SortUtc = part.BumpedUtc ?? part.PostedUtc ?? item.CreatedUtc,
                    Latitude = part.Latitude,
                    Longitude = part.Longitude,
                    Published = item.Published,
                    Latest = item.Latest,
                };
            });
}
