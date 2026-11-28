namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Tells, without starting anything, whether a respondent may start a new response now, with the same access rules a
/// start runs (status, window, audience, the context's own rules such as an enrollment, limits with their counting mode
/// and accommodations, max responses, quotas), and how many attempts the total limit leaves. A page that offers a start
/// button asks it instead of counting attempts itself.
/// </summary>
public interface ISurveyStartCheck
{
    /// <summary>
    /// Checks a start through the survey's default link, or the collector of the request's token.
    /// </summary>
    /// <param name="request">The start the respondent would make: the survey, the identity and the context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SurveyStartCheckResult> CheckAsync(StartResponseRequest request, CancellationToken cancellationToken = default);
}
