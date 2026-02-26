using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.AI.DataSources.Handlers;

/// <summary>
/// Listens for content item changes (create, update, publish, remove) and triggers
/// real-time re-indexing of affected documents in the AI Knowledge Base index.
/// This ensures the KB index stays synchronized with the source content indexes.
/// </summary>
internal sealed class DataSourceContentHandler : ContentHandlerBase
{
    private readonly Dictionary<string, ContentItem> _updatedContentItems = [];
    private readonly HashSet<string> _removedContentItemIds = [];

    private bool _taskAdded;

    public override Task PublishedAsync(PublishContentContext context)
        => TrackUpdatedAsync(context.ContentItem);

    public override Task CreatedAsync(CreateContentContext context)
        => TrackUpdatedAsync(context.ContentItem);

    public override Task UpdatedAsync(UpdateContentContext context)
        => TrackUpdatedAsync(context.ContentItem);

    public override Task UnpublishedAsync(PublishContentContext context)
        => TrackUpdatedAsync(context.ContentItem);

    public override Task RemovedAsync(RemoveContentContext context)
    {
        if (context.ContentItem.Id == 0)
        {
            return Task.CompletedTask;
        }

        AddDeferredTask();

        _removedContentItemIds.Add(context.ContentItem.ContentItemId);

        return Task.CompletedTask;
    }

    private Task TrackUpdatedAsync(ContentItem contentItem)
    {
        if (contentItem.Id == 0)
        {
            return Task.CompletedTask;
        }

        AddDeferredTask();

        _updatedContentItems[contentItem.ContentItemId] = contentItem;

        return Task.CompletedTask;
    }

    private void AddDeferredTask()
    {
        if (_taskAdded)
        {
            return;
        }

        _taskAdded = true;

        var updatedItems = _updatedContentItems;
        var removedIds = _removedContentItemIds;

        ShellScope.AddDeferredTask(scope => ProcessChangesAsync(scope, updatedItems, removedIds));
    }

    private static async Task ProcessChangesAsync(
        ShellScope scope,
        Dictionary<string, ContentItem> updatedItems,
        HashSet<string> removedIds)
    {
        if (updatedItems.Count == 0 && removedIds.Count == 0)
        {
            return;
        }

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DataSourceContentHandler>>();

        try
        {
            var indexingService = scope.ServiceProvider.GetRequiredService<DataSourceIndexingService>();

            // Re-index updated content items.
            if (updatedItems.Count > 0)
            {
                var contentItemIds = updatedItems.Keys.ToList();

                await indexingService.IndexDocumentsAsync(contentItemIds);
            }

            // Remove deleted content items from the KB index.
            if (removedIds.Count > 0)
            {
                await indexingService.RemoveDocumentsAsync(removedIds);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during real-time AI Knowledge Base index update.");
        }
    }
}
