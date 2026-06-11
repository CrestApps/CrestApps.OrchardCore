using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Provides helpers for resolving subject flow configuration state.
/// </summary>
public interface ISubjectFlowSettingsService
{
    /// <summary>
    /// Gets all subject flow settings that are fully configured and usable for activity creation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<SubjectFlowSettings>> GetConfiguredFlowSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the configured flow settings for the specified subject content type.
    /// </summary>
    /// <param name="subjectContentType">The subject content type name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<SubjectFlowSettings> FindConfiguredFlowSettingsAsync(string subjectContentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the configured subject content types.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<ContentTypeDefinition>> GetConfiguredSubjectTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the specified flow settings are fully configured.
    /// </summary>
    /// <param name="flowSettings">The flow settings to evaluate.</param>
    bool IsConfigured(SubjectFlowSettings flowSettings);
}
