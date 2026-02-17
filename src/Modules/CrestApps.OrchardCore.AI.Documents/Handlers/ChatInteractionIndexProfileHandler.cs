using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Documents.Services;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Documents.Handlers;

public sealed class ChatInteractionIndexProfileHandler : IndexProfileHandlerBase
{
    private readonly ChatInteractionIndexingService _indexingService;

    public ChatInteractionIndexProfileHandler(ChatInteractionIndexingService indexingService)
    {
        _indexingService = indexingService;
    }

    public override async Task SynchronizedAsync(IndexProfileSynchronizedContext context)
    {
        if (!CanHandle(context.IndexProfile))
        {
            return;
        }

        await _indexingService.ProcessRecordsAsync([context.IndexProfile.Id]);
    }

    private static bool CanHandle(IndexProfile indexProfile)
    {
        return string.Equals(ChatInteractionsConstants.IndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase);
    }
}
