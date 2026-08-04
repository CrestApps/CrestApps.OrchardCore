using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Resolves subject flow settings from the content-type part settings of the omnichannel subject part. The
/// stable configuration of a subject lives on its content-type definition; volatile per-run values are
/// chosen when an activity batch is loaded.
/// </summary>
public sealed class SubjectFlowSettingsService : ISubjectFlowSettingsService
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectFlowSettingsService"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public SubjectFlowSettingsService(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SubjectFlowSettings>> GetConfiguredFlowSettingsAsync(CancellationToken cancellationToken = default)
    {
        var contentTypes = await _contentDefinitionManager.ListTypeDefinitionsAsync();

        var result = new List<SubjectFlowSettings>();

        foreach (var contentType in contentTypes)
        {
            var flowSettings = BuildFlowSettings(contentType);

            if (flowSettings is not null)
            {
                result.Add(flowSettings);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<SubjectFlowSettings> FindConfiguredFlowSettingsAsync(string subjectContentType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subjectContentType))
        {
            return null;
        }

        var contentType = await _contentDefinitionManager.GetTypeDefinitionAsync(subjectContentType);

        return BuildFlowSettings(contentType);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentTypeDefinition>> GetConfiguredSubjectTypesAsync(CancellationToken cancellationToken = default)
    {
        var contentTypes = await _contentDefinitionManager.ListTypeDefinitionsAsync();

        return contentTypes
            .Where(HasOmnichannelSubjectPart)
            .OrderBy(contentType => contentType.DisplayName)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentTypeDefinition>> GetConfiguredSubjectTypesAsync(SubjectDirection direction, CancellationToken cancellationToken = default)
    {
        var contentTypes = await _contentDefinitionManager.ListTypeDefinitionsAsync();

        return contentTypes
            .Where(contentType => HasOmnichannelSubjectPart(contentType) && GetDirection(contentType) == direction)
            .OrderBy(contentType => contentType.DisplayName)
            .ToArray();
    }

    /// <inheritdoc />
    public bool IsConfigured(SubjectFlowSettings flowSettings)
    {
        return !string.IsNullOrWhiteSpace(flowSettings?.SubjectContentType);
    }

    private static bool HasOmnichannelSubjectPart(ContentTypeDefinition contentTypeDefinition)
    {
        return contentTypeDefinition?.Parts.Any(part =>
            part.Name == OmnichannelConstants.ContentParts.OmnichannelSubject) == true;
    }

    private static SubjectDirection GetDirection(ContentTypeDefinition contentTypeDefinition)
    {
        var partDefinition = contentTypeDefinition.Parts
            .FirstOrDefault(part => part.Name == OmnichannelConstants.ContentParts.OmnichannelSubject);

        return partDefinition is null
            ? SubjectDirection.Outbound
            : partDefinition.GetSettings<OmnichannelSubjectPartSettings>().Direction;
    }

    private static SubjectFlowSettings BuildFlowSettings(ContentTypeDefinition contentTypeDefinition)
    {
        if (contentTypeDefinition is null)
        {
            return null;
        }

        var partDefinition = contentTypeDefinition.Parts
            .FirstOrDefault(part => part.Name == OmnichannelConstants.ContentParts.OmnichannelSubject);

        if (partDefinition is null)
        {
            return null;
        }

        var baseSettings = partDefinition.GetSettings<OmnichannelSubjectPartSettings>();
        var aiSettings = partDefinition.GetSettings<OmnichannelSubjectAISettings>();

        return new SubjectFlowSettings
        {
            SubjectContentType = contentTypeDefinition.Name,
            Direction = baseSettings.Direction,
            InteractionType = baseSettings.InteractionType,
            Channel = baseSettings.Channel,
            ChannelEndpointId = baseSettings.ChannelEndpointId,
            CampaignId = baseSettings.DefaultCampaignId,
            RequireDisposition = baseSettings.RequireDisposition,
            ProfileId = aiSettings.ProfileId,
            SubjectGoal = aiSettings.SubjectGoal,
            InitialOutboundPromptPattern = aiSettings.InitialOutboundPromptPattern,
            SpeechToTextDeploymentName = aiSettings.SpeechToTextDeploymentName,
            TextToSpeechDeploymentName = aiSettings.TextToSpeechDeploymentName,
            TextToSpeechVoiceId = aiSettings.TextToSpeechVoiceId,
            AllowAIToUpdateContact = aiSettings.AllowAIToUpdateContact,
            AllowAIToUpdateSubject = aiSettings.AllowAIToUpdateSubject,
            NoResponseTimeoutInMinutes = aiSettings.NoResponseTimeoutInMinutes,
            SmsResponseDelayInSeconds = aiSettings.SmsResponseDelayInSeconds,
            SmsOptOutKeywords = aiSettings.SmsOptOutKeywords,
        };
    }
}
