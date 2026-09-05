using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Handlers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Modules;
using OrchardCore.Sms;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.BackgroundTasks;

/// <summary>
/// Proactively re-engages automated SMS contacts who have gone quiet, when the loading campaign enabled it.
/// </summary>
/// <remarks>
/// When a contact does not reply for longer than the configured silence window, this task sends an AI-composed
/// follow-up to invite a response, up to the configured maximum. Every send here is background-initiated, so it is
/// gated by the campaign's business-hours calendar evaluated in the contact's local time zone — we never nudge a
/// contact after hours. (A live reply to a contact who is actively messaging goes through the inbound webhook, not
/// this task, and is never gated.) The per-conversation lock and the single-active-generation registry are honored so
/// a nudge can never collide with a live reply.
/// </remarks>
[BackgroundTask(
    Title = "Automated SMS Re-Engagement",
    Schedule = "*/5 * * * *",
    Description = "Sends follow-up messages to automated SMS contacts who have gone quiet, within business hours.",
    LockTimeout = 5_000,
    LockExpiration = _leaseMilliseconds)]
public sealed class SmsReEngagementBackgroundTask : IBackgroundTask
{
    private const int _leaseMilliseconds = 300_000;
    private const int _batchSize = 100;
    private const int _maxConversationsPerInvocation = 200;

    private const string ReEngagementSystemPromptPrefix =
        """
        You are the sales agent in an ongoing SMS conversation with a customer who has not replied to your last message.
        Write a brief, friendly follow-up that re-engages them and invites a response. Follow this guidance from the
        campaign:
        """;

    private const string ReEngagementSystemPromptSuffix =
        """
        Keep it short (one or two sentences), natural, and do not repeat your previous message word for word. Reply with
        only the message text to send — no preamble, quotes, or labels.
        """;

    private static string BuildReEngagementSystemMessage(string guidance)
        => string.IsNullOrWhiteSpace(guidance)
            ? $"{ReEngagementSystemPromptPrefix}\n{ReEngagementSystemPromptSuffix}"
            : $"{ReEngagementSystemPromptPrefix} {guidance.Trim()}\n{ReEngagementSystemPromptSuffix}";

    /// <summary>
    /// Asynchronously performs the do work operation.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<SmsReEngagementBackgroundTask>>();

        var session = serviceProvider.GetRequiredService<ISession>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var promptStore = serviceProvider.GetRequiredService<IAIChatSessionPromptStore>();
        var chatSessionManager = serviceProvider.GetRequiredService<IAIChatSessionManager>();
        var profileManager = serviceProvider.GetRequiredService<IAIProfileManager>();
        var deploymentManager = serviceProvider.GetRequiredService<IAIDeploymentManager>();
        var contextBuilder = serviceProvider.GetRequiredService<IAICompletionContextBuilder>();
        var completionService = serviceProvider.GetRequiredService<IAICompletionService>();
        var endpointCatalog = serviceProvider.GetRequiredService<ICatalog<OmnichannelChannelEndpoint>>();
        var cadenceCatalog = serviceProvider.GetRequiredService<ICatalog<Cadence>>();
        var contentManager = serviceProvider.GetRequiredService<IContentManager>();
        var smsService = serviceProvider.GetRequiredService<ISmsService>();
        var omnichannelActivityStore = serviceProvider.GetRequiredService<IOmnichannelActivityStore>();
        var subjectFlowSettingsService = serviceProvider.GetRequiredService<ISubjectFlowSettingsService>();
        var localLock = serviceProvider.GetRequiredService<ILocalLock>();

        // The business-hours gate is only registered when a feature provides calendars (ContactCenter). Without it,
        // there is no way to know a contact's hours, so we do not nudge at all rather than risk an after-hours send.
        var businessHoursGate = serviceProvider.GetService<IBusinessHoursGate>();

        var deadline = clock.UtcNow.AddMilliseconds(_leaseMilliseconds * 0.6);

        long documentId = 0;
        var processedCount = 0;

