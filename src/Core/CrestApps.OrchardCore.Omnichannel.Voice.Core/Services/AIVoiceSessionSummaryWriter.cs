using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Turns what the conversation loop knew about a finished call into the one <see cref="AIVoiceSessionSummary"/>
/// the usage report reads, in whatever scope it is resolved in.
/// </summary>
/// <remarks>
/// Everything durable is read again here rather than carried across: by the time a summary is written the request
/// the call ran in may be long gone, and the activity may have moved on.
/// </remarks>
internal sealed class AIVoiceSessionSummaryWriter
{
    private readonly IOmnichannelActivityStore _activityStore;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAIProfileManager _profileManager;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly ICatalog<OmnichannelCampaign> _campaigns;
    private readonly IAIVoiceSessionSummaryStore _summaries;
    private readonly IOptionsMonitor<GeneralAIOptions> _generalAIOptions;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceSessionSummaryWriter"/> class.
    /// </summary>
    public AIVoiceSessionSummaryWriter(
        IOmnichannelActivityStore activityStore,
        IAIChatSessionPromptStore promptStore,
        IAIProfileManager profileManager,
        IAIDeploymentManager deploymentManager,
        ICatalog<OmnichannelCampaign> campaigns,
        IAIVoiceSessionSummaryStore summaries,
        IOptionsMonitor<GeneralAIOptions> generalAIOptions,
        IClock clock,
        ILogger<AIVoiceSessionSummaryWriter> logger)
    {
        _activityStore = activityStore;
        _promptStore = promptStore;
        _profileManager = profileManager;
        _deploymentManager = deploymentManager;
        _campaigns = campaigns;
        _summaries = summaries;
        _generalAIOptions = generalAIOptions;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Writes the call's summary, unless the tenant does not track usage or the call already has one.
    /// </summary>
    /// <param name="draft">What the loop knew about the call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The summary written, or <see langword="null"/> when none was.</returns>
    public async Task<AIVoiceSessionSummary> WriteAsync(AIVoiceSessionDraft draft, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(draft?.ActivityId) || !_generalAIOptions.CurrentValue.EnableAIUsageTracking)
        {
            return null;
        }

        // Once per call. A live call is summarized when its session ends and asked about again by the provider's
        // hangup, which knows nothing of the audio. The two race: when the hangup's unmeasured summary landed
        // first, the measured one takes its place; otherwise the first summary stands.
        var existing = await _summaries.FindByActivityAsync(draft.ActivityId, cancellationToken);

        if (existing is not null)
        {
            if (draft.Measurements is null || existing.SessionDurationMs.HasValue)
            {
                return null;
            }

            await _summaries.DeleteAsync(existing, cancellationToken);
        }

        var activity = await _activityStore.FindByIdAsync(draft.ActivityId, cancellationToken);

        if (activity is null)
        {
            return null;
        }

        var prompts = string.IsNullOrEmpty(activity.AISessionId)
            ? []
            : (await _promptStore.GetPromptsAsync(activity.AISessionId))?.ToList() ?? [];

        var profile = string.IsNullOrEmpty(activity.AIProfileId)
            ? null
            : await _profileManager.FindByIdAsync(activity.AIProfileId, cancellationToken);

        var deployment = await ResolveDeploymentAsync(draft.DeploymentName, profile, cancellationToken);

        var campaign = string.IsNullOrEmpty(activity.CampaignId)
            ? null
            : await _campaigns.FindByIdAsync(activity.CampaignId, cancellationToken);

        var measured = draft.Measurements;
        var isRealtime = draft.Engine == AIVoiceSessionEngine.Realtime;
        var spoken = prompts.Where(prompt => !prompt.IsGeneratedPrompt && !string.IsNullOrWhiteSpace(prompt.Content)).ToList();

        var summary = new AIVoiceSessionSummary
        {
            ItemId = UniqueId.GenerateId(),
            ActivityId = activity.ItemId,
            AISessionId = activity.AISessionId,
            AIProfileId = activity.AIProfileId,
            AIProfileName = profile is null ? null : (string.IsNullOrWhiteSpace(profile.DisplayText) ? profile.Name : profile.DisplayText),
            CampaignId = activity.CampaignId,
            CampaignName = campaign?.DisplayText,
            Channel = activity.Channel,
            ChannelEndpointId = activity.ChannelEndpointId,
            ProviderName = draft.ProviderName,
            ProviderCallId = draft.ProviderCallId,
            Engine = draft.Engine,
            DeploymentName = deployment?.Name ?? draft.DeploymentName,
            ModelName = deployment?.ModelName,
            ConnectionName = deployment?.ConnectionName,
            Outcome = draft.Outcome ?? AIVoiceSessionOutcomes.ForTurnBased(activity, prompts, draft.WasAnswered),
            StartedUtc = measured?.StartedUtc,
            EndedUtc = measured?.EndedUtc ?? draft.EndedUtc,
            CreatedUtc = _clock.UtcNow,
            SessionDurationMs = measured?.SessionDurationMs,
            AssistantSpeakingMs = measured?.AssistantSpeakingMs,
            CallerSpeakingMs = measured?.CallerSpeakingMs,
            MutualSilenceMs = measured?.MutualSilenceMs,
            TimeToFirstAssistantAudioMs = measured?.TimeToFirstAssistantAudioMs,
            AssistantTurns = spoken.Count(prompt => prompt.Role == ChatRole.Assistant),
            CallerTurns = spoken.Count(prompt => prompt.Role == ChatRole.User),

            // A turn-based call stops listening while it speaks, so nobody can talk over it: barge-ins do not
            // apply to it, rather than never happening.
            BargeIns = isRealtime ? measured?.BargeIns ?? 0 : null,

            // The live session counts its own prompts; a turn-based call's are the lines it said for them.
            IdlePrompts = isRealtime
                ? measured?.IdlePrompts ?? 0
                : spoken.Count(prompt => prompt.Role == ChatRole.Assistant && string.Equals(prompt.Content.Trim(), VoiceAgentConversationLoop.StillThereLine, StringComparison.Ordinal)),
            EchoHeldMs = measured?.EchoHeldMs,
        };

        await _summaries.SaveAsync(summary, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            // The outcome is left to the stored summary: it can name a voicemail, which is not written to the log.
            _logger.LogDebug(
                "Recorded the {Engine} AI voice session of activity '{ActivityId}' ({DurationMs} ms).",
                summary.Engine,
                summary.ActivityId.SanitizeLogValue(),
                summary.SessionDurationMs);
        }

        return summary;
    }

    private async Task<AIDeployment> ResolveDeploymentAsync(string deploymentName, AIProfile profile, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(deploymentName))
        {
            return await _deploymentManager.FindByNameAsync(deploymentName, cancellationToken);
        }

        if (profile is null)
        {
            return null;
        }

        // The deployment a turn-based reply is completed on: the profile's chat slot, as the loop resolves it.
        return await _deploymentManager.ResolveSlotAsync(
            AIDeploymentSlotNames.Chat,
            deploymentName: profile.ChatDeploymentName,
            cancellationToken: cancellationToken);
    }
}
