using System.Diagnostics;
using System.Threading.Channels;
using CrestApps.AI.Chat.Models;
using CrestApps.AI.Models;
using CrestApps.AI.Prompting.Rendering;
using CrestApps.AI.Prompting.Services;
using CrestApps.AI.Services;
using CrestApps.Extensions;
using CrestApps.OrchardCore.AI.Models;
using Cysharp.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TextToSpeechOptions = CrestApps.AI.Models.TextToSpeechOptions;

#pragma warning disable MEAI001 // Text-to-speech APIs from Microsoft.Extensions.AI are preview and require explicit opt-in at each usage site.
namespace CrestApps.AI.Chat.Hubs;

/// <summary>
/// Core SignalR hub for AI chat sessions. Provides streaming message delivery,
/// session management, message rating, handler transfer, conversation mode support,
/// and notification action dispatch.
/// <para>
/// All public hub methods are <c>virtual</c> so that framework-specific subclasses
/// (e.g., OrchardCore) can wrap each call with their own scoping or authorization
/// logic and then call the base implementation.
/// </para>
/// </summary>
public class AIChatHubCore<TClient> : Hub<TClient>
    where TClient : class, IAIChatHubClient
{
    private const string _conversationCtsKey = "ConversationCts";

    private readonly IServiceProvider _services;

    protected AIChatHubCore(
        IServiceProvider services,
        ILogger logger)
    {
        _services = services;
        Logger = logger;
    }

    protected ILogger Logger { get; }

    /// <summary>
    /// Executes an action within a service scope. Override in OrchardCore to use
    /// <c>ShellScope.UsingChildScopeAsync</c> so that each hub invocation gets
    /// its own <c>ISession</c> / <c>IDocumentStore</c> lifecycle.
    /// </summary>
    protected virtual Task ExecuteInScopeAsync(Func<IServiceProvider, Task> action)
        => action(_services);

    /// <summary>
    /// Gets the chat context type for this hub. Override when using a different
    /// chat context type (e.g., <see cref="ChatContextType.ChatInteraction"/>).
    /// </summary>
    protected virtual ChatContextType GetChatContextType()
        => ChatContextType.AIChatSession;

    /// <summary>
    /// Gets the current UTC time. Override to use a framework-specific time
    /// abstraction (e.g., <c>IClock</c>).
    /// </summary>
    protected virtual DateTime GetUtcNow()
        => DateTime.UtcNow;

    /// <summary>
    /// Generates a unique identifier. Override to use a framework-specific
    /// ID generator (e.g., OrchardCore's <c>IdGenerator</c>).
    /// </summary>
    protected virtual string GenerateId()
        => UniqueId.GenerateId();

    /// <summary>
    /// Returns the default blank session title used when no title can be determined.
    /// </summary>
    protected virtual string DefaultBlankSessionTitle => "Untitled";

    // ───────────────────────── Error message hooks ─────────────────────────

    protected virtual string GetRequiredFieldMessage(string fieldName)
        => $"{fieldName} is required.";

    protected virtual string GetProfileNotFoundMessage()
        => "Profile not found.";

    protected virtual string GetSessionNotFoundMessage()
        => "Session not found.";

    protected virtual string GetNotAuthorizedMessage()
        => "You are not authorized to interact with the given profile.";

    protected virtual string GetFriendlyErrorMessage(Exception ex)
        => "An error occurred processing your message.";

    protected virtual string GetOnlyChatProfilesMessage()
        => "Only chat profiles can start chat sessions.";

    protected virtual string GetConversationNotEnabledMessage()
        => "Conversation mode is not enabled for this profile.";

    protected virtual string GetNoSttDeploymentMessage()
        => "No speech-to-text deployment is configured.";

    protected virtual string GetNoTtsDeploymentMessage()
        => "No text-to-speech deployment is configured.";

    protected virtual string GetSttDeploymentNotFoundMessage()
        => "The configured speech-to-text deployment was not found.";

    protected virtual string GetTtsDeploymentNotFoundMessage()
        => "The configured text-to-speech deployment was not found.";

    protected virtual string GetTtsNotEnabledMessage()
        => "Text-to-speech is not enabled for this profile.";

    protected virtual string GetConversationErrorMessage()
        => "An error occurred during the conversation. Please try again.";

    protected virtual string GetNotificationActionErrorMessage()
        => "An error occurred while processing your action. Please try again.";

    protected virtual string GetTranscriptionErrorMessage()
        => "An error occurred while transcribing the audio. Please try again.";

    protected virtual string GetSpeechSynthesisErrorMessage()
        => "An error occurred while synthesizing speech. Please try again.";

    // ───────────────────────── Authorization hooks ─────────────────────────

    /// <summary>
    /// Checks whether the current caller is authorized to use the given profile.
    /// Override to perform framework-specific authorization checks.
    /// The default implementation always returns <c>true</c>.
    /// </summary>
    protected virtual Task<bool> AuthorizeProfileAsync(IServiceProvider services, AIProfile profile)
        => Task.FromResult(true);

    // ────────────────────── Post-completion hooks ──────────────────────

    /// <summary>
    /// Called after a streaming response has been fully collected and saved.
    /// Override to perform analytics, citation collection, or workflow triggers.
    /// </summary>
    protected virtual Task OnMessageCompletedAsync(
        IServiceProvider services,
        ChatMessageCompletedContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Collects references (citations) during streaming. Called after each chunk
    /// and once after the stream ends. Override to integrate citation collection.
    /// </summary>
    protected virtual void CollectStreamingReferences(
        IServiceProvider services,
        ChatResponseHandlerContext handlerContext,
        Dictionary<string, AICompletionReference> references,
        HashSet<string> contentItemIds)
    {
        // No-op. OC overrides to use CitationReferenceCollector.
    }

    // ───────────────── Session title generation ─────────────────

    /// <summary>
    /// Generates a title for a new session. The default implementation uses AI
    /// title generation when configured on the profile, falling back to a
    /// truncated user prompt.
    /// </summary>
    protected virtual async Task<string> GenerateSessionTitleAsync(
        IServiceProvider services,
        AIProfile profile,
        string userPrompt)
    {
        var titleUserPrompt = BuildTitleUserPrompt(profile, userPrompt);

        if (profile.TitleType == AISessionTitleType.Generated)
        {
            var generated = await GetAIGeneratedTitleAsync(services, profile, titleUserPrompt);

            if (!string.IsNullOrEmpty(generated))
            {
                return generated;
            }
        }

        return Truncate(titleUserPrompt, 255);
    }

    private static string Truncate(string value, int maxLength)
        => string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value
            : value[..maxLength];

    private static async Task<string> GetAIGeneratedTitleAsync(
        IServiceProvider services,
        AIProfile profile,
        string userPrompt)
    {
        var aiTemplateService = services.GetService<IAITemplateService>();
        var completionContextBuilder = services.GetService<IAICompletionContextBuilder>();
        var completionService = services.GetService<IAICompletionService>();

        if (aiTemplateService is null || completionContextBuilder is null || completionService is null)
        {
            return null;
        }

        var titleSystemMessage = await aiTemplateService.RenderAsync(AITemplateIds.TitleGeneration);

        var context = await completionContextBuilder.BuildAsync(profile, c =>
        {
            c.SystemMessage = titleSystemMessage;
            c.FrequencyPenalty = 0;
            c.PresencePenalty = 0;
            c.TopP = 1;
            c.Temperature = 0;
            c.MaxTokens = 64;
            c.DataSourceId = null;
            c.DisableTools = true;
        });

        var deploymentManager = services.GetService<IAIDeploymentManager>();

        if (deploymentManager is null)
        {
            return null;
        }

        var chatDeployment = await deploymentManager.ResolveUtilityOrDefaultAsync(
            utilityDeploymentId: context.UtilityDeploymentId,
            chatDeploymentId: context.ChatDeploymentId);

        if (chatDeployment is null)
        {
            return null;
        }

        var titleResponse = await completionService.CompleteAsync(chatDeployment,
        [
            new(ChatRole.User, userPrompt),
        ], context);

        return titleResponse.Messages.Count > 0
            ? Truncate(titleResponse.Messages.First().Text, 255)
            : null;
    }

    protected static string BuildTitleUserPrompt(AIProfile profile, string userPrompt)
    {
        var trimmedUserPrompt = userPrompt?.Trim();
        var profileMetadata = profile.As<AIProfileMetadata>();
        var initialPrompt = profileMetadata.InitialPrompt?.Trim();

        if (string.IsNullOrWhiteSpace(initialPrompt))
        {
            return trimmedUserPrompt;
        }

        return string.IsNullOrWhiteSpace(trimmedUserPrompt)
            ? initialPrompt
            : $"{initialPrompt}\n\n{trimmedUserPrompt}";
    }

    // ────────────── Session group management ──────────────

    /// <summary>
    /// Gets the SignalR group name for a chat session. Clients in this group
    /// receive deferred responses delivered via webhook or external callback.
    /// </summary>
    public static string GetSessionGroupName(string sessionId)
        => $"aichat-session-{sessionId}";

    // ───────────────── Deployment resolution ─────────────────

    /// <summary>
    /// Resolves the deployment settings for speech services. Override in
    /// OrchardCore to read from ISiteService instead of IOptionsMonitor.
    /// </summary>
    protected virtual Task<DefaultAIDeploymentSettings> GetDeploymentSettingsAsync(IServiceProvider services)
    {
        var options = services.GetService<IOptionsMonitor<DefaultAIDeploymentSettings>>();
        return Task.FromResult(options?.CurrentValue ?? new DefaultAIDeploymentSettings());
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PUBLIC HUB METHODS — all virtual for framework-specific overrides
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Streams a chat response for the given prompt. Creates a new session on the
    /// fly when <paramref name="sessionId"/> is empty.
    /// </summary>
    public virtual ChannelReader<CompletionPartialMessage> SendMessage(
        string profileId,
        string prompt,
        string sessionId,
        string sessionProfileId,
        CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();

        _ = ExecuteInScopeAsync(services =>
            HandleSendMessageAsync(channel.Writer, services, profileId, prompt, sessionId, sessionProfileId, cancellationToken));

        return channel.Reader;
    }

    /// <summary>
    /// Loads an existing session and sends its messages to the caller.
    /// Also joins the caller to the session's SignalR group for deferred responses.
    /// </summary>
    public virtual async Task LoadSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            await Clients.Caller.ReceiveError(GetRequiredFieldMessage(nameof(sessionId)));
            return;
        }

        await ExecuteInScopeAsync(async services =>
        {
            var sessionManager = services.GetRequiredService<IAIChatSessionManager>();
            var profileManager = services.GetRequiredService<IAIProfileManager>();
            var promptStore = services.GetRequiredService<IAIChatSessionPromptStore>();

            var chatSession = await sessionManager.FindAsync(sessionId);

            if (chatSession == null)
            {
                await Clients.Caller.ReceiveError(GetSessionNotFoundMessage());
                return;
            }

            var profile = await profileManager.FindByIdAsync(chatSession.ProfileId);

            if (profile is null)
            {
                await Clients.Caller.ReceiveError(GetProfileNotFoundMessage());
                return;
            }

            if (!await AuthorizeProfileAsync(services, profile))
            {
                await Clients.Caller.ReceiveError(GetNotAuthorizedMessage());
                return;
            }

            var prompts = await promptStore.GetPromptsAsync(chatSession.SessionId);

            await Groups.AddToGroupAsync(Context.ConnectionId, GetSessionGroupName(chatSession.SessionId));

            await Clients.Caller.LoadSession(CreateSessionPayload(chatSession, profile, prompts));
        });
    }

    /// <summary>
    /// Creates a new chat session for the given profile and returns it to the caller.
    /// </summary>
    public virtual async Task StartSession(string profileId, string initialResponseHandlerName = null)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            await Clients.Caller.ReceiveError(GetRequiredFieldMessage(nameof(profileId)));
            return;
        }

        await ExecuteInScopeAsync(async services =>
        {
            var sessionManager = services.GetRequiredService<IAIChatSessionManager>();
            var profileManager = services.GetRequiredService<IAIProfileManager>();
            var promptStore = services.GetRequiredService<IAIChatSessionPromptStore>();

            var profile = await profileManager.FindByIdAsync(profileId);

            if (profile is null)
            {
                await Clients.Caller.ReceiveError(GetProfileNotFoundMessage());
                return;
            }

            if (!await AuthorizeProfileAsync(services, profile))
            {
                await Clients.Caller.ReceiveError(GetNotAuthorizedMessage());
                return;
            }

            if (profile.Type != AIProfileType.Chat)
            {
                await Clients.Caller.ReceiveError(GetOnlyChatProfilesMessage());
                return;
            }

            var chatSession = await sessionManager.NewAsync(profile, new NewAIChatSessionContext());

            if (!string.IsNullOrWhiteSpace(initialResponseHandlerName))
            {
                chatSession.ResponseHandlerName = initialResponseHandlerName.Trim();
            }

            await sessionManager.SaveAsync(chatSession);
            var prompts = await promptStore.GetPromptsAsync(chatSession.SessionId);

            await Groups.AddToGroupAsync(Context.ConnectionId, GetSessionGroupName(chatSession.SessionId));

            await Clients.Caller.LoadSession(CreateSessionPayload(chatSession, profile, prompts));
        });
    }

    /// <summary>
    /// Rates a message as positive or negative. Toggling the same rating clears it.
    /// </summary>
    public virtual async Task RateMessage(string sessionId, string messageId, bool isPositive)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(messageId))
        {
            return;
        }

        await ExecuteInScopeAsync(async services =>
        {
            var sessionManager = services.GetRequiredService<IAIChatSessionManager>();
            var profileManager = services.GetRequiredService<IAIProfileManager>();
            var promptStore = services.GetRequiredService<IAIChatSessionPromptStore>();

            var chatSession = await sessionManager.FindAsync(sessionId);

            if (chatSession is null)
            {
                return;
            }

            var profile = await profileManager.FindByIdAsync(chatSession.ProfileId);

            if (profile is null)
            {
                return;
            }

            if (!await AuthorizeProfileAsync(services, profile))
            {
                return;
            }

            var prompt = (await promptStore.GetPromptsAsync(chatSession.SessionId))
                .FirstOrDefault(p => p.ItemId == messageId);

            if (prompt is null)
            {
                return;
            }

            prompt.UserRating = prompt.UserRating == isPositive ? null : isPositive;

            await promptStore.UpdateAsync(prompt);

            await OnMessageRatedAsync(services, chatSession, promptStore);

            await Clients.Caller.MessageRated(messageId, prompt.UserRating);
        });
    }

    /// <summary>
    /// Called after a message has been rated. Override to record analytics.
    /// </summary>
    protected virtual Task OnMessageRatedAsync(
        IServiceProvider services,
        AIChatSession chatSession,
        IAIChatSessionPromptStore promptStore)
        => Task.CompletedTask;

    /// <summary>
    /// Handles a user-initiated action on a chat notification system message.
    /// Dispatches to registered <see cref="IChatNotificationActionHandler"/> implementations.
    /// </summary>
    public virtual async Task HandleNotificationAction(string sessionId, string notificationType, string actionName)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(actionName))
        {
            return;
        }

        await ExecuteInScopeAsync(async services =>
        {
            try
            {
                var handler = services.GetKeyedService<IChatNotificationActionHandler>(actionName);

                if (handler is null)
                {
                    Logger.LogWarning("No notification action handler found for action '{ActionName}'.", actionName);
                    return;
                }

                var context = new ChatNotificationActionContext
                {
                    SessionId = sessionId,
                    NotificationType = notificationType,
                    ActionName = actionName,
                    ChatType = GetChatContextType(),
                    ConnectionId = Context.ConnectionId,
                    Services = services,
                };

                await handler.HandleAsync(context);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An error occurred while handling notification action '{ActionName}'.", actionName);

                try
                {
                    await Clients.Caller.ReceiveError(GetNotificationActionErrorMessage());
                }
                catch
                {
                    // Best-effort error reporting.
                }
            }
        });
    }

    /// <summary>
    /// Stops the current conversation by cancelling the conversation CTS.
    /// </summary>
    public virtual Task StopConversation()
    {
        if (Context.Items.TryGetValue(_conversationCtsKey, out var value) && value is CancellationTokenSource cts)
        {
            cts.Cancel();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts a real-time conversation with speech-to-text transcription and
    /// text-to-speech synthesis. The caller streams audio chunks and receives
    /// AI responses as both text tokens and synthesized audio.
    /// </summary>
    public virtual async Task StartConversation(
        string profileId,
        string sessionId,
        IAsyncEnumerable<string> audioChunks,
        string audioFormat = null,
        string language = null)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            await Clients.Caller.ReceiveError(GetRequiredFieldMessage(nameof(profileId)));
            return;
        }

        var cancellationToken = Context.ConnectionAborted;

        try
        {
            await ExecuteInScopeAsync(async services =>
            {
                var profileManager = services.GetRequiredService<IAIProfileManager>();
                var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();
                var clientFactory = services.GetRequiredService<IAIClientFactory>();

                var profile = await profileManager.FindByIdAsync(profileId);

                if (profile is null)
                {
                    await Clients.Caller.ReceiveError(GetProfileNotFoundMessage());
                    return;
                }

                if (!await AuthorizeProfileAsync(services, profile))
                {
                    await Clients.Caller.ReceiveError(GetNotAuthorizedMessage());
                    return;
                }

                if (!profile.TryGetSettings<ChatModeProfileSettings>(out var chatModeSettings)
                    || chatModeSettings.ChatMode != ChatMode.Conversation)
                {
                    await Clients.Caller.ReceiveError(GetConversationNotEnabledMessage());
                    return;
                }

                var deploymentSettings = await GetDeploymentSettingsAsync(services);

                if (string.IsNullOrEmpty(deploymentSettings.DefaultSpeechToTextDeploymentId))
                {
                    await Clients.Caller.ReceiveError(GetNoSttDeploymentMessage());
                    return;
                }

                if (string.IsNullOrEmpty(deploymentSettings.DefaultTextToSpeechDeploymentId))
                {
                    await Clients.Caller.ReceiveError(GetNoTtsDeploymentMessage());
                    return;
                }

                var sttDeployment = await deploymentManager.FindByIdAsync(deploymentSettings.DefaultSpeechToTextDeploymentId);

                if (sttDeployment is null)
                {
                    await Clients.Caller.ReceiveError(GetSttDeploymentNotFoundMessage());
                    return;
                }

                var ttsDeployment = await deploymentManager.FindByIdAsync(deploymentSettings.DefaultTextToSpeechDeploymentId);

                if (ttsDeployment is null)
                {
                    await Clients.Caller.ReceiveError(GetTtsDeploymentNotFoundMessage());
                    return;
                }

                using var sttClient = await clientFactory.CreateSpeechToTextClientAsync(sttDeployment);
                using var ttsClient = await clientFactory.CreateTextToSpeechClientAsync(ttsDeployment);

                var effectiveVoiceName = !string.IsNullOrWhiteSpace(chatModeSettings.VoiceName)
                    ? chatModeSettings.VoiceName
                    : deploymentSettings.DefaultTextToSpeechVoiceId;

                var speechLanguage = !string.IsNullOrWhiteSpace(language) ? language : "en-US";

                using var conversationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Context.Items[_conversationCtsKey] = conversationCts;

                try
                {
                    await RunConversationLoopAsync(
                        profile, sessionId, audioChunks, audioFormat, speechLanguage,
                        sttClient, ttsClient, effectiveVoiceName, services, conversationCts.Token);
                }
                finally
                {
                    Context.Items.Remove(_conversationCtsKey);
                }
            });
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                Logger.LogDebug("Conversation was cancelled.");
                return;
            }

            Logger.LogError(ex, "An error occurred during conversation mode.");

            try
            {
                await Clients.Caller.ReceiveError(GetConversationErrorMessage());
            }
            catch (Exception writeEx)
            {
                Logger.LogWarning(writeEx, "Failed to write conversation error message.");
            }
        }
    }

    /// <summary>
    /// Streams audio chunks for speech-to-text transcription. Returns partial
    /// and final transcripts to the caller as they are produced.
    /// </summary>
    public virtual async Task SendAudioStream(
        string profileId,
        string sessionId,
        IAsyncEnumerable<string> audioChunks,
        string audioFormat = null,
        string language = null)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            await Clients.Caller.ReceiveError(GetRequiredFieldMessage(nameof(profileId)));
            return;
        }

        var cancellationToken = Context.ConnectionAborted;

        try
        {
            await ExecuteInScopeAsync(async services =>
            {
                var profileManager = services.GetRequiredService<IAIProfileManager>();
                var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();
                var clientFactory = services.GetRequiredService<IAIClientFactory>();

                var profile = await profileManager.FindByIdAsync(profileId);

                if (profile is null)
                {
                    await Clients.Caller.ReceiveError(GetProfileNotFoundMessage());
                    return;
                }

                if (!await AuthorizeProfileAsync(services, profile))
                {
                    await Clients.Caller.ReceiveError(GetNotAuthorizedMessage());
                    return;
                }

                var deploymentSettings = await GetDeploymentSettingsAsync(services);

                if (string.IsNullOrEmpty(deploymentSettings.DefaultSpeechToTextDeploymentId))
                {
                    await Clients.Caller.ReceiveError(GetNoSttDeploymentMessage());
                    return;
                }

                var deployment = await deploymentManager.FindByIdAsync(deploymentSettings.DefaultSpeechToTextDeploymentId);

                if (deployment is null)
                {
                    await Clients.Caller.ReceiveError(GetSttDeploymentNotFoundMessage());
                    return;
                }

#pragma warning disable MEAI001
                var sttClient = await clientFactory.CreateSpeechToTextClientAsync(deployment);
#pragma warning restore MEAI001

                var speechLanguage = !string.IsNullOrWhiteSpace(language) ? language : "en-US";

                await StreamTranscriptionAsync(sttClient, sessionId ?? string.Empty, audioChunks, audioFormat, speechLanguage, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                Logger.LogDebug("Audio transcription was cancelled.");
                return;
            }

            Logger.LogError(ex, "An error occurred while transcribing audio.");

            try
            {
                await Clients.Caller.ReceiveError(GetTranscriptionErrorMessage());
            }
            catch (Exception writeEx)
            {
                Logger.LogWarning(writeEx, "Failed to write transcription error message.");
            }
        }
    }

    /// <summary>
    /// Synthesizes the given text as speech and streams audio chunks to the caller.
    /// </summary>
    public virtual async Task SynthesizeSpeech(
        string profileId,
        string sessionId,
        string text,
        string voiceName = null)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            await Clients.Caller.ReceiveError(GetRequiredFieldMessage(nameof(profileId)));
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            await Clients.Caller.ReceiveError(GetRequiredFieldMessage(nameof(text)));
            return;
        }

        var cancellationToken = Context.ConnectionAborted;

        try
        {
            await ExecuteInScopeAsync(async services =>
            {
                var profileManager = services.GetRequiredService<IAIProfileManager>();
                var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();
                var clientFactory = services.GetRequiredService<IAIClientFactory>();

                var profile = await profileManager.FindByIdAsync(profileId);

                if (profile is null)
                {
                    await Clients.Caller.ReceiveError(GetProfileNotFoundMessage());
                    return;
                }

                if (!await AuthorizeProfileAsync(services, profile))
                {
                    await Clients.Caller.ReceiveError(GetNotAuthorizedMessage());
                    return;
                }

                if (!profile.TryGetSettings<ChatModeProfileSettings>(out var chatModeSettings)
                    || chatModeSettings.ChatMode != ChatMode.Conversation)
                {
                    await Clients.Caller.ReceiveError(GetTtsNotEnabledMessage());
                    return;
                }

                var deploymentSettings = await GetDeploymentSettingsAsync(services);

                if (string.IsNullOrEmpty(deploymentSettings.DefaultTextToSpeechDeploymentId))
                {
                    await Clients.Caller.ReceiveError(GetNoTtsDeploymentMessage());
                    return;
                }

                var deployment = await deploymentManager.FindByIdAsync(deploymentSettings.DefaultTextToSpeechDeploymentId);

                if (deployment is null)
                {
                    await Clients.Caller.ReceiveError(GetTtsDeploymentNotFoundMessage());
                    return;
                }

                var ttsClient = await clientFactory.CreateTextToSpeechClientAsync(deployment);

                var effectiveVoiceName = !string.IsNullOrWhiteSpace(voiceName)
                    ? voiceName
                    : !string.IsNullOrWhiteSpace(chatModeSettings.VoiceName)
                        ? chatModeSettings.VoiceName
                        : deploymentSettings.DefaultTextToSpeechVoiceId;

                using (ttsClient)
                {
                    await StreamSpeechAsync(ttsClient, sessionId ?? string.Empty, text, effectiveVoiceName, cancellationToken);
                }
            });
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                Logger.LogDebug("Speech synthesis was cancelled.");
                return;
            }

            Logger.LogError(ex, "An error occurred while synthesizing speech.");

            try
            {
                await Clients.Caller.ReceiveError(GetSpeechSynthesisErrorMessage());
            }
            catch (Exception writeEx)
            {
                Logger.LogWarning(writeEx, "Failed to write speech synthesis error message.");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PROTECTED IMPLEMENTATION — chat prompt processing pipeline
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Top-level handler for <see cref="SendMessage"/>. Validates input, resolves
    /// the profile, and dispatches to the appropriate processor.
    /// </summary>
    protected virtual async Task HandleSendMessageAsync(
        ChannelWriter<CompletionPartialMessage> writer,
        IServiceProvider services,
        string profileId,
        string prompt,
        string sessionId,
        string sessionProfileId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var invocationScope = AIInvocationScope.Begin();

            if (string.IsNullOrWhiteSpace(profileId))
            {
                await Clients.Caller.ReceiveError(GetRequiredFieldMessage(nameof(profileId)));
                return;
            }

            var profileManager = services.GetRequiredService<IAIProfileManager>();
            var profile = await profileManager.FindByIdAsync(profileId);

            if (profile is null)
            {
                await Clients.Caller.ReceiveError(GetProfileNotFoundMessage());
                return;
            }

            if (!await AuthorizeProfileAsync(services, profile))
            {
                await Clients.Caller.ReceiveError(GetNotAuthorizedMessage());
                return;
            }

            if (profile.Type == AIProfileType.Utility)
            {
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    await Clients.Caller.ReceiveError(GetRequiredFieldMessage(nameof(prompt)));
                    return;
                }

                await ProcessUtilityAsync(writer, services, profile, prompt.Trim(), cancellationToken);
                return;
            }

            await ProcessChatPromptAsync(writer, services, profile, sessionId, prompt?.Trim(), cancellationToken);
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException || (ex is TaskCanceledException && cancellationToken.IsCancellationRequested))
            {
                Logger.LogDebug("Chat prompt processing was cancelled.");
                return;
            }

            Logger.LogError(ex, "An error occurred while processing the chat prompt.");

            try
            {
                var errorMessage = new CompletionPartialMessage
                {
                    SessionId = sessionId,
                    MessageId = GenerateId(),
                    Content = GetFriendlyErrorMessage(ex),
                };

                await writer.WriteAsync(errorMessage, CancellationToken.None);
            }
            catch (Exception writeEx)
            {
                Logger.LogWarning(writeEx, "Failed to write error message to the channel.");
            }
        }
        finally
        {
            writer.Complete();
        }
    }

    /// <summary>
    /// Processes a chat prompt: resolves or creates a session, dispatches to the
    /// handler, streams the response, and persists results.
    /// </summary>
    protected virtual async Task ProcessChatPromptAsync(
        ChannelWriter<CompletionPartialMessage> writer,
        IServiceProvider services,
        AIProfile profile,
        string sessionId,
        string prompt,
        CancellationToken cancellationToken)
    {
        var sessionManager = services.GetRequiredService<IAIChatSessionManager>();
        var promptStore = services.GetRequiredService<IAIChatSessionPromptStore>();
        var handlerResolver = services.GetRequiredService<IChatResponseHandlerResolver>();
        var sessionHandlers = services.GetRequiredService<IEnumerable<IAIChatSessionHandler>>();

        var (chatSession, isNew) = await GetOrCreateSessionAsync(services, sessionId, profile, prompt);

        await Groups.AddToGroupAsync(Context.ConnectionId, GetSessionGroupName(chatSession.SessionId), cancellationToken);

        var utcNow = GetUtcNow();

        if (chatSession.Status == ChatSessionStatus.Closed)
        {
            chatSession.Status = ChatSessionStatus.Active;
            chatSession.ClosedAtUtc = null;
        }

        chatSession.LastActivityUtc = utcNow;

        // Generate a title when the session was created without one (e.g., via document upload).
        if (!isNew &&
            !string.IsNullOrWhiteSpace(prompt) &&
            (string.IsNullOrWhiteSpace(chatSession.Title) || chatSession.Title == DefaultBlankSessionTitle))
        {
            chatSession.Title = await GenerateSessionTitleAsync(services, profile, prompt);
        }

        var userPromptRecord = new AIChatSessionPrompt
        {
            ItemId = GenerateId(),
            SessionId = chatSession.SessionId,
            Role = ChatRole.User,
            Content = prompt,
        };

        await promptStore.CreateAsync(userPromptRecord);

        var existingPrompts = await promptStore.GetPromptsAsync(chatSession.SessionId);

        var conversationHistory = existingPrompts
            .Where(x => !x.IsGeneratedPrompt)
            .Select(p => new ChatMessage(p.Role, p.Content))
            .ToList();

        // Resolve the chat response handler for this session.
        var chatMode = profile.TryGetSettings<ChatModeProfileSettings>(out var chatModeSettings)
            ? chatModeSettings.ChatMode
            : ChatMode.TextInput;
        var handler = handlerResolver.Resolve(chatSession.ResponseHandlerName, chatMode);

        var handlerContext = new ChatResponseHandlerContext
        {
            Prompt = prompt,
            ConnectionId = Context.ConnectionId,
            SessionId = chatSession.SessionId,
            ChatType = GetChatContextType(),
            ConversationHistory = conversationHistory,
            Services = services,
            Profile = profile,
            ChatSession = chatSession,
        };

        var handlerResult = await handler.HandleAsync(handlerContext, cancellationToken);

        if (handlerResult.IsDeferred)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetSessionGroupName(chatSession.SessionId), cancellationToken);
            await sessionManager.SaveAsync(chatSession);
            return;
        }

        // Streaming response with reference collection.
        var assistantMessage = new AIChatSessionPrompt
        {
            ItemId = GenerateId(),
            SessionId = chatSession.SessionId,
            Role = ChatRole.Assistant,
            Title = profile.PromptSubject,
        };

        if (handlerContext.AssistantAppearance is not null)
        {
            assistantMessage.Put(handlerContext.AssistantAppearance);
        }

        var builder = ZString.CreateStringBuilder();
        var contentItemIds = new HashSet<string>();
        var references = new Dictionary<string, AICompletionReference>();
        var stopwatch = Stopwatch.StartNew();

        // Collect preemptive RAG references if available.
        CollectStreamingReferences(services, handlerContext, references, contentItemIds);

        await foreach (var chunk in handlerResult.ResponseStream.WithCancellation(cancellationToken))
        {
            if (string.IsNullOrEmpty(chunk.Text))
            {
                continue;
            }

            builder.Append(chunk.Text);

            CollectStreamingReferences(services, handlerContext, references, contentItemIds);

            var partialMessage = new CompletionPartialMessage
            {
                SessionId = chatSession.SessionId,
                MessageId = assistantMessage.ItemId,
                ResponseId = chunk.ResponseId,
                Content = chunk.Text,
                References = references,
                Appearance = handlerContext.AssistantAppearance,
            };

            await writer.WriteAsync(partialMessage, cancellationToken);
        }

        // Final pass for any references added by the last tool call.
        CollectStreamingReferences(services, handlerContext, references, contentItemIds);

        stopwatch.Stop();

        if (builder.Length > 0)
        {
            assistantMessage.Content = builder.ToString();
            assistantMessage.ContentItemIds = contentItemIds.ToList();
            assistantMessage.References = references;

            await promptStore.CreateAsync(assistantMessage);
        }

        var prompts = await promptStore.GetPromptsAsync(chatSession.SessionId);

        var context = new ChatMessageCompletedContext
        {
            Profile = profile,
            ChatSession = chatSession,
            Prompts = prompts,
            ResponseLatencyMs = stopwatch.Elapsed.TotalMilliseconds,
        };

        await sessionHandlers.InvokeAsync((h, ctx) => h.MessageCompletedAsync(ctx), context, Logger);

        await OnMessageCompletedAsync(services, context);

        await sessionManager.SaveAsync(chatSession);
    }

    /// <summary>
    /// Processes a generated prompt for a profile that uses a prompt template.
    /// </summary>
    protected virtual async Task ProcessGeneratedPromptAsync(
        ChannelWriter<CompletionPartialMessage> writer,
        IServiceProvider services,
        AIProfile profile,
        string sessionId,
        AIProfile parentProfile,
        CancellationToken cancellationToken)
    {
        var sessionManager = services.GetRequiredService<IAIChatSessionManager>();
        var promptStore = services.GetRequiredService<IAIChatSessionPromptStore>();
        var aiTemplateEngine = services.GetRequiredService<IAITemplateEngine>();
        var completionContextBuilder = services.GetRequiredService<IAICompletionContextBuilder>();
        var completionService = services.GetRequiredService<IAICompletionService>();

        (var chatSession, _) = await GetOrCreateSessionAsync(services, sessionId, parentProfile, userPrompt: profile.Name);

        var generatedPrompt = await aiTemplateEngine.RenderAsync(profile.PromptTemplate,
            new Dictionary<string, object>()
            {
                ["Profile"] = profile,
                ["Session"] = chatSession,
            });

        var assistantMessage = new AIChatSessionPrompt
        {
            ItemId = GenerateId(),
            SessionId = chatSession.SessionId,
            Role = ChatRole.Assistant,
            IsGeneratedPrompt = true,
            Title = profile.PromptSubject,
        };

        var completionContext = await completionContextBuilder.BuildAsync(profile, c =>
        {
        });

        var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();
        var chatDeployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.Chat, deploymentId: completionContext.ChatDeploymentId)
            ?? throw new InvalidOperationException("Unable to resolve a chat deployment for the profile.");

        var builder = ZString.CreateStringBuilder();

        var contentItemIds = new HashSet<string>();
        var references = new Dictionary<string, AICompletionReference>();

        await foreach (var chunk in completionService.CompleteStreamingAsync(chatDeployment, [new ChatMessage(ChatRole.User, generatedPrompt)], completionContext, cancellationToken))
        {
            if (string.IsNullOrEmpty(chunk.Text))
            {
                continue;
            }

            builder.Append(chunk.Text);

            var partialMessage = new CompletionPartialMessage
            {
                SessionId = sessionId,
                MessageId = assistantMessage.ItemId,
                Content = chunk.Text,
                References = references,
            };

            await writer.WriteAsync(partialMessage, cancellationToken);
        }

        assistantMessage.Content = builder.ToString();
        assistantMessage.ContentItemIds = contentItemIds.ToList();
        assistantMessage.References = references;

        await promptStore.CreateAsync(assistantMessage);

        await sessionManager.SaveAsync(chatSession);
    }

    /// <summary>
    /// Processes a utility (one-shot) profile — no session or history needed.
    /// </summary>
    protected virtual async Task ProcessUtilityAsync(
        ChannelWriter<CompletionPartialMessage> writer,
        IServiceProvider services,
        AIProfile profile,
        string prompt,
        CancellationToken cancellationToken)
    {
        var completionContextBuilder = services.GetRequiredService<IAICompletionContextBuilder>();
        var completionService = services.GetRequiredService<IAICompletionService>();
        var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();

        var messageId = GenerateId();

        var completionContext = await completionContextBuilder.BuildAsync(profile, c =>
        {
        });

        var chatDeployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.Chat, deploymentId: completionContext.ChatDeploymentId)
            ?? throw new InvalidOperationException("Unable to resolve a chat deployment for the profile.");

        var references = new Dictionary<string, AICompletionReference>();

        await foreach (var chunk in completionService.CompleteStreamingAsync(chatDeployment, [new ChatMessage(ChatRole.User, prompt)], completionContext, cancellationToken))
        {
            if (string.IsNullOrEmpty(chunk.Text))
            {
                continue;
            }

            var partialMessage = new CompletionPartialMessage
            {
                MessageId = messageId,
                Content = chunk.Text,
                References = references,
            };

            await writer.WriteAsync(partialMessage, cancellationToken);
        }
    }

    // ───────────────── Session resolution ─────────────────

    /// <summary>
    /// Finds an existing session by ID or creates a new one for the given profile.
    /// </summary>
    protected virtual async Task<(AIChatSession ChatSession, bool IsNewSession)> GetOrCreateSessionAsync(
        IServiceProvider services,
        string sessionId,
        AIProfile profile,
        string userPrompt)
    {
        var sessionManager = services.GetRequiredService<IAIChatSessionManager>();

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var existingChatSession = await sessionManager.FindAsync(sessionId);

            if (existingChatSession != null && existingChatSession.ProfileId == profile.ItemId)
            {
                return (existingChatSession, false);
            }
        }

        var chatSession = await sessionManager.NewAsync(profile, new NewAIChatSessionContext());
        chatSession.Title = await GenerateSessionTitleAsync(services, profile, userPrompt);

        if (string.IsNullOrEmpty(chatSession.Title))
        {
            chatSession.Title = Truncate(userPrompt, 255);
        }

        return (chatSession, true);
    }

    // ───────────────── Session payload ─────────────────

    protected virtual object CreateSessionPayload(
        AIChatSession chatSession,
        AIProfile profile,
        IReadOnlyList<AIChatSessionPrompt> prompts)
        => new
        {
            chatSession.SessionId,
            Profile = new
            {
                Id = chatSession.ProfileId,
                Type = profile.Type.ToString(),
            },
            chatSession.Documents,
            Messages = prompts.Select(message => new AIChatResponseMessageDetailed
            {
                Id = message.ItemId,
                Role = message.Role.Value,
                IsGeneratedPrompt = message.IsGeneratedPrompt,
                Title = message.Title,
                Content = message.Content,
                UserRating = message.UserRating,
                References = message.References,
                Appearance = message.As<AssistantMessageAppearance>(),
            })
        };

    // ═══════════════════════════════════════════════════════════════════
    //  PROTECTED TTS / STT HELPERS — shared by conversation methods
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Synthesizes the given text as speech and streams audio chunks to the caller.
    /// </summary>
    protected async Task StreamSpeechAsync(
        ITextToSpeechClient ttsClient,
        string identifier,
        string text,
        string voiceName,
        CancellationToken cancellationToken)
    {
        var options = new TextToSpeechOptions();

        if (!string.IsNullOrWhiteSpace(voiceName))
        {
            options.VoiceId = voiceName;
        }

        var speechText = SpeechTextSanitizer.Sanitize(text);

        if (string.IsNullOrWhiteSpace(speechText))
        {
            await Clients.Caller.ReceiveAudioComplete(identifier);
            return;
        }

        await foreach (var update in ttsClient.GetStreamingAudioAsync(speechText, options, cancellationToken))
        {
            var audioContent = update.Contents.OfType<DataContent>().FirstOrDefault();
            if (audioContent?.Data is not { Length: > 0 } audioData)
            {
                continue;
            }

            var base64Audio = Convert.ToBase64String(audioData.ToArray());
            await Clients.Caller.ReceiveAudioChunk(identifier, base64Audio, audioContent.MediaType ?? "audio/mp3");
        }

        await Clients.Caller.ReceiveAudioComplete(identifier);
    }

    /// <summary>
    /// Reads sentences from a channel and synthesizes each as speech.
    /// </summary>
    protected async Task StreamSentencesAsSpeechAsync(
        ITextToSpeechClient ttsClient,
        Func<string> getIdentifier,
        ChannelReader<string> sentenceReader,
        string voiceName,
        CancellationToken cancellationToken)
    {
        var options = new TextToSpeechOptions();

        if (!string.IsNullOrWhiteSpace(voiceName))
        {
            options.VoiceId = voiceName;
        }

        await foreach (var sentence in sentenceReader.ReadAllAsync(cancellationToken))
        {
            var identifier = getIdentifier();
            var speechText = SpeechTextSanitizer.Sanitize(sentence);

            if (string.IsNullOrWhiteSpace(speechText))
            {
                continue;
            }

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("StreamSentencesAsSpeechAsync: Synthesizing sentence ({Length} chars).", speechText.Length);
            }

            await foreach (var update in ttsClient.GetStreamingAudioAsync(speechText, options, cancellationToken))
            {
                var audioContent = update.Contents.OfType<DataContent>().FirstOrDefault();
                if (audioContent?.Data is not { Length: > 0 } audioData)
                {
                    continue;
                }

                var base64Audio = Convert.ToBase64String(audioData.ToArray());
                await Clients.Caller.ReceiveAudioChunk(identifier, base64Audio, audioContent.MediaType ?? "audio/mp3");
            }

            await Clients.Caller.ReceiveAudioComplete(identifier);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CONVERSATION LOOP — STT transcription + AI response + TTS
    // ═══════════════════════════════════════════════════════════════════

#pragma warning disable MEAI001
    private async Task RunConversationLoopAsync(
        AIProfile profile,
        string sessionId,
        IAsyncEnumerable<string> audioChunks,
        string audioFormat,
        string speechLanguage,
        ISpeechToTextClient sttClient,
        ITextToSpeechClient ttsClient,
        string voiceName,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var pipe = new System.IO.Pipelines.Pipe();

        using var errorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var transcriptionTask = TranscribeConversationAsync(
            pipe.Reader, profile, sessionId, audioFormat, speechLanguage,
            sttClient, ttsClient, voiceName, services, errorCts, cancellationToken);

        try
        {
            await foreach (var base64Chunk in audioChunks.WithCancellation(errorCts.Token))
            {
                try
                {
                    var bytes = Convert.FromBase64String(base64Chunk);
                    await pipe.Writer.WriteAsync(bytes, errorCts.Token);
                }
                catch (FormatException)
                {
                    continue;
                }
            }
        }
        catch (OperationCanceledException) when (errorCts.IsCancellationRequested)
        {
            // Transcription error or connection aborted.
        }

        await pipe.Writer.CompleteAsync();
        await transcriptionTask;
    }

    private async Task TranscribeConversationAsync(
        System.IO.Pipelines.PipeReader pipeReader,
        AIProfile profile,
        string sessionId,
        string audioFormat,
        string speechLanguage,
        ISpeechToTextClient sttClient,
        ITextToSpeechClient ttsClient,
        string voiceName,
        IServiceProvider services,
        CancellationTokenSource errorCts,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource currentResponseCts = null;
        Task<string> currentResponseTask = null;

        try
        {
            await using var readerStream = pipeReader.AsStream();

            using var committedText = ZString.CreateStringBuilder();
            var sttOptions = new SpeechToTextOptions
            {
                SpeechLanguage = speechLanguage,
            };

            if (!string.IsNullOrWhiteSpace(audioFormat))
            {
                sttOptions.AdditionalProperties ??= [];
                sttOptions.AdditionalProperties["audioFormat"] = audioFormat;
            }

            var effectiveSessionId = sessionId ?? string.Empty;

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("TranscribeConversationAsync: Starting STT stream. Language={Language}, Format={Format}.", speechLanguage, audioFormat);
            }

            await foreach (var update in sttClient.GetStreamingTextAsync(readerStream, sttOptions, cancellationToken))
            {
                if (string.IsNullOrEmpty(update.Text))
                {
                    continue;
                }

                var isPartial = update.AdditionalProperties?.TryGetValue("isPartial", out var p) == true && p is true;

                if (isPartial)
                {
                    var display = committedText.Length > 0
                        ? committedText.ToString() + update.Text
                        : update.Text;
                    await Clients.Caller.ReceiveTranscript(effectiveSessionId, display, false);
                }
                else
                {
                    if (currentResponseCts != null)
                    {
                        Logger.LogDebug("TranscribeConversationAsync: New utterance received, cancelling previous AI response.");
                        await currentResponseCts.CancelAsync();

                        if (currentResponseTask != null)
                        {
                            try
                            {
                                effectiveSessionId = await currentResponseTask;
                            }
                            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                            {
                                Logger.LogDebug("AI response was interrupted by new user speech.");
                            }
                        }

                        currentResponseCts.Dispose();
                        currentResponseCts = null;
                        currentResponseTask = null;
                    }

                    if (committedText.Length > 0)
                    {
                        committedText.Append(' ');
                    }

                    committedText.Append(update.Text);
                    var fullText = committedText.ToString().TrimEnd();

                    await Clients.Caller.ReceiveTranscript(effectiveSessionId, fullText, true);
                    await Clients.Caller.ReceiveConversationUserMessage(effectiveSessionId, fullText);

                    committedText.Clear();

                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        Logger.LogDebug("TranscribeConversationAsync: Final utterance received: '{Text}'. Dispatching AI response.", fullText);
                    }

                    currentResponseCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    currentResponseTask = ProcessConversationPromptAsync(
                        profile, effectiveSessionId, fullText,
                        ttsClient, voiceName, services, currentResponseCts.Token);
                }
            }

            Logger.LogDebug("TranscribeConversationAsync: STT stream ended.");

            if (currentResponseTask != null)
            {
                try
                {
                    effectiveSessionId = await currentResponseTask;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Interrupted.
                }

                currentResponseCts?.Dispose();
                currentResponseCts = null;
                currentResponseTask = null;
            }

            var remainingText = committedText.ToString().TrimEnd();

            if (!string.IsNullOrEmpty(remainingText))
            {
                await Clients.Caller.ReceiveConversationUserMessage(effectiveSessionId, remainingText);

                try
                {
                    await ProcessConversationPromptAsync(
                        profile, effectiveSessionId, remainingText,
                        ttsClient, voiceName, services, cancellationToken);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Interrupted.
                }
            }
        }
        catch (Exception)
        {
            await errorCts.CancelAsync();
            throw;
        }
    }

    private async Task<string> ProcessConversationPromptAsync(
        AIProfile profile,
        string sessionId,
        string prompt,
        ITextToSpeechClient ttsClient,
        string voiceName,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("ProcessConversationPromptAsync: Starting for prompt length={PromptLength}.", prompt.Length);
        }

        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();

        var handleTask = HandleSendMessageAsync(channel.Writer, services, profile.ItemId, prompt, sessionId, null, cancellationToken);

        var sentenceChannel = Channel.CreateUnbounded<string>();
        var effectiveSessionId = sessionId;
        string messageId = null;
        string responseId = null;

        var ttsTask = StreamSentencesAsSpeechAsync(ttsClient, () => effectiveSessionId, sentenceChannel.Reader, voiceName, cancellationToken);

        var sentenceBuffer = ZString.CreateStringBuilder();

        try
        {
            await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.SessionId) && string.IsNullOrEmpty(effectiveSessionId))
                {
                    effectiveSessionId = chunk.SessionId;
                }

                messageId ??= chunk.MessageId;
                responseId ??= chunk.ResponseId;

                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    await Clients.Caller.ReceiveConversationAssistantToken(
                        effectiveSessionId, messageId ?? string.Empty, chunk.Content, responseId ?? string.Empty);

                    sentenceBuffer.Append(chunk.Content);

                    if (SentenceBoundaryDetector.EndsWithSentenceBoundary(chunk.Content))
                    {
                        var sentence = sentenceBuffer.ToString().Trim();

                        if (!string.IsNullOrEmpty(sentence))
                        {
                            if (Logger.IsEnabled(LogLevel.Debug))
                            {
                                Logger.LogDebug("ProcessConversationPromptAsync: Queuing sentence for TTS ({Length} chars).", sentence.Length);
                            }

                            await sentenceChannel.Writer.WriteAsync(sentence, cancellationToken);
                            sentenceBuffer.Dispose();
                            sentenceBuffer = ZString.CreateStringBuilder();
                        }
                    }
                }
            }

            await handleTask;

            var remaining = sentenceBuffer.ToString().Trim();
            sentenceBuffer.Dispose();

            if (!string.IsNullOrEmpty(remaining))
            {
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("ProcessConversationPromptAsync: Queuing final partial sentence for TTS ({Length} chars).", remaining.Length);
                }

                await sentenceChannel.Writer.WriteAsync(remaining, cancellationToken);
            }

            sentenceChannel.Writer.Complete();

            await ttsTask;

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("ProcessConversationPromptAsync: Completed. SessionId={SessionId}.", effectiveSessionId);
            }
        }
        finally
        {
            sentenceChannel.Writer.TryComplete();
            sentenceBuffer.Dispose();

            if (!string.IsNullOrEmpty(messageId))
            {
                try
                {
                    await Clients.Caller.ReceiveConversationAssistantComplete(effectiveSessionId, messageId);
                }
                catch
                {
                    // Best-effort — the client may have disconnected.
                }
            }
        }

        return effectiveSessionId;
    }
