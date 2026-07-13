using CrestApps.Core.AI.Models;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="AutomatedVoiceActivitySettingsResolver"/> class.
    /// </summary>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="siteService">The site service used to read default AI deployment settings.</param>
    public AutomatedVoiceActivitySettingsResolver(
        ISubjectFlowSettingsService subjectFlowSettingsService,
        ISiteService siteService)
    {
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _siteService = siteService;
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

        return new AutomatedVoiceActivitySettings
        {
            AIProfileId = FirstValue(activity.AIProfileId, flowSettings?.ProfileId),
            SpeechToTextDeploymentName = FirstValue(
                activity.SpeechToTextDeploymentName,
                flowSettings?.SpeechToTextDeploymentName,
                siteSettings.DefaultSpeechToTextDeploymentName),
            TextToSpeechDeploymentName = FirstValue(
                activity.TextToSpeechDeploymentName,
                flowSettings?.TextToSpeechDeploymentName,
                siteSettings.DefaultTextToSpeechDeploymentName),
            TextToSpeechVoiceId = FirstValue(
                activity.TextToSpeechVoiceId,
                flowSettings?.TextToSpeechVoiceId,
                siteSettings.DefaultTextToSpeechVoiceId),
        };
    }

    private static string FirstValue(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
