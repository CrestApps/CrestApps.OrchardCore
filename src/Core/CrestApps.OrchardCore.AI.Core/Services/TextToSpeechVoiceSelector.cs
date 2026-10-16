using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Speech;
using CrestApps.Core.Support;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Picks the voice a text-to-speech deployment is asked to speak with, keeping only voices that deployment offers.
/// </summary>
/// <remarks>
/// A chat profile has one voice, and when a realtime deployment carries its conversation the profile editor lists
/// that model's voices. Read-aloud speaks through the site's text-to-speech deployment instead, which may not have
/// that voice at all: Azure AI Speech knows <c>en-US-JennyNeural</c>, not the realtime <c>marin</c>. Sending it
/// anyway fails the synthesis. Each candidate is therefore checked against the deployment's voice list, and the
/// first one it offers wins. A provider that publishes no voice list cannot be checked, so its first candidate is
/// trusted as before.
/// </remarks>
public sealed class TextToSpeechVoiceSelector
{
    private static readonly TimeSpan _voiceListLifetime = TimeSpan.FromMinutes(15);

    private readonly ISpeechVoiceResolver _speechVoiceResolver;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextToSpeechVoiceSelector"/> class.
    /// </summary>
    /// <param name="speechVoiceResolver">The resolver for a deployment's text-to-speech voices.</param>
    /// <param name="memoryCache">The cache holding each deployment's voice list.</param>
    /// <param name="logger">The logger instance.</param>
    public TextToSpeechVoiceSelector(
        ISpeechVoiceResolver speechVoiceResolver,
        IMemoryCache memoryCache,
        ILogger<TextToSpeechVoiceSelector> logger)
    {
        _speechVoiceResolver = speechVoiceResolver;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// Returns the first candidate voice the deployment offers.
    /// </summary>
    /// <param name="deployment">The text-to-speech deployment that will speak.</param>
    /// <param name="candidates">The voices to try, most preferred first. Blank entries are skipped.</param>
    /// <returns>
    /// The chosen voice id, or <see langword="null"/> when no candidate is offered, so the provider speaks with its
    /// own default voice.
    /// </returns>
    public async Task<string> SelectVoiceAsync(AIDeployment deployment, params string[] candidates)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var requested = candidates?
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => candidate.Trim())
            .ToArray() ?? [];

        if (requested.Length == 0)
        {
            return null;
        }

        var offered = await GetOfferedVoiceIdsAsync(deployment);

        if (offered is null || offered.Count == 0)
        {
            return requested[0];
        }

        foreach (var voice in requested)
        {
            if (offered.Contains(voice))
            {
                return voice;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "The text-to-speech deployment '{DeploymentName}' does not offer the voice '{VoiceId}', so it is not used. A realtime model's voice is a common cause.",
                    deployment.Name.SanitizeLogValue(),
                    voice.SanitizeLogValue());
            }
        }

        return null;
    }

    private async Task<HashSet<string>> GetOfferedVoiceIdsAsync(AIDeployment deployment)
    {
        var cacheKey = $"{nameof(TextToSpeechVoiceSelector)}:{deployment.ItemId}:{deployment.ClientName}:{deployment.ConnectionName}:{deployment.ModelName}";

        if (_memoryCache.TryGetValue(cacheKey, out HashSet<string> cached))
        {
            return cached;
        }

        SpeechVoice[] voices;

        try
        {
            voices = await _speechVoiceResolver.GetSpeechVoicesAsync(deployment);
        }
        catch (Exception ex)
        {
            // Not cached: the next request asks again. Until then the voice cannot be checked, which is how
            // read-aloud behaved before the check existed.
            _logger.LogWarning(ex, "Failed to fetch the voices of the text-to-speech deployment '{DeploymentName}'.", deployment.Name.SanitizeLogValue());

            return null;
        }

        var offered = (voices ?? [])
            .Where(voice => !string.IsNullOrWhiteSpace(voice?.Id))
            .Select(voice => voice.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _memoryCache.Set(cacheKey, offered, _voiceListLifetime);

        return offered;
    }
}
