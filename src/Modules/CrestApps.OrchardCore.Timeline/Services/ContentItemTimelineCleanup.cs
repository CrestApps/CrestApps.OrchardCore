using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Timeline.Services;

/// <summary>
/// Deletes a content item's timeline, with the files attached to it, once the item is removed for good, such as a
/// contact removed for a privacy request. The removal asks for <see cref="TimelineDeletionStep"/> in its own transaction,
/// so the deletion runs after it commits and runs again when it fails.
/// </summary>
internal sealed class ContentItemTimelineCleanup : ContentHandlerBase
{
    private readonly ResumableSteps _steps;

    public ContentItemTimelineCleanup(ResumableSteps steps)
    {
        _steps = steps;
    }

    /// <inheritdoc/>
    public override Task RemovedAsync(RemoveContentContext context)
    {
        // Removing one version (a draft) leaves the item; only an item with no version left loses its timeline.
        return context.NoActiveVersionLeft
            ? TimelineDeletionStep.RequestAsync(_steps, new RecordRef(TimelineConstants.ContentItemKind, context.ContentItem.ContentItemId))
            : Task.CompletedTask;
    }
}
