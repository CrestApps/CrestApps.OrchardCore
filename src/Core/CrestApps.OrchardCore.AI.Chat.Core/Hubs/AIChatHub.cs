using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading.Channels;
using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Chat.Core;
using CrestApps.OrchardCore.AI.Chat.Core.Hubs;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.Support;
using Cysharp.Text;
using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Liquid;
using OrchardCore.Modules;
using OrchardCore.Settings;

#pragma warning disable MEAI001 // Text-to-speech APIs from Microsoft.Extensions.AI are preview and require explicit opt-in at each usage site.
namespace CrestApps.OrchardCore.AI.Chat.Hubs;

public class AIChatHub : ChatHubBase<IAIChatHubClient>
{
    private readonly ILogger<AIChatHub> _logger;

    public AIChatHub(
        ILogger<AIChatHub> logger,
        IStringLocalizer<AIChatHub> stringLocalizer)
        : base(logger, stringLocalizer)
    {
        _logger = logger;
    }

    protected override ChatContextType GetChatType()
        => ChatContextType.AIChatSession;

    public ChannelReader<CompletionPartialMessage> SendMessage(string profileId, string prompt, string sessionId, string sessionProfileId, CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();

        // Create a child scope for proper ISession/IDocumentStore lifecycle.
        _ = ShellScope.UsingChildScopeAsync(async scope =>
        {
            await HandlePromptAsync(channel.Writer, scope.ServiceProvider, profileId, prompt, sessionId, sessionProfileId, cancellationToken);
        });

        return channel.Reader;
    }

