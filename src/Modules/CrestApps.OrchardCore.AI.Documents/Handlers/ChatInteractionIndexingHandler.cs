using CrestApps.Core.AI.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Documents.Handlers;

internal sealed class ChatInteractionIndexingHandler : CatalogEntryHandlerBase<ChatInteraction>
{
    private readonly IIndexingTaskManager _indexingTaskManager;

    private readonly HashSet<string> _updatedIndexIds = [];
    private readonly HashSet<string> _deletedIndexIds = [];

    public ChatInteractionIndexingHandler(IIndexingTaskManager indexingTaskManager)
    {
        _indexingTaskManager = indexingTaskManager;
    }

    public override Task CreatedAsync(
        CreatedContext<ChatInteraction> context,
        CancellationToken cancellationToken = default)
    {
        if (!_updatedIndexIds.Add(context.Model.ItemId))
        {
            return Task.CompletedTask;
        }

        return _indexingTaskManager.CreateTaskAsync(new CreateIndexingTaskContext(context.Model.ItemId, AIConstants.AIDocumentsIndexingTaskType, RecordIndexingTaskTypes.Update));
    }

    public override async Task UpdatedAsync(
        UpdatedContext<ChatInteraction> context,
        CancellationToken cancellationToken = default)
    {
        if (!_updatedIndexIds.Add(context.Model.ItemId))
        {
            return;
        }

        await _indexingTaskManager.CreateTaskAsync(new CreateIndexingTaskContext(context.Model.ItemId, AIConstants.AIDocumentsIndexingTaskType, RecordIndexingTaskTypes.Update));
    }

    public override Task DeletedAsync(
        DeletedContext<ChatInteraction> context,
        CancellationToken cancellationToken = default)
    {
        if (!_deletedIndexIds.Add(context.Model.ItemId))
        {
            return Task.CompletedTask;
        }

        return _indexingTaskManager.CreateTaskAsync(new CreateIndexingTaskContext(context.Model.ItemId, AIConstants.AIDocumentsIndexingTaskType, RecordIndexingTaskTypes.Delete));
    }
}
