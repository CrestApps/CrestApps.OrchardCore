using System.Threading.Channels;
using CrestApps.AI;
using CrestApps.AI.Chat.Hubs;
using CrestApps.AI.Chat.Models;
using CrestApps.AI.Models;
using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
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

/// <summary>
/// OrchardCore-specific AI chat hub. Inherits all behavior from <see cref="AIChatHubCore"/>
/// and overrides hooks to integrate with OrchardCore's scoping, authorization, localization,
/// analytics, and citation systems.
/// </summary>
public class AIChatHub : AIChatHubCore<IAIChatHubClient>
{
    private readonly IStringLocalizer S;

    public AIChatHub(
        IServiceProvider services,
        ILogger<AIChatHub> logger,
        IStringLocalizer<AIChatHub> stringLocalizer)
        : base(services, logger)
    {
        S = stringLocalizer;
    }

    // ───────────────────── Scope override ─────────────────────

    /// <summary>
    /// Uses <c>ShellScope.UsingChildScopeAsync</c> so each hub invocation gets
    /// its own <c>ISession</c> / <c>IDocumentStore</c> lifecycle with proper
    /// commit/rollback on disposal.
    /// </summary>
    protected override Task ExecuteInScopeAsync(Func<IServiceProvider, Task> action)
        => ShellScope.UsingChildScopeAsync(scope => action(scope.ServiceProvider));

    // ──────────────────── Authorization ────────────────────

