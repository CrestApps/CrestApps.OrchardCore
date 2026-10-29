using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Services;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Webhooks;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Closes AI chat sessions: marks the session closed, or abandoned when the customer never said anything, runs the
/// profile's post-session analysis, rewrites the contact's memory, and triggers the <c>AI Chat Session Closed</c>
/// workflow event. The inactivity close and the end of a call both close sessions through it.
/// </summary>
public sealed class AIChatSessionCloser : IAIChatSessionCloser
{
    private readonly IAIChatSessionStore _sessionStore;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAIProfileManager _profileManager;
    private readonly AIChatSessionPostCloseProcessor _postCloseProcessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionCloser"/> class.
    /// </summary>
    /// <param name="sessionStore">The session store, which reads and saves sessions whoever started them.</param>
    /// <param name="promptStore">The prompt store.</param>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="postCloseProcessor">The post-session analysis.</param>
    /// <param name="serviceProvider">The service provider, for the optional contact memory and workflows.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AIChatSessionCloser(
        IAIChatSessionStore sessionStore,
        IAIChatSessionPromptStore promptStore,
        IAIProfileManager profileManager,
        AIChatSessionPostCloseProcessor postCloseProcessor,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<AIChatSessionCloser> logger)
    {
        _sessionStore = sessionStore;
        _promptStore = promptStore;
        _profileManager = profileManager;
        _postCloseProcessor = postCloseProcessor;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> CloseAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        var chatSession = await _sessionStore.FindByIdAsync(sessionId, cancellationToken);

        if (chatSession is null || chatSession.Status != ChatSessionStatus.Active || string.IsNullOrEmpty(chatSession.ProfileId))
        {
            return false;
        }

        var profile = await _profileManager.FindByIdAsync(chatSession.ProfileId, cancellationToken);

        if (profile is null)
        {
            return false;
        }

        var prompts = await _promptStore.GetPromptsAsync(chatSession.SessionId);

        await CloseAsync(profile, chatSession, prompts, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Closed AI chat session '{SessionId}' of profile '{ProfileId}' as '{Status}' when its conversation ended.",
                chatSession.SessionId,
                profile.ItemId,
                chatSession.Status);
        }

        return true;
    }

    /// <summary>
    /// Closes a session that is known to be over, with its prompts already read.
    /// </summary>
    /// <param name="profile">The session's AI profile.</param>
    /// <param name="chatSession">The session.</param>
    /// <param name="prompts">The session's prompts.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task CloseAsync(AIProfile profile, AIChatSession chatSession, IReadOnlyList<AIChatSessionPrompt> prompts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(chatSession);

        prompts ??= [];

        chatSession.Status = GetClosedStatus(prompts);
        chatSession.ClosedAtUtc = _clock.UtcNow;

        if (_postCloseProcessor.QueueIfNeeded(profile, chatSession))
        {
            await _postCloseProcessor.ProcessAsync(profile, chatSession, prompts, cancellationToken);
        }
        else
        {
            chatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.None;
        }

        await _sessionStore.SaveAsync(chatSession, cancellationToken);

        await RememberAsync(profile, chatSession, prompts, cancellationToken);

        await NotifyClosedAsync(profile, chatSession, cancellationToken);

        await TriggerSessionClosedWorkflowAsync(profile, chatSession);
    }

    /// <summary>
    /// A conversation the customer took part in is closed; one they never spoke in is abandoned.
    /// </summary>
    /// <param name="prompts">The session's prompts.</param>
    internal static ChatSessionStatus GetClosedStatus(IEnumerable<AIChatSessionPrompt> prompts)
        => (prompts ?? []).Any(prompt => prompt.Role == ChatRole.User)
            ? ChatSessionStatus.Closed
            : ChatSessionStatus.Abandoned;

    /// <summary>
    /// Adds the transcript and variables every AI session workflow event carries.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="input">The workflow input.</param>
    /// <param name="chatSession">The session.</param>
    internal static async Task AddTranscriptAsync(IServiceProvider serviceProvider, Dictionary<string, object> input, AIChatSession chatSession)
    {
        var promptStore = serviceProvider.GetService<IAIChatSessionPromptStore>();

        AIConversationTranscript.AddTo(
            input,
            chatSession,
            promptStore is null ? null : await promptStore.GetPromptsAsync(chatSession.SessionId));
    }

    // Rewrites the contact's memory note from the conversation that just ended, when the profile saves memory.
    private async Task RememberAsync(AIProfile profile, AIChatSession chatSession, IReadOnlyList<AIChatSessionPrompt> prompts, CancellationToken cancellationToken)
    {
        try
        {
            await _serviceProvider.GetRequiredService<AIContactMemoryService>().RememberSessionAsync(profile, chatSession, prompts, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Updating the contact memory from session '{SessionId}' failed.", chatSession.SessionId);
        }
    }

    // Lets other features act on the closed conversation, such as writing its extracted fields to the contact.
    private async Task NotifyClosedAsync(AIProfile profile, AIChatSession chatSession, CancellationToken cancellationToken)
    {
        foreach (var handler in _serviceProvider.GetServices<IAIConversationClosedHandler>())
        {
            try
            {
                await handler.ClosedAsync(profile, chatSession, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A closed-conversation handler failed for session '{SessionId}'.", chatSession.SessionId);
            }
        }
    }

    private async Task TriggerSessionClosedWorkflowAsync(AIProfile profile, AIChatSession chatSession)
    {
        await AIWebhookPublisher.TryPublishAsync(_serviceProvider, AIWebhookEvents.ConversationEnded, new
        {
            sessionId = chatSession.SessionId,
            profileId = profile.ItemId,
            closedUtc = chatSession.ClosedAtUtc,
            variables = chatSession.GetConversationVariables(),
        });

        var workflowManager = _serviceProvider.GetService<IWorkflowManager>();

        if (workflowManager is null)
        {
            return;
        }

        try
        {
            var input = new Dictionary<string, object>
            {
                { "SessionId", chatSession.SessionId },
                { "ProfileId", profile.ItemId },
                { "Session", chatSession },
                { "Profile", profile },
                { "ClosedAtUtc", chatSession.ClosedAtUtc },
            };

            await AddTranscriptAsync(_serviceProvider, input, chatSession);

            await workflowManager.TriggerEventAsync(
                nameof(AIChatSessionClosedEvent),
                input,
                correlationId: chatSession.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger AIChatSessionClosedEvent for session '{SessionId}'.", chatSession.SessionId);
        }
    }
}
