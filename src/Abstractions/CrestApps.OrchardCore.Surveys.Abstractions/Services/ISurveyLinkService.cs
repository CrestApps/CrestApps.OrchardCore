using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Makes links to respond, such as a personal link for a ticket's requester.
/// </summary>
public interface ISurveyLinkService
{
    /// <summary>
    /// Gets the public link of a survey's default collector.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The absolute URL, or <see langword="null"/> when the survey is not published.</returns>
    Task<string> GetPublicLinkAsync(string surveyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a personal link for a respondent and a context.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="context">The context, or <see langword="null"/>.</param>
    /// <param name="respondent">The respondent, or <see langword="null"/>.</param>
    /// <param name="email">The e-mail address, or <see langword="null"/>.</param>
    /// <param name="expiresUtc">When the link expires, in UTC, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The absolute URL, or <see langword="null"/> when the survey is not published.</returns>
    Task<string> CreateLinkAsync(string surveyId, RecordRef? context, PartyRef? respondent, string email, DateTime? expiresUtc, CancellationToken cancellationToken = default);
}