    public async Task LoadSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(sessionId)].Value);

            return;
        }

        // SignalR connections share a single DI scope for the entire WebSocket lifetime,
        // but OrchardCore scoped services (ISession, IDocumentStore) expect per-request
        // lifetimes. A child scope gives each hub invocation its own services with
        // proper commit/rollback lifecycle on disposal.
        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var services = scope.ServiceProvider;
            var sessionManager = services.GetRequiredService<IAIChatSessionManager>();
            var profileManager = services.GetRequiredService<IAIProfileManager>();
            var authorizationService = services.GetRequiredService<IAuthorizationService>();
            var promptStore = services.GetRequiredService<IAIChatSessionPromptStore>();

            var chatSession = await sessionManager.FindAsync(sessionId);

            if (chatSession == null)
            {
                await Clients.Caller.ReceiveError(S["Session not found."].Value);

                return;
            }

            var profile = await profileManager.FindByIdAsync(chatSession.ProfileId);

            if (profile is null)
            {
                await Clients.Caller.ReceiveError(S["Profile not found."].Value);

                return;
            }

            var httpContext = Context.GetHttpContext();

            if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
            {
                await Clients.Caller.ReceiveError(S["You are not authorized to interact with the given profile."].Value);

                return;
            }

            var prompts = await promptStore.GetPromptsAsync(chatSession.SessionId);

            // Join the SignalR group for this session so deferred responses
            // (e.g., from an external agent via webhook) can reach this client.
            await Groups.AddToGroupAsync(Context.ConnectionId, GetSessionGroupName(chatSession.SessionId));

            await Clients.Caller.LoadSession(CreateSessionPayload(chatSession, profile, prompts));
        });
    }

    public async Task StartSession(string profileId, string initialResponseHandlerName = null)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(profileId)].Value);
            return;
        }

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var services = scope.ServiceProvider;
            var sessionManager = services.GetRequiredService<IAIChatSessionManager>();
            var profileManager = services.GetRequiredService<IAIProfileManager>();
            var authorizationService = services.GetRequiredService<IAuthorizationService>();
            var promptStore = services.GetRequiredService<IAIChatSessionPromptStore>();

            var profile = await profileManager.FindByIdAsync(profileId);
            if (profile is null)
            {
                await Clients.Caller.ReceiveError(S["Profile not found."].Value);
                return;
            }

            var httpContext = Context.GetHttpContext();
            if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
            {
                await Clients.Caller.ReceiveError(S["You are not authorized to interact with the given profile."].Value);
                return;
            }

            if (profile.Type != AIProfileType.Chat)
            {
                await Clients.Caller.ReceiveError(S["Only chat profiles can start chat sessions."].Value);
                return;
            }

            var availability = await GetOrchestratorAvailabilityAsync(services, profile);
            if (!availability.IsAvailable)
            {
                await Clients.Caller.ReceiveError(availability.Message);
                return;
            }

            var chatSession = await sessionManager.NewAsync(profile, new NewAIChatSessionContext());

            // Allow the caller to override the initial response handler set by the profile.
            if (!string.IsNullOrWhiteSpace(initialResponseHandlerName))
            {
                chatSession.ResponseHandlerName = initialResponseHandlerName.Trim();
            }

            await sessionManager.SaveAsync(chatSession);
            var prompts = await promptStore.GetPromptsAsync(chatSession.SessionId);

            // Join the SignalR group for this session so deferred responses can reach this client.
            await Groups.AddToGroupAsync(Context.ConnectionId, GetSessionGroupName(chatSession.SessionId));

            await Clients.Caller.LoadSession(CreateSessionPayload(chatSession, profile, prompts));
        });
    }

    public async Task RateMessage(string sessionId, string messageId, bool isPositive)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(messageId))
        {
            return;
        }

        // Each hub invocation gets its own child scope for proper ISession/IDocumentStore lifecycle.
        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var services = scope.ServiceProvider;
            var sessionManager = services.GetRequiredService<IAIChatSessionManager>();
            var profileManager = services.GetRequiredService<IAIProfileManager>();
            var authorizationService = services.GetRequiredService<IAuthorizationService>();
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

            var httpContext = Context.GetHttpContext();

            if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
            {
                return;
            }

            var prompt = (await promptStore.GetPromptsAsync(chatSession.SessionId))
                .FirstOrDefault(p => p.ItemId == messageId);

            if (prompt is null)
            {
                return;
            }

            // Toggle: if the user clicks the same rating again, clear it.
            prompt.UserRating = prompt.UserRating == isPositive ? null : isPositive;

            await promptStore.UpdateAsync(prompt);

            // Also update session-level rating for analytics.
            var eventService = services.GetService<AIChatSessionEventService>();

            if (eventService is not null)
            {
                // Compute session-level rating from the latest message ratings.
                var allPrompts = await promptStore.GetPromptsAsync(chatSession.SessionId);
                var ratings = allPrompts
                    .Where(p => p.UserRating.HasValue)
                    .Select(p => p.UserRating.Value)
                    .ToList();

                if (ratings.Count > 0)
                {
                    var thumbsUpCount = ratings.Count(r => r);
                    var thumbsDownCount = ratings.Count(r => !r);
                    await eventService.RecordUserRatingAsync(sessionId, thumbsUpCount, thumbsDownCount);
                }
            }

            // Notify the caller of the updated rating.
            await Clients.Caller.MessageRated(messageId, prompt.UserRating);

            // Child scope auto-commits on disposal via IDocumentStore.CommitAsync().
        });
    }

    private async Task HandlePromptAsync(ChannelWriter<CompletionPartialMessage> writer, IServiceProvider services, string profileId, string prompt, string sessionId, string sessionProfileId, CancellationToken cancellationToken)
    {
        try
        {
            using var invocationScope = AIInvocationScope.Begin();

            if (string.IsNullOrWhiteSpace(profileId))
            {
                await Clients.Caller.ReceiveError(S["{0} is required.", nameof(sessionId)].Value);

                return;
            }

            var profileManager = services.GetRequiredService<IAIProfileManager>();
            var profile = await profileManager.FindByIdAsync(profileId);

            if (profile is null)
            {
                await Clients.Caller.ReceiveError(S["Profile not found."].Value);

                return;
            }

            var httpContext = Context.GetHttpContext();
            var authorizationService = services.GetRequiredService<IAuthorizationService>();

            if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
            {
                await Clients.Caller.ReceiveError(S["You are not authorized to interact with the given profile."].Value);

                return;
            }

            var availability = await GetOrchestratorAvailabilityAsync(services, profile);
            if (!availability.IsAvailable)
            {
                await Clients.Caller.ReceiveError(availability.Message);
                return;
            }

            if (profile.Type == AIProfileType.Utility)
            {
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    await Clients.Caller.ReceiveError(S["{0} is required.", nameof(prompt)].Value);
                    return;
                }

                await ProcessUtilityAsync(writer, services, profile, prompt.Trim(), cancellationToken);

                // We don't need to save the session for utility profiles.
                return;
            }

            if (profile.Type == AIProfileType.TemplatePrompt)
            {
                if (string.IsNullOrWhiteSpace(sessionProfileId))
                {
                    await Clients.Caller.ReceiveError(S["{0} is required.", nameof(sessionProfileId)].Value);

                    return;
                }

                var parentProfile = await profileManager.FindByIdAsync(sessionProfileId);

                if (parentProfile is null)
                {
                    await Clients.Caller.ReceiveError(S["Invalid value given to {0}.", nameof(sessionProfileId)].Value);

                    return;
                }

                await ProcessGeneratedPromptAsync(writer, services, profile, sessionId, parentProfile, cancellationToken);
            }
            else
            {
                // At this point, we are dealing with a chat profile.
                await ProcessChatPromptAsync(writer, services, profile, sessionId, prompt?.Trim(), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Don't write error messages if the operation was cancelled (e.g., user navigated away).
            if (ex is OperationCanceledException || (ex is TaskCanceledException && cancellationToken.IsCancellationRequested))
            {
                _logger.LogDebug("Chat prompt processing was cancelled.");
                return;
            }

            _logger.LogError(ex, "An error occurred while processing the chat prompt.");

            try
            {
                var errorMessage = new CompletionPartialMessage
                {
                    SessionId = sessionId,
                    MessageId = IdGenerator.GenerateId(),
                    Content = AIHubErrorMessageHelper.GetFriendlyErrorMessage(ex, S).Value,
                };

                await writer.WriteAsync(errorMessage, CancellationToken.None);
            }
            catch (Exception writeEx)
            {
                _logger.LogWarning(writeEx, "Failed to write error message to the channel.");
            }
        }
        finally
        {
            writer.Complete();
        }
    }

    private static async Task<(AIChatSession ChatSession, bool IsNewSession)> GetSessionAsync(IServiceProvider services, string sessionId, AIProfile profile, string userPrompt)
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

        // At this point, we need to create a new session.
        var chatSession = await sessionManager.NewAsync(profile, new NewAIChatSessionContext());
        var titleUserPrompt = BuildTitleUserPrompt(profile, userPrompt);

        if (profile.TitleType == AISessionTitleType.Generated)
        {
            chatSession.Title = await GetGeneratedTitleAsync(services, profile, titleUserPrompt);
        }

        if (string.IsNullOrEmpty(chatSession.Title))
        {
            chatSession.Title = Str.Truncate(titleUserPrompt, 255);
        }

        return (chatSession, true);
    }

    private static async Task<string> GetGeneratedTitleAsync(IServiceProvider services, AIProfile profile, string userPrompt)
    {
        var aiTemplateService = services.GetRequiredService<IAITemplateService>();
        var completionContextBuilder = services.GetRequiredService<IAICompletionContextBuilder>();
        var completionService = services.GetRequiredService<IAICompletionService>();
        var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();

        var titleSystemMessage = await aiTemplateService.RenderAsync(AITemplateIds.TitleGeneration);

        var context = await completionContextBuilder.BuildAsync(profile, c =>
        {
            c.SystemMessage = titleSystemMessage;
            c.FrequencyPenalty = 0;
            c.PresencePenalty = 0;
            c.TopP = 1;
            c.Temperature = 0;
            c.MaxTokens = 64; // 64 token to generate about 255 characters.

            // Avoid using tools or any data sources when generating title to reduce the used tokens.
            c.DataSourceId = null;
            c.DisableTools = true;
        });

        // Prefer utility deployment for title generation, fall back to chat.
        var deployment = await deploymentManager.ResolveUtilityOrDefaultAsync(
            utilityDeploymentName: context.UtilityDeploymentName,
            chatDeploymentName: context.ChatDeploymentName);

        if (deployment == null)
        {
            return Str.Truncate(userPrompt, 255);
        }

        var titleResponse = await completionService.CompleteAsync(
        deployment,
        [
            new (ChatRole.User, userPrompt),
        ], context);

        // If we fail to set an AI generated title to the session, we'll use the user's prompt at the title.
        return titleResponse.Messages.Count > 0
            ? Str.Truncate(titleResponse.Messages.First().Text, 255)
            : Str.Truncate(userPrompt, 255);
    }

    private async Task ProcessChatPromptAsync(ChannelWriter<CompletionPartialMessage> writer, IServiceProvider services, AIProfile profile, string sessionId, string prompt, CancellationToken cancellationToken)
    {
        var sessionManager = services.GetRequiredService<IAIChatSessionManager>();
        var promptStore = services.GetRequiredService<IAIChatSessionPromptStore>();
        var handlerResolver = services.GetRequiredService<IChatResponseHandlerResolver>();
        var sessionHandlers = services.GetRequiredService<IEnumerable<IAIChatSessionHandler>>();
        var citationCollector = services.GetRequiredService<CitationReferenceCollector>();
        var clock = services.GetRequiredService<IClock>();

        (var chatSession, var isNew) = await GetSessionAsync(services, sessionId, profile, prompt);

        // Ensure the caller joins the session group as soon as the effective session is known,
        // even when the session was created implicitly by SendMessage streaming.
        await Groups.AddToGroupAsync(Context.ConnectionId, GetSessionGroupName(chatSession.SessionId), cancellationToken);

        var utcNow = clock.UtcNow;

        // Handle session reopen if closed.
        if (chatSession.Status == ChatSessionStatus.Closed)
        {
            chatSession.Status = ChatSessionStatus.Active;
            chatSession.ClosedAtUtc = null;
        }

        // Update last activity.
        chatSession.LastActivityUtc = utcNow;

        // Generate a title when the session was created without one (e.g., via document upload).
        if (!isNew &&
            !string.IsNullOrWhiteSpace(prompt) &&
            (string.IsNullOrWhiteSpace(chatSession.Title) || chatSession.Title == AIConstants.DefaultBlankSessionTitle))
        {
            var titleUserPrompt = BuildTitleUserPrompt(profile, prompt);
            if (profile.TitleType == AISessionTitleType.Generated)
            {
                chatSession.Title = await GetGeneratedTitleAsync(services, profile, titleUserPrompt);
            }

            if (string.IsNullOrEmpty(chatSession.Title) || chatSession.Title == AIConstants.DefaultBlankSessionTitle)
            {
                chatSession.Title = Str.Truncate(titleUserPrompt, 255);
            }
        }

        var userPromptRecord = new AIChatSessionPrompt
        {
            ItemId = IdGenerator.GenerateId(),
            SessionId = chatSession.SessionId,
            Role = ChatRole.User,
            Content = prompt,
        };

        await promptStore.CreateAsync(userPromptRecord);

        var existingPrompts = await promptStore.GetPromptsAsync(chatSession.SessionId);

        var conversationHistory = new List<ChatMessage>();

        conversationHistory.AddRange(existingPrompts
            .Where(x => !x.IsGeneratedPrompt)
            .Select(prompt => new ChatMessage(prompt.Role, prompt.Content)));

        // Resolve the chat response handler for this session.
        // In conversation mode, always use the AI handler for TTS/STT integration.
        var chatMode = profile.TryGetSettings<ChatModeProfileSettings>(out var chatModeSettings)
            ? chatModeSettings.ChatMode
            : ChatMode.TextInput;
        var handler = handlerResolver.Resolve(chatSession.ResponseHandlerName, chatMode);

        var handlerContext = new ChatResponseHandlerContext
        {
            Prompt = prompt,
            ConnectionId = Context.ConnectionId,
            SessionId = chatSession.SessionId,
            ChatType = ChatContextType.AIChatSession,
            ConversationHistory = conversationHistory,
            Services = services,
            Profile = profile,
            ChatSession = chatSession,
        };

        var handlerResult = await handler.HandleAsync(handlerContext, cancellationToken);

        if (handlerResult.IsDeferred)
        {
            // Deferred response: save the user prompt (already done above) and session state.
            // The response will arrive later via webhook or external callback.
            // Join the SignalR group so deferred responses can reach the client after reconnection.
            await Groups.AddToGroupAsync(Context.ConnectionId, GetSessionGroupName(chatSession.SessionId), cancellationToken);

            await sessionManager.SaveAsync(chatSession);

            return;
        }

        // Streaming response: enumerate the response stream with citation collection.
        var assistantMessage = new AIChatSessionPrompt
        {
            ItemId = IdGenerator.GenerateId(),
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

        // Collect preemptive RAG references if the handler produced an OrchestrationContext.
        if (handlerContext.Properties.TryGetValue("OrchestrationContext", out var ctxObj) && ctxObj is OrchestrationContext orchestratorContext)
        {
            citationCollector.CollectPreemptiveReferences(orchestratorContext, references, contentItemIds);
        }

        await foreach (var chunk in handlerResult.ResponseStream.WithCancellation(cancellationToken))
        {
            if (string.IsNullOrEmpty(chunk.Text))
            {
                continue;
            }

            builder.Append(chunk.Text);

            // Incrementally collect any new tool references that appeared during streaming
            // (e.g., from DataSourceSearchTool or SearchDocumentsTool invoked by the AI model).
            citationCollector.CollectToolReferences(references, contentItemIds);

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

        // Final pass to collect any tool references added by the last tool call.
        citationCollector.CollectToolReferences(references, contentItemIds);

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

        await sessionHandlers.InvokeAsync((handler, ctx) => handler.MessageCompletedAsync(ctx), context, _logger);

        await sessionManager.SaveAsync(chatSession);
    }

    private static async Task ProcessGeneratedPromptAsync(ChannelWriter<CompletionPartialMessage> writer, IServiceProvider services, AIProfile profile, string sessionId, AIProfile parentProfile, CancellationToken cancellationToken)
    {
        var sessionManager = services.GetRequiredService<IAIChatSessionManager>();
        var promptStore = services.GetRequiredService<IAIChatSessionPromptStore>();
        var liquidTemplateManager = services.GetRequiredService<ILiquidTemplateManager>();
        var completionContextBuilder = services.GetRequiredService<IAICompletionContextBuilder>();
        var completionService = services.GetRequiredService<IAICompletionService>();

        (var chatSession, _) = await GetSessionAsync(services, sessionId, parentProfile, userPrompt: profile.Name);

        var generatedPrompt = await liquidTemplateManager.RenderStringAsync(profile.PromptTemplate, NullEncoder.Default,
            new Dictionary<string, FluidValue>()
            {
                ["Profile"] = new ObjectValue(profile),
                ["Session"] = new ObjectValue(chatSession),
            });

        var assistantMessage = new AIChatSessionPrompt
        {
            ItemId = IdGenerator.GenerateId(),
            SessionId = chatSession.SessionId,
            Role = ChatRole.Assistant,
            IsGeneratedPrompt = true,
            Title = profile.PromptSubject,
        };

        var completionContext = await completionContextBuilder.BuildAsync(profile, c =>
        {
        });

        var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();
        var chatDeployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.Chat, deploymentName: completionContext.ChatDeploymentName)
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

    private static async Task ProcessUtilityAsync(ChannelWriter<CompletionPartialMessage> writer, IServiceProvider services, AIProfile profile, string prompt, CancellationToken cancellationToken)
    {
        var completionContextBuilder = services.GetRequiredService<IAICompletionContextBuilder>();
        var completionService = services.GetRequiredService<IAICompletionService>();
        var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();

        var messageId = IdGenerator.GenerateId();

        var completionContext = await completionContextBuilder.BuildAsync(profile, c =>
        {
        });

        var chatDeployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.Chat, deploymentName: completionContext.ChatDeploymentName)
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

    private static object CreateSessionPayload(AIChatSession chatSession, AIProfile profile, IReadOnlyList<AIChatSessionPrompt> prompts)
        => new
        {
            chatSession.SessionId,
            Profile = new
            {
                Id = chatSession.ProfileId,
                Type = profile.Type.ToString()
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

    private static string BuildTitleUserPrompt(AIProfile profile, string userPrompt)
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

    private static async Task<OrchestratorAvailability> GetOrchestratorAvailabilityAsync(IServiceProvider services, AIProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.OrchestratorName))
        {
            return new OrchestratorAvailability();
        }

        var availabilityProvider = services.GetServices<IOrchestratorAvailabilityProvider>()
            .FirstOrDefault(provider => string.Equals(provider.OrchestratorName, profile.OrchestratorName, StringComparison.OrdinalIgnoreCase));

        return availabilityProvider is null
            ? new OrchestratorAvailability()
            : await availabilityProvider.GetAvailabilityAsync();
    }

    /// <summary>
    /// Gets the SignalR group name for a chat session. Clients in this group
    /// receive deferred responses delivered via webhook or external callback.
    /// </summary>
    public static string GetSessionGroupName(string sessionId)
        => $"aichat-session-{sessionId}";

    public async Task StartConversation(string profileId, string sessionId, IAsyncEnumerable<string> audioChunks, string audioFormat = null, string language = null)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(profileId)].Value);
            return;
        }

        var cancellationToken = Context.ConnectionAborted;

        try
        {
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                var services = scope.ServiceProvider;
                var profileManager = services.GetRequiredService<IAIProfileManager>();
                var authorizationService = services.GetRequiredService<IAuthorizationService>();
                var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();
                var clientFactory = services.GetRequiredService<IAIClientFactory>();
                var siteService = services.GetRequiredService<ISiteService>();

                var profile = await profileManager.FindByIdAsync(profileId);

                if (profile is null)
                {
                    await Clients.Caller.ReceiveError(S["Profile not found."].Value);
                    return;
                }

                var httpContext = Context.GetHttpContext();

                if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
                {
                    await Clients.Caller.ReceiveError(S["You are not authorized to interact with the given profile."].Value);
                    return;
                }

                if (!profile.TryGetSettings<ChatModeProfileSettings>(out var chatModeSettings)
                    || chatModeSettings.ChatMode != ChatMode.Conversation)
                {
                    await Clients.Caller.ReceiveError(S["Conversation mode is not enabled for this profile."].Value);
                    return;
                }

                var speechToTextDeployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.SpeechToText);

                if (speechToTextDeployment is null)
                {
                    await Clients.Caller.ReceiveError(S["No speech-to-text deployment is configured or available."].Value);
                    return;
                }

                var textToSpeechDeployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.TextToSpeech);

                if (textToSpeechDeployment is null)
                {
                    await Clients.Caller.ReceiveError(S["No text-to-speech deployment is configured or available."].Value);
                    return;
                }

                using var speechToTextClient = await clientFactory.CreateSpeechToTextClientAsync(speechToTextDeployment);
                using var textToSpeechClient = await clientFactory.CreateTextToSpeechClientAsync(textToSpeechDeployment);

                var effectiveVoiceName = chatModeSettings.VoiceName;

                if (string.IsNullOrWhiteSpace(effectiveVoiceName))
                {
                    var site = await siteService.GetSiteSettingsAsync();

                    if (site.TryGet<DefaultAIDeploymentSettings>(out var deploymentSettings))
                    {
                        effectiveVoiceName = deploymentSettings.DefaultTextToSpeechVoiceId;
                    }
                }

                var speechLanguage = !string.IsNullOrWhiteSpace(language) ? language : "en-US";

                using var conversationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Context.Items[ConversationCtsKey] = conversationCts;

                try
                {

                    await RunConversationLoopAsync(
                        profile, sessionId, audioChunks, audioFormat, speechLanguage,
                        speechToTextClient, textToSpeechClient, effectiveVoiceName, services, conversationCts.Token);

                }
                finally
                {
                    Context.Items.Remove(ConversationCtsKey);
                }
            });
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                _logger.LogDebug("Conversation was cancelled.");
                return;
            }

            _logger.LogError(ex, "An error occurred during conversation mode.");

            try
            {
                await Clients.Caller.ReceiveError(S["An error occurred during the conversation. Please try again."].Value);
            }
            catch (Exception writeEx)
            {
                _logger.LogWarning(writeEx, "Failed to write conversation error message.");
            }
        }
    }

