using CrestApps.Core.Omnichannel.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Resolves the stable configuration of each kind of subject a tenant has defined.
/// </summary>
/// <remarks>
/// Reported against subject definitions rather than whatever the host models a subject type with, so
/// the services that read this do not have to know.
/// </remarks>
public interface ISubjectFlowSettingsService
{
    /// <summary>
    /// Gets the subject flow settings for every configured subject definition.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<SubjectFlowSettings>> GetConfiguredFlowSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the subject flow settings for the specified subject definition.
    /// </summary>
    /// <param name="subjectDefinitionName">The subject definition name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<SubjectFlowSettings> FindConfiguredFlowSettingsAsync(string subjectDefinitionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets every configured subject definition.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<SubjectDefinition>> GetConfiguredSubjectDefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the configured subject definitions matching the specified direction.
    /// </summary>
    /// <param name="direction">The subject communication direction to filter by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<SubjectDefinition>> GetConfiguredSubjectDefinitionsAsync(SubjectDirection direction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the specified flow settings belong to a configured subject.
    /// </summary>
    /// <param name="flowSettings">The flow settings to evaluate.</param>
    bool IsConfigured(SubjectFlowSettings flowSettings);
}
