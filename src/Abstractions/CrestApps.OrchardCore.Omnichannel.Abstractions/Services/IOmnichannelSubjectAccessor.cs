using CrestApps.OrchardCore.Omnichannel.Models;

namespace CrestApps.OrchardCore.Omnichannel.Services;

/// <summary>
/// Reads and writes the subject a conversation is about.
/// </summary>
public interface IOmnichannelSubjectAccessor
{
    /// <summary>
    /// Gets a subject by identifier.
    /// </summary>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The subject, or <see langword="null"/> when there is no such subject.</returns>
    Task<OmnichannelSubject> GetAsync(string subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a subject.
    /// </summary>
    /// <param name="definitionName">The kind of subject to create.</param>
    /// <param name="contactId">The contact the subject is about.</param>
    /// <param name="fieldValues">The values to set, keyed by field name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The subject as stored, with its identifier.</returns>
    Task<OmnichannelSubject> CreateAsync(
        string definitionName,
        string contactId,
        IReadOnlyDictionary<string, string> fieldValues,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes values onto a subject.
    /// </summary>
    /// <remarks>
    /// Only keys naming a field the definition declares are applied, so a conversation can never author
    /// structure the tenant did not ask for. A key naming no such field is ignored rather than refused,
    /// because the caller is usually a model and one stray key should not lose the rest of the answer.
    /// </remarks>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="fieldValues">The values to set, keyed by field name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the subject changed.</returns>
    Task<bool> ApplyAsync(
        string subjectId,
        IReadOnlyDictionary<string, string> fieldValues,
        CancellationToken cancellationToken = default);
}
