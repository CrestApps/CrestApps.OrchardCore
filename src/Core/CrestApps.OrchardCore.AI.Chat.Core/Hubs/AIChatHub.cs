using System.Security.Claims;
using System.Threading.Channels;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Hubs;
using CrestApps.Core.AI.Chat.Models;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Exceptions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.ResponseHandling;
using CrestApps.Core.AI.Security;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.AI.Chat.Core.Services;
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
/// <remarks>
/// This hub intentionally does not carry a blanket <c>[Authorize]</c>. Access is enforced
/// per profile by <see cref="AuthorizeProfileAsync"/> (which checks
/// <see cref="AIPermissions.QueryAnyAIProfile"/> against the specific profile), so a site can
/// grant the Anonymous role access to public profiles. A class-level <c>[Authorize]</c> would
/// reject the SignalR negotiate for anonymous callers before that per-profile check runs, which
/// breaks anonymous chat and the external chat widget hosted in a third-party iframe (where auth
/// cookies are not available). <see cref="AllowAnonymousAttribute"/> is applied explicitly so a
/// future change that secures hubs globally does not silently re-break anonymous access.
/// </remarks>
[AllowAnonymous]
public class AIChatHub : AIChatHubCore<IAIChatHubClient>
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatHub"/> class.
    /// </summary>
    /// <param name="services">The service provider for resolving dependencies.</param>
    /// <param name="clock">The clock for obtaining the current UTC time.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="stringLocalizer">The string localizer for this hub.</param>
    public AIChatHub(
        IServiceProvider services,
        TimeProvider timeProvider,
        ILogger<AIChatHub> logger,
        IStringLocalizer<AIChatHub> stringLocalizer)
        : base(services, timeProvider, logger)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Uses <c>ShellScope.UsingChildScopeAsync</c> so each hub invocation gets
    /// its own <c>ISession</c> / <c>IDocumentStore</c> lifecycle with proper
    /// commit/rollback on disposal.
    /// </summary>
    protected override Task ExecuteInScopeAsync(Func<IServiceProvider, Task> action)
        => ShellScope.UsingChildScopeAsync(scope => action(scope.ServiceProvider));

    protected override async Task<bool> AuthorizeProfileAsync(IServiceProvider services, AIProfile profile)
    {
        var accessEvaluator = services.GetRequiredService<AIChatProfileAccessEvaluator>();
        var httpContext = Context.GetHttpContext();

        return await accessEvaluator.CanAccessProfileAsync(httpContext?.User ?? Context.User, profile);
    }

    /// <summary>
    /// Loads one of the caller's sessions and sends its messages to the caller, logging why a session could not be
    /// loaded.
    /// </summary>
    /// <param name="sessionId">The session id.</param>
    /// <remarks>
    /// This follows <see cref="AIChatHubCore{TClient}.LoadSession(string)"/> step for step. The base answers every
    /// miss with the same "Session not found." error, which is right for the caller -- it must not reveal whether
    /// someone else's session exists -- but leaves nothing in the logs to tell the cases apart.
    /// <para>
    /// The lookup is user-scoped: <see cref="IAIChatSessionManager.FindAsync"/> only returns a session owned by the
    /// caller. A system-owned session (an automated SMS or voice conversation, which carries no user) therefore never
    /// loads here. The admin "Review AI conversation" page reviews those through their owning resource, renders the
    /// transcript itself and does not call this method; the log below names that case when a client still does.
    /// </para>
    /// </remarks>
    public override async Task LoadSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            await base.LoadSession(sessionId);

            return;
        }

        // Captured eagerly, as the base hub does: the caller context is not guaranteed once the invocation returns.
        var user = Context?.User;
        var connectionId = Context?.ConnectionId;
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        await ExecuteInScopeAsync(async services =>
        {
            var userAccessor = services.GetService<IUserAccessor>();

            if (userAccessor is not null)
            {
                userAccessor.User = user;
            }

            var sessionManager = services.GetRequiredService<IAIChatSessionManager>();
            var chatSession = await sessionManager.FindAsync(sessionId);

            if (chatSession is null)
            {
                await LogUnloadableSessionAsync(sessionManager, sessionId, userId, connectionId);
                await Clients.Caller.ReceiveError(GetSessionNotFoundMessage());

                return;
            }

            var profileManager = services.GetRequiredService<IAIProfileManager>();
            var profile = await profileManager.FindByIdAsync(chatSession.ProfileId);

            if (profile is null)
            {
                Logger.LogWarning(
                    "AI chat hub {HubMethod}: session {SessionId} references profile {ProfileId}, which no longer exists. User {UserId}, connection {ConnectionId}.",
                    nameof(LoadSession),
                    sessionId,
                    chatSession.ProfileId,
                    userId,
                    connectionId);

                await Clients.Caller.ReceiveError(GetProfileNotFoundMessage());

                return;
            }

            if (!await AuthorizeProfileAsync(services, profile))
            {
                Logger.LogWarning(
                    "AI chat hub {HubMethod}: user {UserId} is not authorized for profile {ProfileId} of session {SessionId}. Connection {ConnectionId}.",
                    nameof(LoadSession),
                    userId,
                    profile.ItemId,
                    sessionId,
                    connectionId);

                await Clients.Caller.ReceiveError(GetNotAuthorizedMessage());

                return;
            }

            var promptStore = services.GetRequiredService<IAIChatSessionPromptStore>();
            var prompts = await promptStore.GetPromptsAsync(chatSession.SessionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, GetSessionGroupName(chatSession.SessionId));
            await Clients.Caller.LoadSession(CreateSessionPayload(chatSession, profile, prompts));

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug(
                    "AI chat hub {HubMethod}: loaded session {SessionId} of profile {ProfileId} with {MessageCount} messages for user {UserId} on connection {ConnectionId}.",
                    nameof(LoadSession),
                    sessionId,
                    profile.ItemId,
                    prompts.Count,
                    userId,
                    connectionId);
            }
        });
    }

    private async Task LogUnloadableSessionAsync(
        IAIChatSessionManager sessionManager,
        string sessionId,
        string userId,
        string connectionId)
    {
        // The unscoped lookup only classifies the miss for the log. The caller still gets the generic error.
        var storedSession = await sessionManager.FindByIdAsync(sessionId);

        if (storedSession is null)
        {
            Logger.LogWarning(
                "AI chat hub {HubMethod}: session {SessionId} does not exist. User {UserId}, connection {ConnectionId}.",
                nameof(LoadSession),
                sessionId,
                userId,
                connectionId);

            return;
        }

        if (string.IsNullOrEmpty(storedSession.UserId) && string.IsNullOrEmpty(storedSession.ClientId))
        {
            Logger.LogWarning(
                "AI chat hub {HubMethod}: session {SessionId} of profile {ProfileId} is system-owned (no user or visitor), so the user-scoped lookup cannot return it. System sessions are reviewed read-only through their owning resource and are not loaded over the hub. User {UserId}, connection {ConnectionId}.",
                nameof(LoadSession),
                sessionId,
                storedSession.ProfileId,
                userId,
                connectionId);

            return;
        }

        Logger.LogWarning(
            "AI chat hub {HubMethod}: session {SessionId} of profile {ProfileId} belongs to another owner (owner is a {OwnerKind}) and was not loaded for user {UserId}. Connection {ConnectionId}.",
            nameof(LoadSession),
            sessionId,
            storedSession.ProfileId,
            string.IsNullOrEmpty(storedSession.UserId) ? "visitor" : "user",
            userId,
            connectionId);
    }

    protected override DateTime GetUtcNow()
    {
        var clock = Context.GetHttpContext()?.RequestServices?.GetService<IClock>();
        return clock?.UtcNow ?? base.GetUtcNow();
    }

    protected override string GenerateId()
        => IdGenerator.GenerateId();

    protected override string DefaultBlankSessionTitle
        => AIConstants.DefaultBlankSessionTitle;

    protected override async Task<DefaultAIDeploymentSettings> GetDeploymentSettingsAsync(IServiceProvider services)
    {
        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.GetSiteSettingsAsync();

        return site.GetOrCreate<DefaultAIDeploymentSettings>();
    }

    protected override string GetRequiredFieldMessage(string fieldName)
        => S["{0} is required.", fieldName].Value;

    protected override string GetProfileNotFoundMessage()
        => S["Profile not found."].Value;

    protected override string GetSessionNotFoundMessage()
        => S["Session not found."].Value;

    protected override string GetNotAuthorizedMessage()
        => S["You are not authorized to interact with the given profile."].Value;

    /// <summary>
    /// Gets the message shown when a caller is throttled while starting a new chat session.
    /// </summary>
    /// <param name="result">The rate-limit result.</param>
    /// <remarks>
    /// The message deliberately discloses neither the configured limit, the current count, nor the
    /// retry delay: those values let an abuser tune their traffic to sit just under the throttle. It
    /// still tells a legitimate visitor what happened and that waiting resolves it.
    /// </remarks>
    protected override string GetSessionStartRateLimitMessage(RateLimitResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return S["You've reached the limit for starting new chats. Please wait a few minutes and try again."].Value;
    }

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

    protected override string GetTranscriptionErrorMessage(Exception ex = null)
        => S["An error occurred while transcribing the audio. Please try again."].Value;

    protected override string GetSpeechSynthesisErrorMessage(Exception ex = null)
        => S["An error occurred while synthesizing speech. Please try again."].Value;

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

    protected override async Task OnMessageRatedAsync(
        IServiceProvider services,
        AIChatSession chatSession,
        IAIChatSessionPromptStore promptStore)
    {
        var eventService = services.GetService<IAIChatSessionEventService>();

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

    /// <summary>
    /// Extends the base handler to support <see cref="AIProfileType.TemplatePrompt"/> profiles
    /// which render a Liquid template and send the result to the AI completion service.
    /// The base class does not handle TemplatePrompt profiles, so this override intercepts
    /// them and delegates everything else to the base implementation.
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
        if (!string.IsNullOrWhiteSpace(profileId))
        {
            var profileManager = services.GetRequiredService<IAIProfileManager>();
            var profile = await profileManager.FindByIdAsync(profileId, cancellationToken);

            if (profile?.Type == AIProfileType.TemplatePrompt)
            {
                try
                {
                    using var invocationScope = AIInvocationScope.Begin();

                    if (string.IsNullOrWhiteSpace(sessionProfileId))
                    {
                        await Clients.Caller.ReceiveError(GetRequiredFieldMessage(nameof(sessionProfileId)));

                        return;
                    }

                    var parentProfile = await profileManager.FindByIdAsync(sessionProfileId, cancellationToken);

                    if (parentProfile is null)
                    {
                        await Clients.Caller.ReceiveError(S["Invalid value given to {0}.", nameof(sessionProfileId)].Value);

                        return;
                    }

                    if (!await AuthorizeProfileAsync(services, parentProfile))
                    {
                        await Clients.Caller.ReceiveError(GetNotAuthorizedMessage());

                        return;
                    }

                    await ProcessGeneratedPromptAsync(writer, services, profile, sessionId, parentProfile, cancellationToken);
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

                return;
            }
        }

        await base.HandleSendMessageAsync(writer, services, profileId, prompt, sessionId, sessionProfileId, cancellationToken);
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
        AIChatSession chatSession;

        // This override replaces the base implementation entirely, so it has to repeat the base
        // class's session-start error handling. A throttled session start is signalled distinctly
        // from a generic error so the client can show it without its clear-and-retry recovery.
        try
        {
            (chatSession, _) = await GetOrCreateSessionAsync(services, sessionId, parentProfile, userPrompt: profile.Name);
        }
        catch (ChatSessionStartRateLimitedException ex)
        {
            await Clients.Caller.ReceiveSessionStartRejected(ex.Message);

            return;
        }
        catch (InvalidOperationException ex)
        {
            await Clients.Caller.ReceiveError(ex.Message);

            return;
        }

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
        }, cancellationToken);

        var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();
        var chatDeployment = await deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, deploymentName: completionContext.ChatDeploymentName, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Unable to resolve a chat deployment for the profile.");

        using var builder = ZString.CreateStringBuilder();
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

        await promptStore.CreateAsync(assistantMessage, cancellationToken);
        await SaveChatSessionAsync(services, sessionManager, chatSession);
    }
}
