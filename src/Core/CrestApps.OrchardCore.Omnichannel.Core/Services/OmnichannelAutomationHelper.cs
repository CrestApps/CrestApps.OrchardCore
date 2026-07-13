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
        };
    }

    private static string FirstValue(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