#pragma warning restore MEAI001

    // ───────────────── STT transcription (input mode) ─────────────────

#pragma warning disable MEAI001
    private async Task StreamTranscriptionAsync(
        ISpeechToTextClient sttClient,
        string sessionId,
        IAsyncEnumerable<string> audioChunks,
        string audioFormat,
        string speechLanguage,
        CancellationToken cancellationToken)
    {
        var pipe = new System.IO.Pipelines.Pipe();

        using var errorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var transcriptionTask = TranscribeAudioInputAsync(sessionId, pipe, audioFormat, speechLanguage, sttClient, errorCts, cancellationToken);

        try
        {
            await foreach (var base64Chunk in audioChunks.WithCancellation(errorCts.Token))
            {
                try
                {
                    var bytes = Convert.FromBase64String(base64Chunk);
                    await pipe.Writer.WriteAsync(bytes, errorCts.Token);
                }
                catch (FormatException)
                {
                    continue;
                }
            }
        }
        catch (OperationCanceledException) when (errorCts.IsCancellationRequested)
        {
            // Transcription failed or connection aborted.
        }

        await pipe.Writer.CompleteAsync();
        await transcriptionTask;
    }

    private async Task TranscribeAudioInputAsync(
        string sessionId,
        System.IO.Pipelines.Pipe pipe,
        string audioFormat,
        string speechLanguage,
        ISpeechToTextClient sttClient,
        CancellationTokenSource errorCts,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var readerStream = pipe.Reader.AsStream();

            using var committedText = ZString.CreateStringBuilder();
            var sttOptions = new SpeechToTextOptions
            {
                SpeechLanguage = speechLanguage,
            };

            if (!string.IsNullOrWhiteSpace(audioFormat))
            {
                sttOptions.AdditionalProperties ??= [];
                sttOptions.AdditionalProperties["audioFormat"] = audioFormat;
            }

            await foreach (var update in sttClient.GetStreamingTextAsync(readerStream, sttOptions, cancellationToken))
            {
                if (string.IsNullOrEmpty(update.Text))
                {
                    continue;
                }

                var isPartial = update.AdditionalProperties?.TryGetValue("isPartial", out var p) == true && p is true;

                if (isPartial)
                {
                    var display = committedText.Length > 0
                        ? committedText.ToString() + update.Text
                        : update.Text;
                    await Clients.Caller.ReceiveTranscript(sessionId, display, false);
                }
                else
                {
                    if (committedText.Length > 0)
                    {
                        committedText.Append(' ');
                    }

                    committedText.Append(update.Text);
                    await Clients.Caller.ReceiveTranscript(sessionId, committedText.ToString(), false);
                }
            }

            var finalText = committedText.ToString().TrimEnd();

            if (!string.IsNullOrEmpty(finalText))
            {
                await Clients.Caller.ReceiveTranscript(sessionId, finalText, true);
            }
        }
        catch (Exception)
        {
            await errorCts.CancelAsync();
            throw;
        }
    }
#pragma warning restore MEAI001
}
