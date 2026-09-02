using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Provides shared helpers for automated omnichannel activity lifecycle decisions.
/// </summary>
public static class OmnichannelAutomationHelper
{
    /// <summary>
    /// Gets the initial activity status for a loaded activity.
    /// </summary>
    /// <param name="interactionType">The interaction type.</param>
    /// <param name="hasAssignedUser">Whether the loaded activity has an assigned user.</param>
    public static ActivityStatus GetInitialActivityStatus(
        ActivityInteractionType interactionType,
        bool hasAssignedUser)
    {
        if (interactionType == ActivityInteractionType.Automated)
        {
            return ActivityStatus.NotStated;
        }

        return hasAssignedUser
            ? ActivityStatus.NotStated
            : ActivityStatus.Scheduled;
    }

    /// <summary>
    /// Determines whether the flow settings define an automated no-response timeout.
    /// </summary>
    /// <param name="flowSettings">The subject flow settings.</param>
    public static bool HasNoResponseTimeout(SubjectFlowSettings flowSettings)
        => flowSettings?.NoResponseTimeoutInMinutes is > 0;

    /// <summary>
    /// Resolves the UTC deadline for a no-response timeout.
    /// </summary>
    /// <param name="flowSettings">The subject flow settings.</param>
    /// <param name="utcNow">The current UTC time.</param>
    public static DateTime ResolveNoResponseDeadline(
        SubjectFlowSettings flowSettings,
        DateTime utcNow)
    {
        if (!HasNoResponseTimeout(flowSettings))
        {
            return utcNow;
        }

        return utcNow.AddMinutes(flowSettings.NoResponseTimeoutInMinutes.Value);
    }

