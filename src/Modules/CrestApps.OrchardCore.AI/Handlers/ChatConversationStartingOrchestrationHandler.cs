using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.OrchardCore.AI.Core.Conversations;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// Runs the <c>AI Conversation Starting</c> handlers for a chat on the website or in the admin, once, before the
/// chat's first answer. Phone calls and text threads run them before their first message instead, and make their
/// conversation current, which this handler leaves alone.
/// </summary>
/// <remarks>
/// A chat has already started when its first message arrives, so only the variables apply: a chat cannot be
/// declined or moved to another profile from here.
/// </remarks>
internal sealed class ChatConversationStartingOrchestrationHandler : IOrchestrationContextBuilderHandler
{
    private readonly IAIConversationStartingService _startingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatConversationStartingOrchestrationHandler"/> class.
    /// </summary>
    /// <param name="startingService">Runs the starting handlers.</param>
    public ChatConversationStartingOrchestrationHandler(IAIConversationStartingService startingService)
    {
        _startingService = startingService;
    }

    /// <inheritdoc/>
    public async Task BuildingAsync(OrchestrationContextBuildingContext context, CancellationToken cancellationToken = default)
    {
        var session = AIInvocationScope.Current?.ChatSession;

        if (session is null || AIConversationScope.Current is not null || session.Has<AIConversationStartedMetadata>())
        {
            return;
        }

        session.Put(new AIConversationStartedMetadata { StartedUtc = DateTime.UtcNow });

        await _startingService.StartingAsync(new AIConversationContext
        {
            Channel = AIConversationChannels.Chat,
            Direction = AIConversationDirections.Inbound,
            Session = session,
            Profile = context.Resource as AIProfile,
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task BuiltAsync(OrchestrationContextBuiltContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
