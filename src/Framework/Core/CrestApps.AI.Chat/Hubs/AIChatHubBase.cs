using System.Text;
using System.Threading.Channels;
using CrestApps.AI.Chat.Models;
using CrestApps.AI.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.AI.Chat.Hubs;

/// <summary>
/// Base SignalR hub for AI chat sessions. Provides streaming message delivery,
/// session loading, and message rating. Subclass in your application to add
/// authorization, citation collection, or other app-specific logic.
/// </summary>
public abstract class AIChatHubBase : Hub<IAIChatHubClient>
{
    protected AIChatHubBase(
        IAIProfileManager profileManager,
        IAIChatSessionManager sessionManager,
        IAIChatSessionPromptStore promptStore,
        IOrchestratorResolver orchestratorResolver,
        IOrchestrationContextBuilder orchestrationContextBuilder,
        ILogger logger)
    {
        ProfileManager = profileManager;
        SessionManager = sessionManager;
        PromptStore = promptStore;
        OrchestratorResolver = orchestratorResolver;
        OrchestrationContextBuilder = orchestrationContextBuilder;
        Logger = logger;
    }

    protected IAIProfileManager ProfileManager { get; }

    protected IAIChatSessionManager SessionManager { get; }

    protected IAIChatSessionPromptStore PromptStore { get; }

    protected IOrchestratorResolver OrchestratorResolver { get; }

    protected IOrchestrationContextBuilder OrchestrationContextBuilder { get; }

    protected ILogger Logger { get; }

    public virtual async Task LoadSession(string sessionId)
    {
        try
        {
            var session = await SessionManager.FindByIdAsync(sessionId);

            if (session == null)
            {
                await Clients.Caller.ReceiveError("Session not found.");
                return;
            }

            var prompts = await PromptStore.GetPromptsAsync(sessionId);
            var messages = prompts.Where(p => !p.IsGeneratedPrompt).Select(p => new
            {
                p.ItemId,
                Role = p.Role.Value,
                p.Content,
                p.Title,
                p.IsGeneratedPrompt,
                p.UserRating,
                References = p.References ?? new Dictionary<string, AICompletionReference>(),
            }).ToArray();

            await Clients.Caller.LoadSession(new
            {
                session.SessionId,
                session.ProfileId,
                session.Title,
                session.Status,
                Messages = messages,
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading session '{SessionId}'.", sessionId);
            await Clients.Caller.ReceiveError("Failed to load session.");
        }
    }

    public virtual ChannelReader<CompletionPartialMessage> SendMessage(string sessionId, string messageText, string[] fileNames)
    {
        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();

        _ = ProcessSendMessageAsync(channel.Writer, sessionId, messageText, fileNames);

        return channel.Reader;
    }

    protected virtual async Task ProcessSendMessageAsync(
        ChannelWriter<CompletionPartialMessage> writer,
        string sessionId,
        string messageText,
        string[] fileNames)
    {
        var cancellationToken = Context.ConnectionAborted;

        try
        {
            var session = await SessionManager.FindByIdAsync(sessionId);

            if (session == null)
            {
                Logger.LogWarning("Session '{SessionId}' not found.", sessionId);
                await Clients.Caller.ReceiveError("Session not found.");
                writer.Complete();
                return;
            }

            var profile = await ProfileManager.FindByIdAsync(session.ProfileId);

            if (profile == null)
            {
                Logger.LogWarning("Profile '{ProfileId}' not found for session '{SessionId}'.", session.ProfileId, sessionId);
                await Clients.Caller.ReceiveError("AI profile not found for this session.");
                writer.Complete();
                return;
            }

            if (string.IsNullOrEmpty(profile.Source))
            {
                Logger.LogWarning("Profile '{ProfileId}' has no provider (Source) configured.", profile.ItemId);
                await Clients.Caller.ReceiveError("This AI profile does not have a provider configured. Please edit the profile and select a provider.");
                writer.Complete();
                return;
            }

            var orchestrator = OrchestratorResolver.Resolve(profile.OrchestratorName);

            if (orchestrator == null)
            {
                Logger.LogWarning("No orchestrator resolved for profile '{ProfileId}' (orchestrator name: '{OrchestratorName}').", profile.ItemId, profile.OrchestratorName);
                await Clients.Caller.ReceiveError("No orchestrator is configured for this AI profile.");
                writer.Complete();
                return;
            }

            var userPrompt = new AIChatSessionPrompt
            {
                ItemId = Guid.NewGuid().ToString("N"),
                SessionId = sessionId,
                Role = ChatRole.User,
                Content = messageText,
                CreatedUtc = DateTime.UtcNow,
            };
            await PromptStore.CreateAsync(userPrompt);

            var existingPrompts = await PromptStore.GetPromptsAsync(sessionId);
            var chatMessages = new List<ChatMessage>();

            foreach (var prompt in existingPrompts)
            {
                chatMessages.Add(new ChatMessage(prompt.Role, prompt.Content));
            }

            var orchestrationContext = await OrchestrationContextBuilder.BuildAsync(profile, ctx =>
            {
                ctx.ConversationHistory = chatMessages;
                ctx.UserMessage = messageText;
            });

            var messageId = Guid.NewGuid().ToString("N");
            var fullResponse = new StringBuilder();

            await foreach (var chunk in orchestrator.ExecuteStreamingAsync(orchestrationContext, cancellationToken))
            {
                var text = chunk.Text;

                if (!string.IsNullOrEmpty(text))
                {
                    fullResponse.Append(text);
                    await writer.WriteAsync(new CompletionPartialMessage
                    {
                        MessageId = messageId,
                        Content = text,
                        SessionId = sessionId,
                    }, cancellationToken);
                }
            }

            var assistantPrompt = new AIChatSessionPrompt
            {
                ItemId = messageId,
                SessionId = sessionId,
                Role = ChatRole.Assistant,
                Content = fullResponse.ToString(),
                CreatedUtc = DateTime.UtcNow,
            };
            await PromptStore.CreateAsync(assistantPrompt);
            await PromptStore.SaveChangesAsync();

            await OnMessageCompletedAsync(session, profile, messageId, fullResponse.ToString(), cancellationToken);

            writer.Complete();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing message for session '{SessionId}'.", sessionId);
            await Clients.Caller.ReceiveError("An error occurred processing your message.");
            writer.Complete(ex);
        }
    }

    public virtual async Task RateMessage(string sessionId, string messageId, bool? rating)
    {
        try
        {
            var prompt = await PromptStore.FindByIdAsync(messageId);

            if (prompt != null)
            {
                prompt.UserRating = rating;
                await PromptStore.UpdateAsync(prompt);
                await PromptStore.SaveChangesAsync();
                await Clients.Caller.MessageRated(messageId, rating);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error rating message '{MessageId}'.", messageId);
        }
    }

    /// <summary>
    /// Called after a complete assistant response has been saved. Override to add
    /// post-processing such as citation collection, analytics, or title generation.
    /// </summary>
    protected virtual Task OnMessageCompletedAsync(
        AIChatSession session,
        AIProfile profile,
        string messageId,
        string fullResponse,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
