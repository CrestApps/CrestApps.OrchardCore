using CrestApps.Core;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Adds what the AI remembers about the contact to the system message, when the profile uses contact memory and the
/// conversation is with a known contact.
/// </summary>
public sealed class AIContactMemoryCompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    private readonly AIContactMemoryService _memoryService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIContactMemoryCompletionContextBuilderHandler"/> class.
    /// </summary>
    /// <param name="memoryService">The contact memory service.</param>
    /// <param name="logger">The logger.</param>
    public AIContactMemoryCompletionContextBuilderHandler(
        AIContactMemoryService memoryService,
        ILogger<AIContactMemoryCompletionContextBuilderHandler> logger)
    {
        _memoryService = memoryService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task BuildingAsync(AICompletionContextBuildingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task BuiltAsync(AICompletionContextBuiltContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Resource is not AIProfile profile ||
            !profile.GetSettings<AIProfileContactMemorySettings>().UseMemory)
        {
            return;
        }

        var contactId = AIConversationScope.Resolve()?.Contact?.ContentItemId;

        if (string.IsNullOrEmpty(contactId))
        {
            return;
        }

        try
        {
            context.Context.SystemMessage = AIContactMemoryService.AppendTo(context.Context.SystemMessage, await _memoryService.GetAsync(contactId));
        }
        catch (Exception ex)
        {
            // The conversation goes on without the memory rather than failing.
            _logger.LogWarning(ex, "The memory of contact '{ContactId}' could not be read.", contactId);
        }
    }
}
