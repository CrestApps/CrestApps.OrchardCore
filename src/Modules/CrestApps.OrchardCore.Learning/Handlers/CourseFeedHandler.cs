using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Feeds.Models;

namespace CrestApps.OrchardCore.Learning.Handlers;

/// <summary>
/// Turns off the RSS feed Orchard Core offers for every List part, for courses. That feed writes the body of each
/// contained item without asking whether the reader may view it, so a course feed would hand out its lessons.
/// </summary>
/// <remarks>
/// A content handler, not a part handler: content handlers run after the part handlers, so this runs after the List
/// part's feed handler has read its own setting.
/// </remarks>
internal sealed class CourseFeedHandler : ContentHandlerBase
{
    /// <inheritdoc />
    public override Task GetContentItemAspectAsync(ContentItemAspectContext context)
        => context.ContentItem.Has(nameof(CoursePart))
            ? context.ForAsync<FeedMetadata>(metadata =>
            {
                metadata.DisableRssFeed = true;

                return Task.CompletedTask;
            })
            : Task.CompletedTask;
}
