using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultSpeechVoicePresenter
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly ISpeechVoiceResolver _speechVoiceResolver;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger _logger;

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

    public async Task<IEnumerable<SelectListItem>> GetVoiceMenuItemsAsync(string deploymentId)
    {
        if (string.IsNullOrEmpty(deploymentId))
        {
            return [];
        }

        try
        {
            var deployment = await _deploymentManager.FindByIdAsync(deploymentId);

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
            _logger.LogWarning(ex, "Failed to fetch available TTS voices for deployment '{DeploymentId}'.", deploymentId);

            return [];
        }
    }

}
