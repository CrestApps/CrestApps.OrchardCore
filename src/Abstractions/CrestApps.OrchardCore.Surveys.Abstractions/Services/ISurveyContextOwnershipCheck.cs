using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Tells whether a respondent owns a record of a registered context kind, such as their own enrollment. The Surveys API
/// accepts a context only when its kind is registered with <c>AddSurveyContextKind</c> and a check of the kind says the
/// caller owns the record, so a caller can never pick a context to get more attempts or to start workflows. Register an
/// implementation with <c>services.AddSurveyContextOwnershipCheck&lt;TCheck&gt;()</c>.
/// </summary>
public interface ISurveyContextOwnershipCheck
{
    /// <summary>
    /// Gets whether this check answers for a context kind.
    /// </summary>
    /// <param name="kind">The context kind.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    bool CanCheck(string kind);

    /// <summary>
    /// Gets whether the respondent owns the context record and the record may hold responses to the survey.
    /// </summary>
    /// <param name="survey">The survey the response is for.</param>
    /// <param name="context">The context record.</param>
    /// <param name="identity">The respondent.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the respondent owns it.</returns>
    Task<bool> IsOwnerAsync(Survey survey, RecordRef context, SurveyRespondentIdentity identity, CancellationToken cancellationToken = default);
}