    /// <summary>
    /// Resolves the activity-load AI and speech settings from batch overrides and subject-flow defaults.
    /// </summary>
    /// <param name="batch">The activity batch.</param>
    /// <param name="flowSettings">The subject flow settings.</param>
    /// <returns>The settings to persist on each automated activity.</returns>
    public static AutomatedVoiceActivitySettings ResolveActivitySettings(
        OmnichannelActivityBatch batch,
        SubjectFlowSettings flowSettings)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return new AutomatedVoiceActivitySettings
        {
            AIProfileId = FirstValue(batch.AIProfileId, flowSettings?.ProfileId),
            SpeechToTextDeploymentName = FirstValue(
                batch.SpeechToTextDeploymentName,
                flowSettings?.SpeechToTextDeploymentName),
            TextToSpeechDeploymentName = FirstValue(
                batch.TextToSpeechDeploymentName,
                flowSettings?.TextToSpeechDeploymentName),
            TextToSpeechVoiceId = FirstValue(
                batch.TextToSpeechVoiceId,
                flowSettings?.TextToSpeechVoiceId),

            // The batch is the source of truth for the AI field-update guards: the subject AI-settings UI is
            // only shown for inbound subjects, so an outbound automated inventory can only configure these when
            // it is loaded. They are chosen on the batch and snapshotted onto each loaded activity.
            AllowAIToUpdateContact = batch.AllowAIToUpdateContact,
            AllowAIToUpdateSubject = batch.AllowAIToUpdateSubject,

            // The reply delay is likewise chosen on the batch when the inventory is loaded, and snapshotted onto
            // each activity so the conversation paces itself the same way for its whole lifetime.
            ResponseDelayMode = batch.ResponseDelayMode,
            ResponseDelaySeconds = batch.ResponseDelaySeconds,
            ResponseDelayJitterSeconds = batch.ResponseDelayJitterSeconds,

            // The re-engagement (nudge) policy and the business-hours calendar that gates background-initiated sends
            // are chosen on the batch at load time and snapshotted so the whole conversation follows one policy.
            BusinessHoursCalendarId = batch.BusinessHoursCalendarId,
            CadenceId = batch.CadenceId,
        };
    }

    /// <summary>
    /// Computes the delay to wait before sending the next automated reply, from the snapshot on the activity.
    /// Returns <see langword="null"/> when no delay should be applied.
    /// </summary>
    /// <param name="mode">The configured delay mode.</param>
    /// <param name="seconds">The fixed delay, or the base for random.</param>
    /// <param name="jitterSeconds">The +/- jitter for random.</param>
    public static TimeSpan? ResolveResponseDelay(OmnichannelResponseDelayMode mode, int seconds, int jitterSeconds)
    {
        switch (mode)
        {
            case OmnichannelResponseDelayMode.Fixed:
                return seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;

            case OmnichannelResponseDelayMode.Random:
                var min = Math.Max(0, seconds - Math.Abs(jitterSeconds));
                var max = Math.Max(min, seconds + Math.Abs(jitterSeconds));
                var value = min >= max ? min : Random.Shared.Next(min, max + 1);

                return value > 0 ? TimeSpan.FromSeconds(value) : null;

            default:
                return null;
        }
    }

    /// <summary>
    /// The minimum pause, in seconds, before an automated conversation replies. Even when no delay is configured
    /// a reply never fires instantly, so the exchange feels like a person and not a bot.
    /// </summary>
    public const int MinimumHumanizedReplyDelaySeconds = 3;

    /// <summary>
    /// The upper bound, in seconds, on the humanized "reading" pause a reply waits before it is composed. It keeps
    /// a turn well under the per-conversation lock lease even when the customer sends a very long message.
    /// </summary>
    private const int MaximumHumanizedReadingDelaySeconds = 15;

    /// <summary>
    /// The upper bound, in seconds, on the humanized "typing" pause a reply waits after it is composed.
    /// </summary>
    private const int MaximumHumanizedTypingDelaySeconds = 8;

    // Approximate human reading and texting speeds. Reading is faster than composing, so a long customer message
    // adds less delay than composing a long reply of the same size.
    private const double ReadingCharactersPerSecond = 20d;
    private const double TypingCharactersPerSecond = 12d;

    /// <summary>
    /// Computes the humanized pause to wait before <em>composing</em> an automated reply: at least
    /// <see cref="MinimumHumanizedReplyDelaySeconds"/> (or the configured delay when it is longer), plus reading
    /// time proportional to how much the customer just wrote, capped so the turn stays bounded. This doubles as the
    /// settle window that lets several quick inbound texts be answered together.
    /// </summary>
    /// <param name="configuredDelay">The delay configured on the activity/subject flow, used as the floor when longer than the minimum.</param>
    /// <param name="inboundCharacterCount">The total number of characters in the customer messages being answered.</param>
    public static TimeSpan ResolveHumanizedReadingDelay(TimeSpan? configuredDelay, int inboundCharacterCount)
    {
        var floor = TimeSpan.FromSeconds(MinimumHumanizedReplyDelaySeconds);

        if (configuredDelay is { } configured && configured > floor)
        {
            floor = configured;
        }

        var reading = TimeSpan.FromSeconds(Math.Max(0, inboundCharacterCount) / ReadingCharactersPerSecond);
        var total = floor + reading;
        var max = TimeSpan.FromSeconds(MaximumHumanizedReadingDelaySeconds);

        // The reading time is capped on its own so a very long inbound message cannot push a single turn past the
        // lock lease, but the configured floor is always honored even when it exceeds that reading cap.
        var cappedTotal = total > floor + max ? floor + max : total;

        return cappedTotal;
    }

    /// <summary>
    /// Computes the humanized pause to wait after <em>composing</em> a reply and before sending it, proportional to
    /// the length of the reply, so a long reply appears to take longer to type. Capped so the turn stays bounded.
    /// </summary>
    /// <param name="replyCharacterCount">The number of characters in the composed reply.</param>
    public static TimeSpan ResolveHumanizedTypingDelay(int replyCharacterCount)
    {
        var typing = TimeSpan.FromSeconds(Math.Max(0, replyCharacterCount) / TypingCharactersPerSecond);
        var max = TimeSpan.FromSeconds(MaximumHumanizedTypingDelaySeconds);

        return typing > max ? max : typing;
    }

    private static string FirstValue(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
