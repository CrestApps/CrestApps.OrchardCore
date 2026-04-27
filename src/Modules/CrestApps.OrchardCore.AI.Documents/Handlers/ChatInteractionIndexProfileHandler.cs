using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Documents.Services;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Documents.Handlers;

/// <summary>
/// Handles events for chat interaction index profile.
/// </summary>
public sealed class ChatInteractionIndexProfileHandler : IndexProfileHandlerBase
{
    private readonly AIDocumentsIndexingService _indexingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionIndexProfileHandler"/> class.
    /// </summary>
    /// <param name="indexingService">The indexing service.</param>
    public ChatInteractionIndexProfileHandler(AIDocumentsIndexingService indexingService)
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
        return string.Equals(AIConstants.AIDocumentsIndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase);
    }
}
