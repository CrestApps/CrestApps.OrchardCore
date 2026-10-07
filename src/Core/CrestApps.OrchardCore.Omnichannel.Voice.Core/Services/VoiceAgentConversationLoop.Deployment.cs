using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

public sealed partial class VoiceAgentConversationLoop
{
    /// <summary>
    /// The deployment a live session would be held on, or <see langword="null"/> when this profile cannot hold one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A profile that speaks names the deployment that carries its voice as its conversation deployment, kept apart
    /// from its chat deployment, which is the text model. That is the deployment asked first. Reading only the chat
    /// deployment ignored the choice: a profile switched to a newer realtime model kept every call on the first
    /// realtime deployment the tenant had, and nothing said so.
    /// </para>
    /// <para>
    /// A profile with no conversation deployment falls back to its chat deployment, for profiles saved before the
    /// two were split, and then to whatever deployment the tenant has with the capability, which is how the rest of
    /// the platform resolves it.
    /// </para>
    /// </remarks>
    /// <param name="profile">The profile driving the conversation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private async Task<string> ResolveRealtimeDeploymentNameAsync(AIProfile profile, CancellationToken cancellationToken)
    {
        // Asked of the deployment catalog by capability, not through the chat slot. A speech-to-speech model
        // cannot serve a text completion, so the framework excludes the realtime feature from that slot -- which
        // means asking the chat slot to resolve the profile's deployment answers "no such deployment" for
        // precisely the deployment being looked for. Nothing failed when it did: the call connected, the
        // assistant spoke, and the only symptom was that it could not hear the caller while it was talking.
        //
        // With no deployment named, this falls back to whatever deployment the tenant has with the capability,
        // which is how the rest of the platform resolves it.
        var requestedDeploymentName = GetConversationDeploymentName(profile) ?? profile.ChatDeploymentName;

        var deployment = await _capabilityService.ResolveDeploymentWithFeatureAsync(
            AIDeploymentFeatureNames.Realtime,
            requestedDeploymentName,
            cancellationToken);

        if (deployment is not null)
        {
            // A named deployment that could not hold the call was replaced by the tenant's; worth knowing, because
            // the call still connects and sounds fine on a model nobody chose.
            if (!string.IsNullOrWhiteSpace(requestedDeploymentName) &&
                !string.Equals(deployment.Name, requestedDeploymentName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Profile '{ProfileName}' names deployment '{RequestedDeploymentName}' for its calls, which does not declare the '{Feature}' capability; deployment '{DeploymentName}' holds the call instead.",
                    profile.Name.SanitizeLogValue(),
                    requestedDeploymentName.SanitizeLogValue(),
                    AIDeploymentFeatureNames.Realtime,
                    deployment.Name.SanitizeLogValue());
            }

            return deployment.Name;
        }

        // Said plainly on the record, because running turn-based is not an error and produces no other trace:
        // the difference is audible on the phone and invisible in the log.
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Profile '{ProfileName}' runs the turn-based voice loop: deployment '{DeploymentName}' does not declare the '{Feature}' capability.",
                profile.Name.SanitizeLogValue(),
                requestedDeploymentName.SanitizeLogValue(),
                AIDeploymentFeatureNames.Realtime);
        }

        return null;
    }

    /// <summary>
    /// The deployment a profile names to carry its spoken conversations, or <see langword="null"/> when it names none.
    /// </summary>
    /// <param name="profile">The profile.</param>
    internal static string GetConversationDeploymentName(AIProfile profile)
        => profile is not null &&
            profile.TryGetSettings<ChatModeProfileSettings>(out var settings) &&
            !string.IsNullOrWhiteSpace(settings.ConversationDeploymentName)
                ? settings.ConversationDeploymentName.Trim()
                : null;
}
