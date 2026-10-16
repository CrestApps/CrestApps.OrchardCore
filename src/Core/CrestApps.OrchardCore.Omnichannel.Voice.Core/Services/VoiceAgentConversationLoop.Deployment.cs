using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Deployments;
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
        var conversationDeploymentName = GetConversationDeploymentName(profile);

        var deployment = await ResolveRealtimeDeploymentAsync(
            conversationDeploymentName,
            profile.ChatDeploymentName,
            _capabilityService,
            _deploymentManager,
            cancellationToken);

        if (deployment is not null)
        {
            return deployment.Name;
        }

        // Said plainly on the record, because running turn-based is not an error and produces no other trace:
        // the difference is audible on the phone and invisible in the log.
        if (conversationDeploymentName is not null)
        {
            // Reported rather than quietly replaced, as the chat does: the profile chose this model, and holding the
            // call on another one would sound fine on a model nobody chose.
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Profile '{ProfileName}' runs the turn-based voice loop: its conversation deployment '{DeploymentName}' does not declare the '{Feature}' capability.",
                    profile.Name.SanitizeLogValue(),
                    conversationDeploymentName.SanitizeLogValue(),
                    AIDeploymentFeatureNames.Realtime);
            }
        }
        else if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Profile '{ProfileName}' runs the turn-based voice loop: no deployment declares the '{Feature}' capability.",
                profile.Name.SanitizeLogValue(),
                AIDeploymentFeatureNames.Realtime);
        }

        return null;
    }

    /// <summary>
    /// Resolves the realtime deployment a call is held on, the way the chat resolves a profile's conversation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A named conversation deployment is asked of the deployment catalog by capability, not through the chat slot.
    /// A speech-to-speech model cannot serve a text completion, so the framework excludes the realtime feature from
    /// that slot -- which means asking the chat slot to resolve the profile's deployment answers "no such
    /// deployment" for precisely the deployment being looked for. Nothing failed when it did: the call connected,
    /// the assistant spoke, and the only symptom was that it could not hear the caller while it was talking. A named
    /// deployment that cannot hold a session resolves to nothing rather than to another model.
    /// </para>
    /// <para>
    /// With no conversation deployment named, a chat deployment that can hold a session is used, for profiles saved
    /// before the two were split. Otherwise the realtime slot answers: the site's default realtime deployment, then
    /// the first realtime-capable one. Asking the capability catalog for the chat deployment alone sent every profile
    /// saved since the split -- whose chat deployment is a text model -- to the turn-based loop, although the profile
    /// asked for the site's default realtime deployment.
    /// </para>
    /// </remarks>
    /// <param name="conversationDeploymentName">The profile's conversation deployment, or <see langword="null"/>.</param>
    /// <param name="chatDeploymentName">The profile's chat deployment, or <see langword="null"/>.</param>
    /// <param name="capabilityService">Resolves a named deployment by capability.</param>
    /// <param name="deploymentManager">Resolves the realtime slot.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The deployment, or <see langword="null"/> when the call is to take turns.</returns>
    internal static async ValueTask<AIDeployment> ResolveRealtimeDeploymentAsync(
        string conversationDeploymentName,
        string chatDeploymentName,
        IAIDeploymentCapabilityService capabilityService,
        IAIDeploymentManager deploymentManager,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(conversationDeploymentName))
        {
            return await capabilityService.ResolveDeploymentWithFeatureAsync(
                AIDeploymentFeatureNames.Realtime,
                conversationDeploymentName.Trim(),
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(chatDeploymentName))
        {
            var folded = await capabilityService.ResolveDeploymentWithFeatureAsync(
                AIDeploymentFeatureNames.Realtime,
                chatDeploymentName.Trim(),
                cancellationToken);

            if (folded is not null)
            {
                return folded;
            }
        }

        return await deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Realtime, cancellationToken: cancellationToken);
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
