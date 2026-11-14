using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Timeline.Services;

/// <summary>
/// Deletes a content item's timeline, with the files attached to it, once the item is removed for good, such as a
/// contact removed for a privacy request. The timeline service reaches the content manager (through the record directory
/// the mention handler reads), so the work runs after the request instead of from this handler's constructor.
/// </summary>
internal sealed class ContentItemTimelineCleanup : ContentHandlerBase
{
    /// <inheritdoc/>
    public override Task RemovedAsync(RemoveContentContext context)
    {
        // Removing one version (a draft) leaves the item; only an item with no version left loses its timeline.
        if (context.NoActiveVersionLeft && ShellScope.Current is not null)
        {
            var subject = new RecordRef(TimelineConstants.ContentItemKind, context.ContentItem.ContentItemId);

            ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<ITimelineService>().DeleteAllAsync(subject));
        }

        return Task.CompletedTask;
    }
}
