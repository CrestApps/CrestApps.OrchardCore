using System.Globalization;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.AI.Speech;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Retrieves available text-to-speech voices for a given deployment and presents them
/// as grouped <see cref="SelectListItem"/> entries filtered by supported cultures.
/// </summary>
public sealed class DefaultSpeechVoicePresenter
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly ISpeechVoiceResolver _speechVoiceResolver;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSpeechVoicePresenter"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager for resolving TTS deployments.</param>
    /// <param name="speechVoiceResolver">The resolver for fetching available speech voices.</param>
    /// <param name="localizationService">The localization service for determining supported cultures.</param>
    /// <param name="logger">The logger instance.</param>
    public DefaultSpeechVoicePresenter(
        IAIDeploymentManager deploymentManager,
        ISpeechVoiceResolver speechVoiceResolver,
        ILocalizationService localizationService,
        ILogger<DefaultSpeechVoicePresenter> logger)
    {
        _deploymentManager = deploymentManager;
        _speechVoiceResolver = speechVoiceResolver;
        _localizationService = localizationService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves available TTS voices for the specified deployment and returns them as
    /// culture-grouped <see cref="SelectListItem"/> entries.
    /// </summary>
    /// <param name="deploymentName">The deployment name to resolve, or <c>null</c> to use the default TTS deployment.</param>
    public async Task<IEnumerable<SelectListItem>> GetVoiceMenuItemsAsync(string deploymentName)
    {
        try
        {
            var deployment = !string.IsNullOrEmpty(deploymentName)
            ? await _deploymentManager.FindByNameAsync(deploymentName)
            : await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.TextToSpeech);

            if (deployment == null)
            {
                return [];
            }

            var speechVoices = await _speechVoiceResolver.GetSpeechVoicesAsync(deployment);

            var supportedCultures = await _localizationService.GetSupportedCulturesAsync();
            var supportedSet = SpeechVoiceLocalizationHelper.CreateAllowedCultures(
                supportedCultures,
                CultureInfo.CurrentCulture,
                CultureInfo.CurrentUICulture);

            var voices = new List<SelectListItem>();

            foreach (var voiceGroup in speechVoices
                .Where(v => SpeechVoiceLocalizationHelper.IsLanguageAllowed(v.Language, supportedSet))
                .OrderBy(v => v.Language)
                .ThenBy(v => v.Name)
                .GroupBy(v => SpeechVoiceLocalizationHelper.GetCultureDisplayName(v.Language) ?? "Unknown"))
            {
                var group = new SelectListGroup { Name = voiceGroup.Key };

                foreach (var voice in voiceGroup)
                {
                    voices.Add(new SelectListItem(voice.Name, voice.Id) { Group = group });
                }
            }

            return voices;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch available TTS voices for deployment '{DeploymentName}'.", deploymentName ?? "(resolved)");

            return [];
        }
    }
}
