using System.Diagnostics;
using System.Threading.Channels;
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
using OrchardCore.Data.Documents;
using OrchardCore.Liquid;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Hubs;

public class AIChatHub : Hub<IAIChatHubClient>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIProfileManager _profileManager;
    private readonly IAIChatSessionManager _sessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IDocumentStore _documentStore;
    private readonly IAICompletionService _completionService;
    private readonly IAICompletionContextBuilder _completionContextBuilder;
    private readonly IOrchestrationContextBuilder _orchestrationContextBuilder;
    private readonly IOrchestratorResolver _orchestratorResolver;
    private readonly IEnumerable<IAIChatSessionHandler> _sessionHandlers;
    private readonly CitationReferenceCollector _citationCollector;
    private readonly IClock _clock;
    private readonly ILogger<AIChatHub> _logger;

    protected readonly IStringLocalizer S;

    public AIChatHub(
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager,
        IAIChatSessionManager sessionManager,
        IAIChatSessionPromptStore promptStore,
        ILiquidTemplateManager liquidTemplateManager,
        IDocumentStore documentStore,
        IAICompletionService completionService,
        IAICompletionContextBuilder completionContextBuilder,
        IOrchestrationContextBuilder orchestrationContextBuilder,
        IOrchestratorResolver orchestratorResolver,
        IEnumerable<IAIChatSessionHandler> sessionHandlers,
        CitationReferenceCollector citationCollector,
        IClock clock,
        ILogger<AIChatHub> logger,
        IStringLocalizer<AIChatHub> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _profileManager = profileManager;
        _sessionManager = sessionManager;
        _promptStore = promptStore;
        _liquidTemplateManager = liquidTemplateManager;
        _documentStore = documentStore;
        _completionService = completionService;
        _completionContextBuilder = completionContextBuilder;
        _orchestrationContextBuilder = orchestrationContextBuilder;
        _orchestratorResolver = orchestratorResolver;
        _sessionHandlers = sessionHandlers;
        _citationCollector = citationCollector;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    public ChannelReader<CompletionPartialMessage> SendMessage(string profileId, string prompt, string sessionId, string sessionProfileId, CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();

        // Avoid awaiting HandlePromptAsync to prevent blocking until all items are written,  
        // ensuring the channel is returned to the client immediately.
        _ = HandlePromptAsync(channel.Writer, profileId, prompt, sessionId, sessionProfileId, cancellationToken);

        return channel.Reader;
    }

    public async Task LoadSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(sessionId)].Value);

            return;
        }

        var chatSession = await _sessionManager.FindAsync(sessionId);

        if (chatSession == null)
        {
            await Clients.Caller.ReceiveError(S["Session not found."].Value);

            return;
        }

        var profile = await _profileManager.FindByIdAsync(chatSession.ProfileId);

        if (profile is null)
        {
            await Clients.Caller.ReceiveError(S["Profile not found."].Value);

            return;
        }

        var httpContext = Context.GetHttpContext();

        if (!await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
        {
            await Clients.Caller.ReceiveError(S["You are not authorized to interact with the given profile."].Value);

            return;
        }

        var prompts = await _promptStore.GetPromptsAsync(chatSession.SessionId);

        await Clients.Caller.LoadSession(new
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
            })
        });
    }

    public async Task RateMessage(string sessionId, string messageId, bool isPositive)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(messageId))
        {
            return;
        }

        var chatSession = await _sessionManager.FindAsync(sessionId);

        if (chatSession is null)
        {
            return;
        }

        var profile = await _profileManager.FindByIdAsync(chatSession.ProfileId);

        if (profile is null)
        {
            return;
        }

        var httpContext = Context.GetHttpContext();

        if (!await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
        {
            return;
        }

        var prompt = (await _promptStore.GetPromptsAsync(chatSession.SessionId))
            .FirstOrDefault(p => p.ItemId == messageId);

        if (prompt is null)
        {
            return;
        }

        // Toggle: if the user clicks the same rating again, clear it.
        prompt.UserRating = prompt.UserRating == isPositive ? null : isPositive;

        await _promptStore.UpdateAsync(prompt);

        // Also update session-level rating for analytics.
        var eventService = httpContext.RequestServices.GetService<AIChatSessionEventService>();

        if (eventService is not null)
        {
            // Compute session-level rating from the latest message ratings.
            var allPrompts = await _promptStore.GetPromptsAsync(chatSession.SessionId);
            var ratings = allPrompts
                .Where(p => p.UserRating.HasValue)
                .Select(p => p.UserRating.Value)
                .ToList();

            if (ratings.Count > 0)
            {
                var positiveCount = ratings.Count(r => r);
                await eventService.RecordUserRatingAsync(sessionId, positiveCount >= ratings.Count - positiveCount);
            }
        }

        // Notify the caller of the updated rating.
        await Clients.Caller.MessageRated(messageId, prompt.UserRating);

        await _documentStore.CommitAsync();
    }

    private async Task HandlePromptAsync(ChannelWriter<CompletionPartialMessage> writer, string profileId, string prompt, string sessionId, string sessionProfileId, CancellationToken cancellationToken)
    {
        using var invocationScope = AIInvocationScope.Begin();

        try
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                await Clients.Caller.ReceiveError(S["{0} is required.", nameof(sessionId)].Value);

                return;
            }

            var profile = await _profileManager.FindByIdAsync(profileId);

            if (profile is null)
            {
                await Clients.Caller.ReceiveError(S["Profile not found."].Value);

                return;
            }

            var httpContext = Context.GetHttpContext();

            if (!await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
            {
                await Clients.Caller.ReceiveError(S["You are not authorized to interact with the given profile."].Value);

                return;
            }

            if (profile.Type == AIProfileType.Utility)
            {
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    await Clients.Caller.ReceiveError(S["{0} is required.", nameof(prompt)].Value);
                    return;
                }

                await ProcessUtilityAsync(writer, profile, prompt.Trim(), cancellationToken);

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

                var parentProfile = await _profileManager.FindByIdAsync(sessionProfileId);

                if (parentProfile is null)
                {
                    await Clients.Caller.ReceiveError(S["Invalid value given to {0}.", nameof(sessionProfileId)].Value);

                    return;
                }

                await ProcessGeneratedPromptAsync(writer, profile, sessionId, parentProfile, cancellationToken);
            }
            else
            {
                // At this point, we are dealing with a chat profile.
                await ProcessChatPromptAsync(writer, profile, sessionId, prompt?.Trim(), cancellationToken);
            }

            await _documentStore.CommitAsync();
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


    private async Task<(AIChatSession ChatSession, bool IsNewSession)> GetSessionsAsync(IAIChatSessionManager sessionManager, string sessionId, AIProfile profile, string userPrompt)
    {
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

        if (profile.TitleType == AISessionTitleType.Generated)
        {
            chatSession.Title = await GetGeneratedTitleAsync(profile, userPrompt);
        }

        if (string.IsNullOrEmpty(chatSession.Title))
        {
            chatSession.Title = Str.Truncate(userPrompt, 255);
        }

        return (chatSession, true);
    }

    private async Task<string> GetGeneratedTitleAsync(AIProfile profile, string userPrompt)
    {
        var context = await _completionContextBuilder.BuildAsync(profile, c =>
        {
            c.SystemMessage = AIConstants.TitleGeneratorSystemMessage;
            c.FrequencyPenalty = 0;
            c.PresencePenalty = 0;
            c.TopP = 1;
            c.Temperature = 0;
            c.MaxTokens = 64; // 64 token to generate about 255 characters.
            c.UserMarkdownInResponse = false;

            // Avoid using tools or any data sources when generating title to reduce the used tokens.
            c.DataSourceId = null;
            c.DisableTools = true;
        });

        var titleResponse = await _completionService.CompleteAsync(profile.Source,
        [
            new (ChatRole.User, userPrompt),
        ], context);

        // If we fail to set an AI generated title to the session, we'll use the user's prompt at the title.
        return titleResponse.Messages.Count > 0
            ? Str.Truncate(titleResponse.Messages.First().Text, 255)
            : Str.Truncate(userPrompt, 255);
    }

    private async Task ProcessChatPromptAsync(ChannelWriter<CompletionPartialMessage> writer, AIProfile profile, string sessionId, string prompt, CancellationToken cancellationToken)
    {
        (var chatSession, var isNew) = await GetSessionsAsync(_sessionManager, sessionId, profile, prompt);

        var utcNow = _clock.UtcNow;

        // Handle session reopen if closed.
        if (chatSession.Status == ChatSessionStatus.Closed)
        {
            chatSession.Status = ChatSessionStatus.Active;
            chatSession.ClosedAtUtc = null;
        }

        // Update last activity.
        chatSession.LastActivityUtc = utcNow;

        // Generate a title when the session was created without one (e.g., via document upload).
        if (!isNew && chatSession.Title == AIConstants.DefaultBlankSessionTitle && !string.IsNullOrWhiteSpace(prompt))
        {
            if (profile.TitleType == AISessionTitleType.Generated)
            {
                chatSession.Title = await GetGeneratedTitleAsync(profile, prompt);
            }

            if (string.IsNullOrEmpty(chatSession.Title) || chatSession.Title == AIConstants.DefaultBlankSessionTitle)
            {
                chatSession.Title = Str.Truncate(prompt, 255);
            }
        }

        var userPromptRecord = new AIChatSessionPrompt
        {
            ItemId = IdGenerator.GenerateId(),
            SessionId = chatSession.SessionId,
            Role = ChatRole.User,
            Content = prompt,
        };

        await _promptStore.CreateAsync(userPromptRecord);

        var existingPrompts = await _promptStore.GetPromptsAsync(chatSession.SessionId);

        var transcript = existingPrompts
            .Where(x => !x.IsGeneratedPrompt)
            .Select(prompt => new ChatMessage(prompt.Role, prompt.Content));

        var assistantMessage = new AIChatSessionPrompt
        {
            ItemId = IdGenerator.GenerateId(),
            SessionId = chatSession.SessionId,
            Role = ChatRole.Assistant,
            Title = profile.PromptSubject,
        };

        var builder = ZString.CreateStringBuilder();

        // Build the orchestration context using the handler pipeline.
        var orchestratorContext = await _orchestrationContextBuilder.BuildAsync(profile, ctx =>
        {
            ctx.UserMessage = prompt;
            ctx.ConversationHistory = transcript.ToList();
            ctx.CompletionContext.AdditionalProperties["Session"] = chatSession;
        });

        // Store the session in the invocation context so document tools can resolve session documents.
        AIInvocationScope.Current.Items[nameof(AIChatSession)] = chatSession;
        AIInvocationScope.Current.DataSourceId = orchestratorContext.CompletionContext.DataSourceId;

        // Resolve the orchestrator for this profile and execute the completion.
        var orchestrator = _orchestratorResolver.Resolve(profile.OrchestratorName);

        var contentItemIds = new HashSet<string>();
        var references = new Dictionary<string, AICompletionReference>();
        var stopwatch = Stopwatch.StartNew();

        // Collect preemptive RAG references before streaming so the first chunk
        // already contains any references from data sources and documents.
        _citationCollector.CollectPreemptiveReferences(orchestratorContext, references, contentItemIds);

        await foreach (var chunk in orchestrator.ExecuteStreamingAsync(orchestratorContext, cancellationToken))
        {
            if (string.IsNullOrEmpty(chunk.Text))
            {
                continue;
            }

            builder.Append(chunk.Text);

            // Incrementally collect any new tool references that appeared during streaming
            // (e.g., from DataSourceSearchTool or SearchDocumentsTool invoked by the AI model).
            _citationCollector.CollectToolReferences(references, contentItemIds);

            var partialMessage = new CompletionPartialMessage
            {
                SessionId = chatSession.SessionId,
                MessageId = assistantMessage.ItemId,
                Content = chunk.Text,
                References = references,
            };

            await writer.WriteAsync(partialMessage, cancellationToken);
        }

        // Final pass to collect any tool references added by the last tool call.
        _citationCollector.CollectToolReferences(references, contentItemIds);

        stopwatch.Stop();

        if (builder.Length > 0)
        {
            assistantMessage.Content = builder.ToString();
            assistantMessage.ContentItemIds = contentItemIds.ToList();
            assistantMessage.References = references;

            await _promptStore.CreateAsync(assistantMessage);
        }

        var prompts = await _promptStore.GetPromptsAsync(chatSession.SessionId);

        var context = new ChatMessageCompletedContext
        {
            Profile = profile,
            ChatSession = chatSession,
            Prompts = prompts,
            ResponseLatencyMs = stopwatch.Elapsed.TotalMilliseconds,
        };

        await _sessionHandlers.InvokeAsync((handler, ctx) => handler.MessageCompletedAsync(ctx), context, _logger);

        await _sessionManager.SaveAsync(chatSession);
    }

    private async Task ProcessGeneratedPromptAsync(ChannelWriter<CompletionPartialMessage> writer, AIProfile profile, string sessionId, AIProfile parentProfile, CancellationToken cancellationToken)
    {
        (var chatSession, _) = await GetSessionsAsync(_sessionManager, sessionId, parentProfile, userPrompt: profile.Name);

        var generatedPrompt = await _liquidTemplateManager.RenderStringAsync(profile.PromptTemplate, NullEncoder.Default,
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

        var completionContext = await _completionContextBuilder.BuildAsync(profile, c =>
        {
            c.UserMarkdownInResponse = true;
        });

        var builder = ZString.CreateStringBuilder();

        var contentItemIds = new HashSet<string>();
        var references = new Dictionary<string, AICompletionReference>();

        await foreach (var chunk in _completionService.CompleteStreamingAsync(profile.Source, [new ChatMessage(ChatRole.User, generatedPrompt)], completionContext, cancellationToken))
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

        await _promptStore.CreateAsync(assistantMessage);

        await _sessionManager.SaveAsync(chatSession);
    }

    private async Task ProcessUtilityAsync(ChannelWriter<CompletionPartialMessage> writer, AIProfile profile, string prompt, CancellationToken cancellationToken)
    {
        var messageId = IdGenerator.GenerateId();

        var completionContext = await _completionContextBuilder.BuildAsync(profile, c =>
        {
            c.UserMarkdownInResponse = true;
        });

        var references = new Dictionary<string, AICompletionReference>();

        await foreach (var chunk in _completionService.CompleteStreamingAsync(profile.Source, [new ChatMessage(ChatRole.User, prompt)], completionContext, cancellationToken))
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
}