#pragma warning disable MEAI001
    private async Task RunConversationLoopAsync(
        AIProfile profile,
        string sessionId,
        IAsyncEnumerable<string> audioChunks,
        string audioFormat,
        string speechLanguage,
        ISpeechToTextClient speechToTextClient,
        ITextToSpeechClient textToSpeechClient,
        string voiceName,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var pipe = new Pipe();

        // CTS to break the audio chunk loop on transcription failure.
        using var errorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start the transcription pipeline. No Task.Run needed because TranscribeConversationAsync
        // is async and returns at its first await, allowing the caller to proceed to the audio loop.
        var transcriptionTask = TranscribeConversationAsync(
            pipe.Reader, profile, sessionId, audioFormat, speechLanguage,
            speechToTextClient, textToSpeechClient, voiceName, services, errorCts, cancellationToken);

        // Write audio chunks to the pipe as they arrive.
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
        PipeReader pipeReader,
        AIProfile profile,
        string sessionId,
        string audioFormat,
        string speechLanguage,
        ISpeechToTextClient speechToTextClient,
        ITextToSpeechClient textToSpeechClient,
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

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("TranscribeConversationAsync: Starting STT stream. Language={Language}, Format={Format}.", speechLanguage, audioFormat);
            }

            await foreach (var update in speechToTextClient.GetStreamingTextAsync(readerStream, sttOptions, cancellationToken))
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
                    // User produced a complete utterance. Cancel any in-progress AI response
                    // so we can process the new prompt.
                    if (currentResponseCts != null)
                    {
                        _logger.LogDebug("TranscribeConversationAsync: New utterance received, cancelling previous AI response.");
                        await currentResponseCts.CancelAsync();

                        if (currentResponseTask != null)
                        {
                            try
                            {
                                effectiveSessionId = await currentResponseTask;
                            }
                            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                            {
                                _logger.LogDebug("AI response was interrupted by new user speech.");
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

                    // Send final transcript to indicate the utterance is done.
                    await Clients.Caller.ReceiveTranscript(effectiveSessionId, fullText, true);

                    // Display the user's spoken text as a user message.
                    await Clients.Caller.ReceiveConversationUserMessage(effectiveSessionId, fullText);

                    // Reset committed text for the next utterance.
                    committedText.Clear();

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("TranscribeConversationAsync: Final utterance received: '{Text}'. Dispatching AI response.", fullText);
                    }

                    // Start the AI response as a non-blocking task so the STT loop continues
                    // reading and the user can interrupt the AI by speaking again.
                    currentResponseCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    currentResponseTask = ProcessConversationPromptAsync(
                        profile, effectiveSessionId, fullText,
                        textToSpeechClient, voiceName, services, currentResponseCts.Token);
                }
            }

            _logger.LogDebug("TranscribeConversationAsync: STT stream ended.");

            // Wait for any pending AI response after the audio stream ends.
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

            // Handle any remaining committed text after the audio stream ends.
            var remainingText = committedText.ToString().TrimEnd();

            if (!string.IsNullOrEmpty(remainingText))
            {
                await Clients.Caller.ReceiveConversationUserMessage(effectiveSessionId, remainingText);

                try
                {
                    await ProcessConversationPromptAsync(
                        profile, effectiveSessionId, remainingText,
                        textToSpeechClient, voiceName, services, cancellationToken);
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
        ITextToSpeechClient textToSpeechClient,
        string voiceName,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("ProcessConversationPromptAsync: Starting for prompt length={PromptLength}.", prompt.Length);
        }

        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();

        var handleTask = HandlePromptAsync(channel.Writer, services, profile.ItemId, prompt, sessionId, null, cancellationToken);

        var sentenceChannel = Channel.CreateUnbounded<string>();
        var effectiveSessionId = sessionId;
        string messageId = null;
        string responseId = null;

        // Start TTS consumer that sends audio per sentence (text is sent immediately below).
        var ttsTask = StreamSentencesAsSpeechAsync(textToSpeechClient, () => effectiveSessionId, sentenceChannel.Reader, voiceName, cancellationToken);

        var sentenceBuffer = ZString.CreateStringBuilder();

        try
        {
            // Stream text tokens to the client IMMEDIATELY as they arrive from the AI model,
            // and also accumulate into sentences for parallel TTS synthesis.
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
                    // Send text token to the client immediately so the user sees it right away.
                    await Clients.Caller.ReceiveConversationAssistantToken(
                        effectiveSessionId, messageId ?? string.Empty, chunk.Content, responseId ?? string.Empty, chunk.Appearance);

                    sentenceBuffer.Append(chunk.Content);

                    // Queue completed sentences for TTS synthesis.
                    if (SentenceBoundaryDetector.EndsWithSentenceBoundary(chunk.Content))
                    {
                        var sentence = sentenceBuffer.ToString().Trim();

                        if (!string.IsNullOrEmpty(sentence))
                        {
                            if (_logger.IsEnabled(LogLevel.Debug))
                            {
                                _logger.LogDebug("ProcessConversationPromptAsync: Queuing sentence for TTS ({Length} chars).", sentence.Length);
                            }

                            await sentenceChannel.Writer.WriteAsync(sentence, cancellationToken);
                            sentenceBuffer.Dispose();
                            sentenceBuffer = ZString.CreateStringBuilder();
                        }
                    }
                }
            }

            await handleTask;

            // Flush any remaining text as the final sentence.
            var remaining = sentenceBuffer.ToString().Trim();
            sentenceBuffer.Dispose();

            if (!string.IsNullOrEmpty(remaining))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("ProcessConversationPromptAsync: Queuing final partial sentence for TTS ({Length} chars).", remaining.Length);
                }

                await sentenceChannel.Writer.WriteAsync(remaining, cancellationToken);
            }

            sentenceChannel.Writer.Complete();

            // Wait for all TTS sentences to finish streaming audio.
            await ttsTask;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("ProcessConversationPromptAsync: Completed. SessionId={SessionId}.", effectiveSessionId);
            }
        }
        finally
        {
            sentenceChannel.Writer.TryComplete();
            sentenceBuffer.Dispose();

            // Always notify the client that the assistant response finished (or was
            // interrupted/cancelled) so the spinner stops even on error or cancellation.
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

    public async Task SendAudioStream(string profileId, string sessionId, IAsyncEnumerable<string> audioChunks, string audioFormat = null, string language = null)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(profileId)].Value);
            return;
        }

        var traceId = Guid.NewGuid().ToString("N")[..8];
        var sw = Stopwatch.StartNew();

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms SendAudioStream START. ProfileId={ProfileId}, Format={Format}",
                traceId, sw.ElapsedMilliseconds, profileId, audioFormat);
        }

        var cancellationToken = Context.ConnectionAborted;

        try
        {
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                var services = scope.ServiceProvider;
                var profileManager = services.GetRequiredService<IAIProfileManager>();
                var authorizationService = services.GetRequiredService<IAuthorizationService>();
                var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();
                var clientFactory = services.GetRequiredService<IAIClientFactory>();
                var siteService = services.GetRequiredService<ISiteService>();

                var profile = await profileManager.FindByIdAsync(profileId);

                if (profile is null)
                {
                    await Clients.Caller.ReceiveError(S["Profile not found."].Value);
                    return;
                }

                var httpContext = Context.GetHttpContext();

                if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
                {
                    await Clients.Caller.ReceiveError(S["You are not authorized to interact with the given profile."].Value);
                    return;
                }

                var speechToTextDeployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.SpeechToText);

                if (speechToTextDeployment is null)
                {
                    await Clients.Caller.ReceiveError(S["No speech-to-text deployment is configured or available."].Value);
                    return;
                }

