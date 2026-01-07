using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

internal sealed class ChatInteractionIndexingHandler : CatalogEntryHandlerBase<ChatInteraction>
{
    private readonly IIndexingTaskManager _indexingTaskManager;

    private readonly HashSet<string> _updatedIndexIds = [];
    private readonly HashSet<string> _deletedIndexIds = [];

    public ChatInteractionIndexingHandler(IIndexingTaskManager indexingTaskManager)
    {
        _indexingTaskManager = indexingTaskManager;
    }

    public override Task CreatedAsync(CreatedContext<ChatInteraction> context)
    {
        if (!_updatedIndexIds.Add(context.Model.ItemId))
        {
            return Task.CompletedTask;
        }

        return _indexingTaskManager.CreateTaskAsync(new CreateIndexingTaskContext(context.Model.ItemId, ChatInteractionsConstants.IndexingTaskType, RecordIndexingTaskTypes.Update));
    }

    public override async Task UpdatedAsync(UpdatedContext<ChatInteraction> context)
    {
        if (!_updatedIndexIds.Add(context.Model.ItemId))
        {
            return;
        }

        await _indexingTaskManager.CreateTaskAsync(new CreateIndexingTaskContext(context.Model.ItemId, ChatInteractionsConstants.IndexingTaskType, RecordIndexingTaskTypes.Update));
    }

    public override Task DeletedAsync(DeletedContext<ChatInteraction> context)
    {
        if (!_deletedIndexIds.Add(context.Model.ItemId))
        {
            return Task.CompletedTask;
        }

        return _indexingTaskManager.CreateTaskAsync(new CreateIndexingTaskContext(context.Model.ItemId, ChatInteractionsConstants.IndexingTaskType, RecordIndexingTaskTypes.Delete));
    }
}
