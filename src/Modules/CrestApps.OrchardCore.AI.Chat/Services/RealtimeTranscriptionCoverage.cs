using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Realtime;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Finds the realtime deployments that would leave the user untranscribed.
/// </summary>
/// <remarks>
/// A speech-to-speech model hears the user and never writes them down: the user's side of the transcript comes from
/// a speech-to-text model the realtime provider runs on each turn. The orchestrator takes that model from the
/// speech-to-text deployment, and only from one on the realtime deployment's own provider, because another
/// provider's model name means nothing to the session. Without one the conversation still works, but everything that
/// reads it afterwards -- stored history, grounding, summaries, an AI call's review -- sees only the assistant.
/// A cascaded deployment transcribes with its own speech-to-text leg, so it is never one of them.
/// </remarks>
internal static class RealtimeTranscriptionCoverage
{
    /// <summary>
    /// The technical names of the realtime deployments whose provider has no speech-to-text deployment.
    /// </summary>
    /// <param name="realtimeDeployments">The realtime-capable deployments.</param>
    /// <param name="resolveSpeechToTextAsync">
    /// Resolves the speech-to-text slot for one provider, the way the orchestrator does; <see langword="null"/> when
    /// there is none.
    /// </param>
    public static async Task<string[]> GetUntranscribedAsync(
        IEnumerable<AIDeployment> realtimeDeployments,
        Func<string, ValueTask<AIDeployment>> resolveSpeechToTextAsync)
    {
        ArgumentNullException.ThrowIfNull(resolveSpeechToTextAsync);

        var untranscribed = new List<string>();
        var providerTranscribes = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var deployment in realtimeDeployments ?? [])
        {
            if (deployment is null || string.IsNullOrWhiteSpace(deployment.Name))
            {
                continue;
            }

            if (deployment.TryGet<CascadedRealtimeMetadata>(out var cascade) && cascade.IsComplete())
            {
                continue;
            }

            var provider = deployment.ClientName ?? string.Empty;

            if (!providerTranscribes.TryGetValue(provider, out var transcribes))
            {
                var speechToText = await resolveSpeechToTextAsync(deployment.ClientName);

                transcribes = speechToText is not null &&
                    string.Equals(speechToText.ClientName, deployment.ClientName, StringComparison.OrdinalIgnoreCase);

                providerTranscribes[provider] = transcribes;
            }

            if (!transcribes)
            {
                untranscribed.Add(deployment.Name);
            }
        }

        return [.. untranscribed];
    }
}
