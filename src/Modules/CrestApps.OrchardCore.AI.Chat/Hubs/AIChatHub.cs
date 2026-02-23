using System.Text;
using System.Threading.Channels;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.Support;
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
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Chat.Hubs;

public class AIChatHub : Hub<IAIChatHubClient>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIProfileManager _profileManager;
    private readonly IAIChatSessionManager _sessionManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IDocumentStore _documentStore;
    private readonly IAICompletionService _completionService;
    private readonly IAICompletionContextBuilder _completionContextBuilder;
    private readonly IOrchestrationContextBuilder _orchestrationContextBuilder;
    private readonly IOrchestratorResolver _orchestratorResolver;
    private readonly DataExtractionService _dataExtractionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger<AIChatHub> _logger;

    protected readonly IStringLocalizer S;

    public AIChatHub(
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager,
        IAIChatSessionManager sessionManager,
        ILiquidTemplateManager liquidTemplateManager,
        IDocumentStore documentStore,
        IAICompletionService completionService,
        IAICompletionContextBuilder completionContextBuilder,
        IOrchestrationContextBuilder orchestrationContextBuilder,
        IOrchestratorResolver orchestratorResolver,
        DataExtractionService dataExtractionService,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<AIChatHub> logger,
        IStringLocalizer<AIChatHub> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _profileManager = profileManager;
        _sessionManager = sessionManager;
        _liquidTemplateManager = liquidTemplateManager;
        _documentStore = documentStore;
        _completionService = completionService;
        _completionContextBuilder = completionContextBuilder;
        _orchestrationContextBuilder = orchestrationContextBuilder;
        _orchestratorResolver = orchestratorResolver;
        _dataExtractionService = dataExtractionService;
        _serviceProvider = serviceProvider;
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

        await Clients.Caller.LoadSession(new
        {
            chatSession.SessionId,
            Profile = new
            {
                Id = chatSession.ProfileId,
                Type = profile.Type.ToString()
            },
            chatSession.Documents,
            Messages = chatSession.Prompts.Select(message => new AIChatResponseMessageDetailed
            {
                Id = message.Id,
                Role = message.Role.Value,
                IsGeneratedPrompt = message.IsGeneratedPrompt,
                Title = message.Title,
                Content = message.Content,
                References = message.References,
            })
        });
    }

    private async Task HandlePromptAsync(ChannelWriter<CompletionPartialMessage> writer, string profileId, string prompt, string sessionId, string sessionProfileId, CancellationToken cancellationToken)
    {
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

        chatSession.Prompts.Add(new AIChatSessionPrompt
        {
            Id = IdGenerator.GenerateId(),
            Role = ChatRole.User,
            Content = prompt,
        });

        var transcript = chatSession.Prompts
            .Where(x => !x.IsGeneratedPrompt)
            .Select(prompt => new ChatMessage(prompt.Role, prompt.Content));

        var assistantMessage = new AIChatSessionPrompt
        {
            Id = IdGenerator.GenerateId(),
            Role = ChatRole.Assistant,
            Title = profile.PromptSubject,
        };

        var builder = new StringBuilder();

        // Build the orchestration context using the handler pipeline.
        var orchestratorContext = await _orchestrationContextBuilder.BuildAsync(profile, ctx =>
        {
            ctx.UserMessage = prompt;
            ctx.ConversationHistory = transcript.ToList();
            ctx.CompletionContext.AdditionalProperties["Session"] = chatSession;
        });

        // Store the session in HttpContext so document tools can resolve session documents.
        var httpContext = Context.GetHttpContext();
        httpContext.Items[nameof(AIChatSession)] = chatSession;

        // Resolve the orchestrator for this profile and execute the completion.
        var orchestrator = _orchestratorResolver.Resolve(profile.OrchestratorName);

        var contentItemIds = new HashSet<string>();
        var references = new Dictionary<string, AICompletionReference>();

        await foreach (var chunk in orchestrator.ExecuteStreamingAsync(orchestratorContext, cancellationToken))
        {
            if (chunk.AdditionalProperties is not null)
            {
                if (chunk.AdditionalProperties.TryGetValue<IList<string>>("ContentItemIds", out var ids))
                {
                    foreach (var id in ids)
                    {
                        contentItemIds.Add(id);
                    }
                }

                if (chunk.AdditionalProperties.TryGetValue<Dictionary<string, AICompletionReference>>("References", out var referenceItems))
                {
                    foreach (var (key, value) in referenceItems)
                    {
                        references[key] = value;
                    }
                }
            }

            if (string.IsNullOrEmpty(chunk.Text))
            {
                continue;
            }

            builder.Append(chunk.Text);

            var partialMessage = new CompletionPartialMessage
            {
                SessionId = chatSession.SessionId,
                MessageId = assistantMessage.Id,
                Content = chunk.Text,
                References = references,
            };

            await writer.WriteAsync(partialMessage, cancellationToken);
        }

        if (builder.Length > 0)
        {
            assistantMessage.Content = builder.ToString();
            assistantMessage.ContentItemIds = contentItemIds.ToList();
            assistantMessage.References = references;

            chatSession.Prompts.Add(assistantMessage);
        }

        // Run data extraction after the main AI response.
        await RunDataExtractionAsync(profile, chatSession, prompt, assistantMessage.Content, utcNow, cancellationToken);

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
            Id = IdGenerator.GenerateId(),
            Role = ChatRole.Assistant,
            IsGeneratedPrompt = true,
            Title = profile.PromptSubject,
        };

        var completionContext = await _completionContextBuilder.BuildAsync(profile, c =>
        {
            c.UserMarkdownInResponse = true;
        });

        var builder = new StringBuilder();

        var contentItemIds = new HashSet<string>();
        var references = new Dictionary<string, AICompletionReference>();

        await foreach (var chunk in _completionService.CompleteStreamingAsync(profile.Source, [new ChatMessage(ChatRole.User, generatedPrompt)], completionContext, cancellationToken))
        {
            if (chunk.AdditionalProperties is not null)
            {
                if (chunk.AdditionalProperties.TryGetValue<IList<string>>("ContentItemIds", out var ids))
                {
                    foreach (var id in ids)
                    {
                        contentItemIds.Add(id);
                    }
                }

                if (chunk.AdditionalProperties.TryGetValue<Dictionary<string, AICompletionReference>>("References", out var referenceItems))
                {
                    foreach (var (key, value) in referenceItems)
                    {
                        references[key] = value;
                    }
                }
            }

            if (string.IsNullOrEmpty(chunk.Text))
            {
                continue;
            }

            builder.Append(chunk.Text);

            var partialMessage = new CompletionPartialMessage
            {
                SessionId = sessionId,
                MessageId = assistantMessage.Id,
                Content = chunk.Text,
                References = references,
            };

            await writer.WriteAsync(partialMessage, cancellationToken);
        }

        assistantMessage.Content = builder.ToString();
        assistantMessage.ContentItemIds = contentItemIds.ToList();
        assistantMessage.References = references;

        chatSession.Prompts.Add(assistantMessage);

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
            if (chunk.AdditionalProperties is not null)
            {
                if (chunk.AdditionalProperties.TryGetValue<Dictionary<string, AICompletionReference>>("References", out var referenceItems))
                {
                    foreach (var (key, value) in referenceItems)
                    {
                        references[key] = value;
                    }
                }
            }

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

    private async Task RunDataExtractionAsync(
        AIProfile profile,
        AIChatSession chatSession,
        string userMessage,
        string assistantMessage,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        try
        {
            var settings = profile.GetSettings<AIProfileDataExtractionSettings>();

            var promptCount = chatSession.Prompts.Count(p => p.Role == ChatRole.User);

            if (!DataExtractionService.ShouldExtract(settings, promptCount))
            {
                return;
            }

            var fieldsToExtract = DataExtractionService.GetFieldsToExtract(settings, chatSession);

            if (fieldsToExtract.Count == 0)
            {
                return;
            }

            var results = await _dataExtractionService.ExtractAsync(
                profile, chatSession, fieldsToExtract,
                assistantMessage, userMessage, cancellationToken);

            if (results.Count == 0)
            {
                return;
            }

            var changeSet = DataExtractionService.ApplyExtraction(chatSession, settings, results, utcNow);

            // Trigger workflow events for each newly extracted field.
            foreach (var field in changeSet.NewFields)
            {
                await TriggerFieldExtractedEventAsync(chatSession, profile, field, utcNow);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data extraction failed for session '{SessionId}'.", chatSession.SessionId);
        }
    }

    private async Task TriggerFieldExtractedEventAsync(AIChatSession chatSession, AIProfile profile, ExtractedFieldChange field, DateTime utcNow)
    {
        try
        {
            var workflowManager = _serviceProvider.GetService<IWorkflowManager>();

            if (workflowManager == null)
            {
                return;
            }

            var input = new Dictionary<string, object>
            {
                { "SessionId", chatSession.SessionId },
                { "ProfileId", profile.ItemId },
                { "FieldName", field.FieldName },
                { "Value", field.Value },
                { "IsMultiple", field.IsMultiple },
                { "Timestamp", utcNow },
            };

            await workflowManager.TriggerEventAsync(
                nameof(AIChatSessionFieldExtractedEvent),
                input,
                correlationId: chatSession.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to trigger AIChatSessionFieldExtractedEvent for session '{SessionId}'.", chatSession.SessionId);
        }
    }

}