        while (processedCount < _maxConversationsPerInvocation && clock.UtcNow < deadline)
        {
            var activities = await session.Query<OmnichannelActivity, OmnichannelActivityIndex>(x =>
                    x.Status == ActivityStatus.AwaitingCustomerAnswer &&
                    x.InteractionType == ActivityInteractionType.Automated &&
                    x.Channel == OmnichannelConstants.Channels.Sms &&
                    x.DocumentId > documentId,
                    collection: OmnichannelConstants.CollectionName)
                .OrderBy(x => x.DocumentId)
                .Take(_batchSize)
                .ListAsync(cancellationToken);

            if (!activities.Any())
            {
                break;
            }

            foreach (var activity in activities)
            {
                if (clock.UtcNow >= deadline)
                {
                    return;
                }

                documentId = activity.Id;
                processedCount++;

                try
                {
                    await TryReEngageAsync(
                        activity,
                        clock,
                        chatSessionManager,
                        promptStore,
                        profileManager,
                        deploymentManager,
                        contextBuilder,
                        completionService,
                        endpointCatalog,
                        cadenceCatalog,
                        contentManager,
                        smsService,
                        omnichannelActivityStore,
                        subjectFlowSettingsService,
                        localLock,
                        businessHoursGate,
                        logger,
                        cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to re-engage the automated SMS contact for Activity {ActivityId}.", activity.ItemId.SanitizeLogValue());
                }
            }
        }
    }

