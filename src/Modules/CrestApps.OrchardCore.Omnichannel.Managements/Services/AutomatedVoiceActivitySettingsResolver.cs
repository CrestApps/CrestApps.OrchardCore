using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Resolves activity, subject-flow, and site-level defaults for automated phone conversations.
/// </summary>
public sealed class AutomatedVoiceActivitySettingsResolver : IAutomatedVoiceActivitySettingsResolver
{
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly ISiteService _siteService;
    private readonly IAIProfileManager _profileManager;
    private readonly IAIPersonaResolver _personaResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutomatedVoiceActivitySettingsResolver"/> class.
    /// </summary>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="siteService">The site service used to read default AI deployment settings.</param>
    /// <param name="profileManager">The manager used to load the AI profile the call speaks for.</param>
    /// <param name="personaResolvers">
    /// The AI persona resolver, registered only while the AI Personas feature is enabled; without it there is no
    /// persona.
    /// </param>
    public AutomatedVoiceActivitySettingsResolver(
        ISubjectFlowSettingsService subjectFlowSettingsService,
        ISiteService siteService,
        IAIProfileManager profileManager,
        IEnumerable<IAIPersonaResolver> personaResolvers)
    {
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _siteService = siteService;
        _profileManager = profileManager;
        _personaResolver = personaResolvers.LastOrDefault();
    }

    /// <inheritdoc/>
    public async Task<AutomatedVoiceActivitySettings> ResolveAsync(
        OmnichannelActivity activity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var flowSettings = string.IsNullOrWhiteSpace(activity.SubjectContentType)
            ? null
            : await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(
                activity.SubjectContentType,
                cancellationToken);
        var siteSettings = await _siteService.GetSettingsAsync<DefaultAIDeploymentSettings>();
        var profileId = FirstValue(activity.AIProfileId, flowSettings?.ProfileId);
        var textToSpeechDeploymentName = FirstValue(
            activity.TextToSpeechDeploymentName,
            flowSettings?.TextToSpeechDeploymentName,
            siteSettings.DefaultTextToSpeechDeploymentName);

        return new AutomatedVoiceActivitySettings
        {
            AIProfileId = profileId,
            SpeechToTextDeploymentName = FirstValue(
                activity.SpeechToTextDeploymentName,
                flowSettings?.SpeechToTextDeploymentName,
                siteSettings.DefaultSpeechToTextDeploymentName),
            TextToSpeechDeploymentName = textToSpeechDeploymentName,
            TextToSpeechVoiceId = await SelectVoiceAsync(
                profileId,
                FirstValue(activity.TextToSpeechVoiceId, flowSettings?.TextToSpeechVoiceId),
                textToSpeechDeploymentName,
                siteSettings.DefaultTextToSpeechVoiceId,
                cancellationToken),
        };
    }

    private async Task<string> SelectVoiceAsync(
        string profileId,
        string explicitVoice,
        string textToSpeechDeploymentName,
        string defaultVoice,
        CancellationToken cancellationToken)
    {
        // A voice picked on the activity or the subject flow is a deliberate choice, so the profile's persona is not
        // even looked up.
        if (!string.IsNullOrWhiteSpace(explicitVoice))
        {
            return explicitVoice;
        }

        // Without the AI Personas feature there is no persona and the site's default voice is used.
        if (_personaResolver is null || string.IsNullOrWhiteSpace(profileId))
        {
            return FirstValue(defaultVoice);
        }

        var profile = await _profileManager.FindByIdAsync(profileId, cancellationToken);
        var persona = profile is null
            ? null
            : await _personaResolver.ResolveAsync(profile, cancellationToken);

        // The persona's voice is used only when it was chosen on a deployment of the provider that speaks.
        var voice = await _personaResolver.SelectVoiceAsync(
            persona,
            explicitVoice: null,
            textToSpeechDeploymentName,
            defaultVoice,
            cancellationToken);

        return FirstValue(voice);
    }

    private static string FirstValue(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
