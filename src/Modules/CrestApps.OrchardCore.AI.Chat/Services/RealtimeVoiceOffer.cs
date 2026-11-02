using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Realtime;
using CrestApps.Core.AI.Speech;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Checks a voice against the voices of the speech-to-speech deployment a profile's conversations run on.
/// </summary>
/// <remarks>
/// A speech-to-speech provider gets the voice in the same set-up message as the profile's instructions and tools, and
/// refuses the whole message over a voice it does not have. Live, a profile moved from a cascaded deployment to OpenAI's
/// realtime model kept its ElevenLabs voice, and its calls ran with neither instructions nor tools. The editor lists only
/// the deployment's own voices, but a voice it no longer lists can still be posted: by a page loaded before the
/// deployment changed, by a voice list that arrived after a newer one, or by a script. A cascaded deployment speaks
/// through its text-to-speech deployment, and a provider that publishes no voices has nothing to check against, so
/// neither is checked.
/// </remarks>
internal static class RealtimeVoiceOffer
{
    /// <summary>
    /// The speech-to-speech deployment the conversation runs on, when it does not offer <paramref name="voiceName"/>;
    /// otherwise <see langword="null"/>.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="voiceResolver">The realtime voice resolver.</param>
    /// <param name="conversationDeploymentName">The conversation deployment chosen, or empty for the site's default.</param>
    /// <param name="voiceName">The voice chosen, or empty for the default voice.</param>
    public static async Task<AIDeployment> FindDeploymentWithoutVoiceAsync(
        IAIDeploymentManager deploymentManager,
        IRealtimeVoiceResolver voiceResolver,
        string conversationDeploymentName,
        string voiceName)
    {
        if (string.IsNullOrWhiteSpace(voiceName))
        {
            return null;
        }

        // The chain a call resolves too: the deployment chosen, then the site's default, then the first
        // realtime-capable deployment.
        var deployment = await deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Realtime, conversationDeploymentName);

        if (deployment is null || (deployment.TryGet<CascadedRealtimeMetadata>(out var cascade) && cascade.IsComplete()))
        {
            return null;
        }

        SpeechVoice[] voices;

        try
        {
            voices = await voiceResolver.GetVoicesAsync(deployment);
        }
        catch (Exception)
        {
            // A list that cannot be read is no reason to refuse the save.
            return null;
        }

        return voices is null || voices.Length == 0 || voices.Any(voice => string.Equals(voice.Id, voiceName, StringComparison.Ordinal))
            ? null
            : deployment;
    }
}
