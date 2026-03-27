using CrestApps.OrchardCore.AI.Memory.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.AI.Memory.Handlers;

internal sealed class AIMemoryEntryHandler : CatalogEntryHandlerBase<AIMemoryEntry>
{
    private readonly Dictionary<string, AIMemoryEntry> _memories = [];
    private readonly HashSet<string> _removedMemoryIds = [];
    private bool _taskAdded;

    public override Task CreatedAsync(CreatedContext<AIMemoryEntry> context)
    {
        AddDeferredTask();
        _removedMemoryIds.Remove(context.Model.ItemId);
        _memories[context.Model.ItemId] = context.Model;

        return Task.CompletedTask;
    }

    public override Task UpdatedAsync(UpdatedContext<AIMemoryEntry> context)
    {
        AddDeferredTask();
        _removedMemoryIds.Remove(context.Model.ItemId);
        _memories[context.Model.ItemId] = context.Model;

        return Task.CompletedTask;
    }

    public override Task DeletedAsync(DeletedContext<AIMemoryEntry> context)
    {
        if (string.IsNullOrEmpty(context.Model.ItemId))
        {
            return Task.CompletedTask;
        }

        AddDeferredTask();
        _memories.Remove(context.Model.ItemId);
        _removedMemoryIds.Add(context.Model.ItemId);

        return Task.CompletedTask;
    }

    private void AddDeferredTask()
    {
        if (_taskAdded)
        {
            return;
        }

        _taskAdded = true;

        var memories = _memories;
        var removedMemoryIds = _removedMemoryIds;

        ShellScope.AddDeferredTask(scope => IndexAsync(scope, memories.Values, removedMemoryIds));
    }

    private static async Task IndexAsync(ShellScope scope, IEnumerable<AIMemoryEntry> memories, IEnumerable<string> removedMemoryIds)
    {
        var indexedMemories = memories.ToArray();
        var deletedMemoryIds = removedMemoryIds.ToArray();

        if (indexedMemories.Length == 0 && deletedMemoryIds.Length == 0)
        {
            return;
        }

        var indexingService = scope.ServiceProvider.GetRequiredService<AIMemoryIndexingService>();

        foreach (var memory in indexedMemories)
        {
            await indexingService.IndexAsync(memory);
        }

        if (deletedMemoryIds.Length > 0)
        {
            await indexingService.DeleteAsync(deletedMemoryIds);
        }
    }
}
