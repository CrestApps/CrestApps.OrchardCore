using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading.Channels;
using CrestApps.AI.Prompting.Services;
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

namespace CrestApps.OrchardCore.AI.Chat.Hubs;

public class AIChatHub : Hub<IAIChatHubClient>
{
    private readonly ILogger<AIChatHub> _logger;

    protected readonly IStringLocalizer S;

    public AIChatHub(
        ILogger<AIChatHub> logger,
        IStringLocalizer<AIChatHub> stringLocalizer)
    {
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

            await Clients.Caller.LoadSession(CreateSessionPayload(chatSession, profile, prompts));
        });
    }

    public async Task StartSession(string profileId)
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

            var chatSession = await sessionManager.NewAsync(profile, new NewAIChatSessionContext());
            await sessionManager.SaveAsync(chatSession);
            var prompts = await promptStore.GetPromptsAsync(chatSession.SessionId);

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

    private async Task HandlePromptAsync(ChannelWriter<CompletionPartialMessage> writer, string profileId, string prompt, string sessionId, string sessionProfileId, CancellationToken cancellationToken)
    {
        try
        {
            // Each hub invocation gets its own child scope for proper ISession/IDocumentStore lifecycle.
            // This ensures each invocation has a fresh YesSql ISession with auto-commit on disposal,
            // preventing cross-invocation state leakage in long-lived SignalR connections.
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                using var invocationScope = AIInvocationScope.Begin();
                var services = scope.ServiceProvider;

                try
                {
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
            });
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

        var titleSystemMessage = await aiTemplateService.RenderAsync(AITemplateIds.TitleGeneration);

        var context = await completionContextBuilder.BuildAsync(profile, c =>
        {
            c.SystemMessage = titleSystemMessage;
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

        var titleResponse = await completionService.CompleteAsync(profile.Source,
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
        var orchestrationContextBuilder = services.GetRequiredService<IOrchestrationContextBuilder>();
        var orchestratorResolver = services.GetRequiredService<IOrchestratorResolver>();
        var sessionHandlers = services.GetRequiredService<IEnumerable<IAIChatSessionHandler>>();
        var citationCollector = services.GetRequiredService<CitationReferenceCollector>();
        var clock = services.GetRequiredService<IClock>();

        (var chatSession, var isNew) = await GetSessionAsync(services, sessionId, profile, prompt);

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

        var assistantMessage = new AIChatSessionPrompt
        {
            ItemId = IdGenerator.GenerateId(),
            SessionId = chatSession.SessionId,
            Role = ChatRole.Assistant,
            Title = profile.PromptSubject,
        };

        var builder = ZString.CreateStringBuilder();

        // Build the orchestration context using the handler pipeline.
        var orchestratorContext = await orchestrationContextBuilder.BuildAsync(profile, ctx =>
        {
            ctx.UserMessage = prompt;
            ctx.ConversationHistory = conversationHistory;
            ctx.CompletionContext.AdditionalProperties["Session"] = chatSession;
        });

        // Store the session in the invocation context so document tools can resolve session documents.
        AIInvocationScope.Current.Items[nameof(AIChatSession)] = chatSession;
        AIInvocationScope.Current.DataSourceId = orchestratorContext.CompletionContext.DataSourceId;

        // Resolve the orchestrator for this profile and execute the completion.
        var orchestrator = orchestratorResolver.Resolve(profile.OrchestratorName);

        var contentItemIds = new HashSet<string>();
        var references = new Dictionary<string, AICompletionReference>();
        var stopwatch = Stopwatch.StartNew();

        // Collect preemptive RAG references before streaming so the first chunk
        // already contains any references from data sources and documents.
        citationCollector.CollectPreemptiveReferences(orchestratorContext, references, contentItemIds);

        await foreach (var chunk in orchestrator.ExecuteStreamingAsync(orchestratorContext, cancellationToken))
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
            c.UserMarkdownInResponse = true;
        });

        var builder = ZString.CreateStringBuilder();

        var contentItemIds = new HashSet<string>();
        var references = new Dictionary<string, AICompletionReference>();

        await foreach (var chunk in completionService.CompleteStreamingAsync(profile.Source, [new ChatMessage(ChatRole.User, generatedPrompt)], completionContext, cancellationToken))
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

        var messageId = IdGenerator.GenerateId();

        var completionContext = await completionContextBuilder.BuildAsync(profile, c =>
        {
            c.UserMarkdownInResponse = true;
        });

        var references = new Dictionary<string, AICompletionReference>();

        await foreach (var chunk in completionService.CompleteStreamingAsync(profile.Source, [new ChatMessage(ChatRole.User, prompt)], completionContext, cancellationToken))
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

    public async Task SendAudioStream(string profileId, string sessionId, IAsyncEnumerable<string> audioChunks, string audioFormat = null)
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

                var site = await siteService.GetSiteSettingsAsync();
                var deploymentSettings = site.As<DefaultAIDeploymentSettings>();

                if (string.IsNullOrEmpty(deploymentSettings.DefaultSpeechToTextDeploymentId))
                {
                    await Clients.Caller.ReceiveError(S["No speech-to-text deployment is configured."].Value);
                    return;
                }

                var deployment = await deploymentManager.FindByIdAsync(deploymentSettings.DefaultSpeechToTextDeploymentId);

                if (deployment is null)
                {
                    await Clients.Caller.ReceiveError(S["The configured speech-to-text deployment was not found."].Value);
                    return;
                }

#pragma warning disable MEAI001
                var sttClient = await clientFactory.CreateSpeechToTextClientAsync(deployment);
#pragma warning restore MEAI001

                await StreamTranscriptionAsync(sttClient, sessionId ?? string.Empty, audioChunks, audioFormat, cancellationToken);
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
        ISpeechToTextClient sttClient,
        string sessionId,
        IAsyncEnumerable<string> audioChunks,
        string audioFormat,
        CancellationToken cancellationToken)
    {
        var pipe = new Pipe();

        // Cancellation source to break the audio chunk loop when transcription fails.
        using var errorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start streaming transcription in the background.
        var transcriptionTask = TranscribeAsync(sessionId, pipe, audioFormat, sttClient, errorCts, cancellationToken);

        // Write audio chunks to the pipe as they arrive from SignalR.
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
            // Transcription failed; stop consuming audio chunks.
            if (cancellationToken.IsCancellationRequested)
            {

            }
        }

        // Signal that all audio has been sent.
        await pipe.Writer.CompleteAsync();

        // Wait for the transcription to finish processing all audio.
        await transcriptionTask;
    }

    private async Task TranscribeAsync(string sessionId, Pipe pipe, string audioFormat, ISpeechToTextClient sttClient, CancellationTokenSource errorCts, CancellationToken cancellationToken)
    {
        try
        {
            await using var readerStream = pipe.Reader.AsStream();

            using var committedText = ZString.CreateStringBuilder();
            var sttOptions = new SpeechToTextOptions
            {
                SpeechLanguage = "en-US",
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
                    var display = committedText.ToString() + update.Text;
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
            // Cancel the audio chunk loop so the error surfaces immediately.
            await errorCts.CancelAsync();
            throw;
        }
    }
#pragma warning restore MEAI001
}
