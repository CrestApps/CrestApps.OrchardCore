using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Provides helpers for resolving subject flow configuration from the content-type part settings of the
/// omnichannel subject part.
/// </summary>
public interface ISubjectFlowSettingsService
{
    /// <summary>
    /// Gets the subject flow settings for every content type that has the omnichannel subject part.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<SubjectFlowSettings>> GetConfiguredFlowSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the subject flow settings for the specified subject content type.
    /// </summary>
    /// <param name="subjectContentType">The subject content type name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<SubjectFlowSettings> FindConfiguredFlowSettingsAsync(string subjectContentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the content type definitions that have the omnichannel subject part.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<ContentTypeDefinition>> GetConfiguredSubjectTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the specified flow settings belong to a subject content type.
    /// </summary>
    /// <param name="flowSettings">The flow settings to evaluate.</param>
    bool IsConfigured(SubjectFlowSettings flowSettings);
}
