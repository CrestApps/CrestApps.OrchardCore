using CrestApps.Core.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Creates, changes and removes surveys. Writes go through catalog entry handlers.
/// </summary>
public interface ISurveyManager : INamedCatalogManager<Survey>
{
    /// <summary>
    /// Creates a survey from a template or an empty definition.
    /// </summary>
    /// <param name="request">What to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new survey.</returns>
    Task<Survey> CreateSurveyAsync(SurveyCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new draft of a survey. Fails with <see cref="SurveyErrorCode.Busy"/> when someone else saved since
    /// <paramref name="expectedDraftModifiedUtc"/>.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="draft">The new draft.</param>
    /// <param name="expectedDraftModifiedUtc">When the editor loaded the draft, or <see langword="null"/> to skip the check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The error, or <see cref="SurveyErrorCode.None"/>.</returns>
    Task<SurveyErrorCode> SaveDraftAsync(Survey survey, SurveyDefinition draft, DateTime? expectedDraftModifiedUtc = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a survey with new ids and a new name; the copy is a draft.
    /// </summary>
    /// <param name="surveyId">The survey to copy.</param>
    /// <param name="name">The new technical name, or <see langword="null"/> to derive one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The copy, or <see langword="null"/>.</returns>
    Task<Survey> DuplicateAsync(string surveyId, string name = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the status of a survey (open, close, archive, unarchive), following the allowed moves.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="status">The new status.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The error, or <see cref="SurveyErrorCode.None"/>.</returns>
    Task<SurveyErrorCode> ChangeStatusAsync(string surveyId, SurveyStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a survey with its versions and collectors. Refused when it has responses: archive it instead.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The error, or <see cref="SurveyErrorCode.None"/>.</returns>
    Task<SurveyErrorCode> DeleteSurveyAsync(string surveyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the surveys a record owns, such as a course item.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The surveys.</returns>
    Task<IReadOnlyList<Survey>> ListByOwnerAsync(RecordRef owner, CancellationToken cancellationToken = default);
}
