using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Tells which published surveys drew bank questions that changed (or were retired or removed) since the survey was
/// published, so staff can publish them again.
/// </summary>
public interface IQuestionBankChangeDetector
{
    /// <summary>
    /// Compares a survey's published version with the bank.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The change, or <see langword="null"/> when nothing changed or the survey drew nothing from a bank.</returns>
    Task<SurveyBankChange> GetChangeAsync(Survey survey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds every published survey whose bank questions changed.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The changes.</returns>
    Task<IReadOnlyList<SurveyBankChange>> GetChangedSurveysAsync(CancellationToken cancellationToken = default);
}