#pragma warning disable MEAI001
                using var speechToTextClient = await clientFactory.CreateSpeechToTextClientAsync(speechToTextDeployment);
#pragma warning restore MEAI001

                var speechLanguage = !string.IsNullOrWhiteSpace(language) ? language : "en-US";

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms Scope resolved, STT client created. Starting StreamTranscriptionAsync...",
                        traceId, sw.ElapsedMilliseconds);
                }

                await StreamTranscriptionAsync(traceId, sw, speechToTextClient, sessionId ?? string.Empty, audioChunks, audioFormat, speechLanguage, cancellationToken);

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms SendAudioStream COMPLETE.", traceId, sw.ElapsedMilliseconds);
                }
            });
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                _logger.LogDebug("Audio transcription was cancelled.");
                return;
            }

            _logger.LogError(ex, "An error occurred while transcribing audio.");

            try
            {
                await Clients.Caller.ReceiveError(S["An error occurred while transcribing the audio. Please try again."].Value);
            }
            catch (Exception writeEx)
            {
                _logger.LogWarning(writeEx, "Failed to write transcription error message.");
            }
        }
    }

#pragma warning disable MEAI001
    private async Task StreamTranscriptionAsync(
        string traceId,
        Stopwatch sw,
        ISpeechToTextClient speechToTextClient,
        string sessionId,
        IAsyncEnumerable<string> audioChunks,
        string audioFormat,
        string speechLanguage,
        CancellationToken cancellationToken)
    {
        var pipe = new Pipe();
        var chunkCount = 0;
        var totalBytes = 0L;

        // CTS to break the audio chunk loop when transcription fails.
        using var errorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start streaming transcription in the background.
        var transcriptionTask = TranscribeAudioInputAsync(traceId, sw, sessionId, pipe, audioFormat, speechLanguage, speechToTextClient, errorCts, cancellationToken);

        // Write audio chunks to the pipe as they arrive from SignalR.
        try
        {
            await foreach (var base64Chunk in audioChunks.WithCancellation(errorCts.Token))
            {
                try
                {
                    var bytes = Convert.FromBase64String(base64Chunk);
                    await pipe.Writer.WriteAsync(bytes, errorCts.Token);
                    chunkCount++;
                    totalBytes += bytes.Length;

                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms Pipe.Write chunk #{ChunkCount}: {Bytes} bytes (total={TotalBytes})",
                            traceId, sw.ElapsedMilliseconds, chunkCount, bytes.Length, totalBytes);
                    }
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

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms All audio chunks received. Chunks={ChunkCount}, TotalBytes={TotalBytes}. Completing pipe...",
                traceId, sw.ElapsedMilliseconds, chunkCount, totalBytes);
        }

        // Signal that all audio has been sent.
        await pipe.Writer.CompleteAsync();

        // Wait for the transcription to finish processing all audio.
        await transcriptionTask;

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms StreamTranscriptionAsync DONE.", traceId, sw.ElapsedMilliseconds);
        }
    }

    private async Task TranscribeAudioInputAsync(
        string traceId,
        Stopwatch sw,
        string sessionId,
        Pipe pipe,
        string audioFormat,
        string speechLanguage,
        ISpeechToTextClient speechToTextClient,
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

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms TranscribeAudioInputAsync: calling GetStreamingTextAsync...",
                    traceId, sw.ElapsedMilliseconds);
            }

            var updateCount = 0;

            await foreach (var update in speechToTextClient.GetStreamingTextAsync(readerStream, sttOptions, cancellationToken))
            {
                if (string.IsNullOrEmpty(update.Text))
                {
                    continue;
                }

                updateCount++;
                var isPartial = update.AdditionalProperties?.TryGetValue("isPartial", out var p) == true && p is true;

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms Received update #{UpdateCount}: isPartial={IsPartial}, text='{Text}'",
                        traceId, sw.ElapsedMilliseconds, updateCount, isPartial, update.Text);
                }

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

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms STT stream ended. Updates={UpdateCount}, FinalText='{FinalText}'",
                    traceId, sw.ElapsedMilliseconds, updateCount, finalText);
            }

            if (!string.IsNullOrEmpty(finalText))
            {
                await Clients.Caller.ReceiveTranscript(sessionId, finalText, true);
            }
        }
        catch (Exception)
        {
            // Cancel the audio chunk loop so the error surfaces immediately.
            await errorCts.CancelAsync();
            throw;
        }
    }