    private static async Task TryReEngageAsync(
        OmnichannelActivity activity,
        IClock clock,
        IAIChatSessionManager chatSessionManager,
        IAIChatSessionPromptStore promptStore,
        IAIProfileManager profileManager,
        IAIDeploymentManager deploymentManager,
        IAICompletionContextBuilder contextBuilder,
        IAICompletionService completionService,
        ICatalog<OmnichannelChannelEndpoint> endpointCatalog,
        ICatalog<Cadence> cadenceCatalog,
        IContentManager contentManager,
        ISmsService smsService,
        IOmnichannelActivityStore omnichannelActivityStore,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        ILocalLock localLock,
        IBusinessHoursGate businessHoursGate,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activity.CadenceId) ||
            string.IsNullOrWhiteSpace(activity.AISessionId) ||
            string.IsNullOrEmpty(activity.ChannelEndpointId))
        {
            return;
        }

        var cadence = await cadenceCatalog.FindByIdAsync(activity.CadenceId, cancellationToken);

        // No schedule, disabled, or already past its last step: nothing more to send. The step count caps the nudges.
        if (cadence is null ||
            !cadence.Enabled ||
            cadence.Steps is not { Count: > 0 } ||
            activity.ReEngagementAttempts >= cadence.Steps.Count)
        {
            return;
        }

        var step = cadence.Steps[activity.ReEngagementAttempts];

        // A live inbound is composing a reply for this conversation right now; the customer is active, so leave it.
        if (SmsOmnichannelEventHandler.IsGenerating(activity.AISessionId))
        {
            return;
        }

        var chatSession = await chatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);

        if (chatSession is null)
        {
            return;
        }

        var prompts = (await promptStore.GetPromptsAsync(chatSession.SessionId))
            .Where(x => !x.IsGeneratedPrompt)
            .ToList();

        // A nudge is only warranted when we are the ones waiting on the customer: the last message must be ours. If
        // the last message is the customer's, a reply is owed instead (handled by the live path / recovery), not a nudge.
        var lastPrompt = prompts.LastOrDefault();

        if (lastPrompt is null || lastPrompt.Role != ChatRole.Assistant)
        {
            return;
        }

        var now = clock.UtcNow;

        // The silence threshold for THIS nudge is the current step's DelayMinutes. Because our last message time
        // (session LastActivityUtc) is updated on every send — the opening and each nudge — this measures the gap since
        // the previous outbound, so successive steps space nudges out and the number of steps caps the total.
        if (step.DelayMinutes <= 0 ||
            chatSession.LastActivityUtc > now.AddMinutes(-step.DelayMinutes))
        {
            return;
        }

        var endpoint = await endpointCatalog.FindByIdAsync(activity.ChannelEndpointId, cancellationToken);

        if (endpoint is null ||
            !string.Equals(endpoint.Channel, activity.Channel, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(endpoint.Value))
        {
            return;
        }

        // Resolve the contact's local time zone so business hours are evaluated where the customer actually is.
        string contactTimeZoneId = null;
        var contact = await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        if (contact is not null)
        {
            contactTimeZoneId = contact.Get<OmnichannelContactPart>(nameof(OmnichannelContactPart))?.TimeZoneId;
        }

        // Every send here is background-initiated, so it must respect business hours. With no gate registered we cannot
        // know the hours, so we decline to nudge rather than risk an after-hours message.
        if (businessHoursGate is null)
        {
            return;
        }

        if (!await businessHoursGate.IsOpenAsync(activity.BusinessHoursCalendarId, now, contactTimeZoneId, cancellationToken))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Skipping SMS re-engagement for Activity {ActivityId}: outside business hours.", activity.ItemId.SanitizeLogValue());
            }

            return;
        }

        var profileId = string.IsNullOrWhiteSpace(chatSession.ProfileId) ? activity.AIProfileId : chatSession.ProfileId;

        if (string.IsNullOrWhiteSpace(profileId))
        {
            return;
        }

        var profile = await profileManager.FindByIdAsync(profileId, cancellationToken);

        if (profile is null || profile.Type != AIProfileType.Chat)
        {
            return;
        }

        // Serialize against the live reply path on the same per-conversation lock. If a reply is being sent we simply
        // skip this pass; the nudge is retried next run.
        var (locker, locked) = await localLock.TryAcquireLockAsync(
            $"SMS_CONVERSATION_{chatSession.SessionId}",
            TimeSpan.Zero,
            TimeSpan.FromMinutes(2));

        if (!locked)
        {
            return;
        }

        await using (locker)
        {
            // Re-read under the lock: a live reply may have just answered, or another node may have nudged.
            var currentActivity = await omnichannelActivityStore.FindByIdAsync(activity.ItemId, cancellationToken);

            if (currentActivity is null ||
                currentActivity.Status != ActivityStatus.AwaitingCustomerAnswer ||
                currentActivity.ReEngagementAttempts >= cadence.Steps.Count)
            {
                return;
            }

            var latestPrompts = (await promptStore.GetPromptsAsync(chatSession.SessionId))
                .Where(x => !x.IsGeneratedPrompt)
                .ToList();

            if (latestPrompts.LastOrDefault()?.Role != ChatRole.Assistant)
            {
                // The customer replied in the meantime; nothing to nudge.
                return;
            }

            string message;

            if (step.IsAiGenerated)
            {
                // The AI composes the nudge from the conversation, guided by the step's optional instruction.
                var transcript = latestPrompts
                    .Select(prompt => new ChatMessage(prompt.Role, prompt.Content))
                    .ToList();

                var context = await contextBuilder.BuildAsync(profile, ctx =>
                {
                    ctx.SystemMessage = BuildReEngagementSystemMessage(step.Message);
                    ctx.DisableTools = true;
                }, cancellationToken);
                context.AdditionalProperties["Session"] = chatSession;

                var deployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentPurpose.Chat, deploymentName: context.ChatDeploymentName, cancellationToken: cancellationToken);

                if (deployment is null)
                {
                    return;
                }

                var completion = await completionService.CompleteAsync(deployment, transcript, context, cancellationToken);
                message = completion?.Messages?.FirstOrDefault()?.Text?.Trim();
            }
            else
            {
                // A defined-message step sends its verbiage exactly as written.
                message = step.Message?.Trim();
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                logger.LogWarning("The re-engagement step produced no content for Activity {ActivityId}; skipping.", activity.ItemId.SanitizeLogValue());

                return;
            }

            var result = await smsService.SendAsync(new SmsMessage
            {
                To = currentActivity.PreferredDestination,
                From = endpoint.Value,
                Body = message,
            }, cancellationToken);

            if (!result.Succeeded)
            {
                logger.LogWarning("The SMS provider reported a failed re-engagement send for Activity {ActivityId}.", activity.ItemId.SanitizeLogValue());

                return;
            }

            await promptStore.CreateAsync(new AIChatSessionPrompt
            {
                ItemId = UniqueId.GenerateId(),
                SessionId = chatSession.SessionId,
                Role = ChatRole.Assistant,
                Content = message,
                CreatedUtc = now,
            }, cancellationToken);

            chatSession.LastActivityUtc = now;
            await chatSessionManager.SaveAsync(chatSession, cancellationToken);

            currentActivity.ReEngagementAttempts++;
            currentActivity.LastReEngagementUtc = now;

            // Restart the no-response window so the contact gets the full timeout to answer the nudge before the
            // conversation is failed.
            var flowSettings = await subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(currentActivity.SubjectContentType, cancellationToken);

            if (flowSettings is not null && OmnichannelAutomationHelper.HasNoResponseTimeout(flowSettings))
            {
                currentActivity.ScheduledUtc = OmnichannelAutomationHelper.ResolveNoResponseDeadline(flowSettings, now);
            }

            await omnichannelActivityStore.UpdateAsync(currentActivity, cancellationToken);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Sent SMS re-engagement {Attempt}/{Max} for Activity {ActivityId}.", currentActivity.ReEngagementAttempts, cadence.Steps.Count, activity.ItemId.SanitizeLogValue());
            }
        }
    }
}