    protected override async Task<bool> AuthorizeProfileAsync(IServiceProvider services, AIProfile profile)
    {
        var authorizationService = services.GetRequiredService<IAuthorizationService>();
        var httpContext = Context.GetHttpContext();

        return await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile);
    }

    // ──────────────── Time / ID generation ────────────────

    protected override DateTime GetUtcNow()
    {
        var clock = Context.GetHttpContext()?.RequestServices?.GetService<IClock>();
        return clock?.UtcNow ?? DateTime.UtcNow;
    }

    protected override string GenerateId()
        => IdGenerator.GenerateId();

    protected override string DefaultBlankSessionTitle
        => AIConstants.DefaultBlankSessionTitle;

    // ─────────────── Deployment settings ───────────────

    protected override async Task<DefaultAIDeploymentSettings> GetDeploymentSettingsAsync(IServiceProvider services)
    {
        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.GetSiteSettingsAsync();

        return site.As<DefaultAIDeploymentSettings>();
    }

    // ──────────────────── Error messages ────────────────────

    protected override string GetRequiredFieldMessage(string fieldName)
        => S["{0} is required.", fieldName].Value;

    protected override string GetProfileNotFoundMessage()
        => S["Profile not found."].Value;

    protected override string GetSessionNotFoundMessage()
        => S["Session not found."].Value;

    protected override string GetNotAuthorizedMessage()
        => S["You are not authorized to interact with the given profile."].Value;

    protected override string GetFriendlyErrorMessage(Exception ex)
        => AIHubErrorMessageHelper.GetFriendlyErrorMessage(ex, S).Value;

    protected override string GetOnlyChatProfilesMessage()
        => S["Only chat profiles can start chat sessions."].Value;

    protected override string GetConversationNotEnabledMessage()
        => S["Conversation mode is not enabled for this profile."].Value;

    protected override string GetNoSttDeploymentMessage()
        => S["No speech-to-text deployment is configured."].Value;

    protected override string GetNoTtsDeploymentMessage()
        => S["No text-to-speech deployment is configured."].Value;

    protected override string GetSttDeploymentNotFoundMessage()
        => S["The configured speech-to-text deployment was not found."].Value;

    protected override string GetTtsDeploymentNotFoundMessage()
        => S["The configured text-to-speech deployment was not found."].Value;

    protected override string GetTtsNotEnabledMessage()
        => S["Text-to-speech is not enabled for this profile."].Value;

    protected override string GetConversationErrorMessage()
        => S["An error occurred during the conversation. Please try again."].Value;

    protected override string GetNotificationActionErrorMessage()
        => S["An error occurred while processing your action. Please try again."].Value;

    protected override string GetTranscriptionErrorMessage()
        => S["An error occurred while transcribing the audio. Please try again."].Value;

    protected override string GetSpeechSynthesisErrorMessage()
        => S["An error occurred while synthesizing speech. Please try again."].Value;

    // ────────────── Citation collection ──────────────

    protected override void CollectStreamingReferences(
        IServiceProvider services,
        ChatResponseHandlerContext handlerContext,
        Dictionary<string, AICompletionReference> references,
        HashSet<string> contentItemIds)
    {
        var citationCollector = services.GetRequiredService<CitationReferenceCollector>();

        // Collect preemptive RAG references if the handler produced an OrchestrationContext.
        if (handlerContext.Properties.TryGetValue("OrchestrationContext", out var ctxObj) && ctxObj is OrchestrationContext orchestratorContext)
        {
            citationCollector.CollectPreemptiveReferences(orchestratorContext, references, contentItemIds);

            // Remove the key after initial collection to avoid re-collecting on subsequent chunks.
            handlerContext.Properties.Remove("OrchestrationContext");
        }

        // Collect tool references added during streaming.
        citationCollector.CollectToolReferences(references, contentItemIds);
    }

    // ───────────── Post-completion analytics ─────────────

    protected override async Task OnMessageRatedAsync(
        IServiceProvider services,
        AIChatSession chatSession,
        IAIChatSessionPromptStore promptStore)
    {
        var eventService = services.GetService<AIChatSessionEventService>();

        if (eventService is null)
        {
            return;
        }

        var allPrompts = await promptStore.GetPromptsAsync(chatSession.SessionId);
        var ratings = allPrompts
            .Where(p => p.UserRating.HasValue)
            .Select(p => p.UserRating.Value)
            .ToList();

        if (ratings.Count > 0)
        {
            var thumbsUpCount = ratings.Count(r => r);
            var thumbsDownCount = ratings.Count(r => !r);
            await eventService.RecordUserRatingAsync(chatSession.SessionId, thumbsUpCount, thumbsDownCount);
        }
    }

    // ──────────── TemplatePrompt profile support ────────────

    /// <summary>
    /// Extends the base handler to support <see cref="AIProfileType.TemplatePrompt"/> profiles
    /// which render a Liquid template and send the result to the AI completion service.
    /// </summary>
    protected override async Task HandleSendMessageAsync(
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

            if (profile.Type == AIProfileType.TemplatePrompt)
            {
                if (string.IsNullOrWhiteSpace(sessionProfileId))
                {
                    await Clients.Caller.ReceiveError(GetRequiredFieldMessage(nameof(sessionProfileId)));
                    return;
                }

                var parentProfile = await profileManager.FindByIdAsync(sessionProfileId);

                if (parentProfile is null)
                {
                    await Clients.Caller.ReceiveError(S["Invalid value given to {0}.", nameof(sessionProfileId)].Value);
                    return;
                }

                await ProcessGeneratedPromptAsync(writer, services, profile, sessionId, parentProfile, cancellationToken);
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
    /// Processes a TemplatePrompt profile by rendering a Liquid template and streaming
    /// the AI response. This is OrchardCore-specific because it depends on
    /// <see cref="ILiquidTemplateManager"/>.
    /// </summary>
    protected override async Task ProcessGeneratedPromptAsync(
        ChannelWriter<CompletionPartialMessage> writer,
        IServiceProvider services,
        AIProfile profile,
        string sessionId,
        AIProfile parentProfile,
        CancellationToken cancellationToken)
    {
        var sessionManager = services.GetRequiredService<IAIChatSessionManager>();
        var promptStore = services.GetRequiredService<IAIChatSessionPromptStore>();
        var liquidTemplateManager = services.GetRequiredService<ILiquidTemplateManager>();
        var completionContextBuilder = services.GetRequiredService<IAICompletionContextBuilder>();
        var completionService = services.GetRequiredService<IAICompletionService>();

        var (chatSession, _) = await GetOrCreateSessionAsync(services, sessionId, parentProfile, userPrompt: profile.Name);

        var generatedPrompt = await liquidTemplateManager.RenderStringAsync(profile.PromptTemplate, NullEncoder.Default,
            new Dictionary<string, FluidValue>
            {
                ["Profile"] = new ObjectValue(profile),
                ["Session"] = new ObjectValue(chatSession),
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
                SessionId = chatSession.SessionId,
                MessageId = assistantMessage.ItemId,
                Content = chunk.Text,
                References = references,
            };

            await writer.WriteAsync(partialMessage, cancellationToken);
        }

        assistantMessage.Content = builder.ToString();
        assistantMessage.References = references;

        await promptStore.CreateAsync(assistantMessage);
        await sessionManager.SaveAsync(chatSession);
    }
}