#pragma warning restore MEAI001

    public async Task SynthesizeSpeech(string profileId, string sessionId, string text, string voiceName = null)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(profileId)].Value);
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(text)].Value);
            return;
        }

        var cancellationToken = Context.ConnectionAborted;

        try
        {
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                var services = scope.ServiceProvider;
                var profileManager = services.GetRequiredService<IAIProfileManager>();
                var authorizationService = services.GetRequiredService<IAuthorizationService>();
                var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();
                var clientFactory = services.GetRequiredService<IAIClientFactory>();
                var siteService = services.GetRequiredService<ISiteService>();

                var profile = await profileManager.FindByIdAsync(profileId);

                if (profile is null)
                {
                    await Clients.Caller.ReceiveError(S["Profile not found."].Value);
                    return;
                }

                var httpContext = Context.GetHttpContext();

                if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
                {
                    await Clients.Caller.ReceiveError(S["You are not authorized to interact with the given profile."].Value);
                    return;
                }

                if (!profile.TryGetSettings<ChatModeProfileSettings>(out var chatModeSettings)
                    || chatModeSettings.ChatMode != ChatMode.Conversation)
                {
                    await Clients.Caller.ReceiveError(S["Text-to-speech is not enabled for this profile."].Value);
                    return;
                }

                var site = await siteService.GetSiteSettingsAsync();
                var deploymentSettings = site.As<DefaultAIDeploymentSettings>();
                var textToSpeechDeployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.TextToSpeech);

                if (textToSpeechDeployment is null)
                {
                    await Clients.Caller.ReceiveError(S["No text-to-speech deployment is configured or available."].Value);
                    return;
                }

                using var textToSpeechClient = await clientFactory.CreateTextToSpeechClientAsync(textToSpeechDeployment);

                var effectiveVoiceName = !string.IsNullOrWhiteSpace(voiceName)
                    ? voiceName
                    : !string.IsNullOrWhiteSpace(chatModeSettings.VoiceName)
                        ? chatModeSettings.VoiceName
                        : deploymentSettings.DefaultTextToSpeechVoiceId;

                await StreamSpeechAsync(textToSpeechClient, sessionId ?? string.Empty, text, effectiveVoiceName, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                _logger.LogDebug("Speech synthesis was cancelled.");
                return;
            }

            _logger.LogError(ex, "An error occurred while synthesizing speech.");

            try
            {
                await Clients.Caller.ReceiveError(S["An error occurred while synthesizing speech. Please try again."].Value);
            }
            catch (Exception writeEx)
            {
                _logger.LogWarning(writeEx, "Failed to write speech synthesis error message.");
            }
        }
    }
}
