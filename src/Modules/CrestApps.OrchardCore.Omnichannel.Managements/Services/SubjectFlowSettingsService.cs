using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Resolves subject flow settings that are complete enough to create or load activities.
/// </summary>
internal sealed class SubjectFlowSettingsService : ISubjectFlowSettingsService
{
    private readonly ICatalog<SubjectFlowSettings> _flowSettingsCatalog;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectFlowSettingsService"/> class.
    /// </summary>
    /// <param name="flowSettingsCatalog">The subject flow settings catalog.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public SubjectFlowSettingsService(
        ICatalog<SubjectFlowSettings> flowSettingsCatalog,
        IContentDefinitionManager contentDefinitionManager)
    {
        _flowSettingsCatalog = flowSettingsCatalog;
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SubjectFlowSettings>> GetConfiguredFlowSettingsAsync(CancellationToken cancellationToken = default)
    {
        var flowSettings = await _flowSettingsCatalog.GetAllAsync(cancellationToken);

        return flowSettings
            .Where(IsConfigured)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<SubjectFlowSettings> FindConfiguredFlowSettingsAsync(string subjectContentType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subjectContentType))
        {
            return null;
        }

        var configuredFlowSettings = await GetConfiguredFlowSettingsAsync(cancellationToken);

        return configuredFlowSettings.FirstOrDefault(flowSettings =>
            string.Equals(flowSettings.SubjectContentType, subjectContentType, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentTypeDefinition>> GetConfiguredSubjectTypesAsync(CancellationToken cancellationToken = default)
    {
        var configuredFlowSettings = await GetConfiguredFlowSettingsAsync(cancellationToken);
        var configuredSubjectNames = configuredFlowSettings
            .Select(flowSettings => flowSettings.SubjectContentType)
            .Where(subjectContentType => !string.IsNullOrWhiteSpace(subjectContentType))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var contentTypes = await _contentDefinitionManager.ListTypeDefinitionsAsync();

        return contentTypes
            .Where(contentType =>
                contentType.StereotypeEquals(OmnichannelConstants.Sterotypes.OmnichannelSubject) &&
                configuredSubjectNames.Contains(contentType.Name))
            .OrderBy(contentType => contentType.DisplayName)
            .ToArray();
    }

    /// <inheritdoc />
    public bool IsConfigured(SubjectFlowSettings flowSettings)
    {
        if (flowSettings is null ||
            string.IsNullOrWhiteSpace(flowSettings.SubjectContentType) ||
            string.IsNullOrWhiteSpace(flowSettings.CampaignId) ||
            string.IsNullOrWhiteSpace(flowSettings.Channel))
        {
            return false;
        }

        return flowSettings.InteractionType != ActivityInteractionType.Automated ||
            (!string.IsNullOrWhiteSpace(flowSettings.ChannelEndpointId) &&
                !string.IsNullOrWhiteSpace(flowSettings.ProfileId));
    }
}
