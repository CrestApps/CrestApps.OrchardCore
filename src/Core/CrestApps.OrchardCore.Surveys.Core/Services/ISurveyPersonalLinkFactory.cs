using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Makes personal links to respond. The Survey Invitations feature registers the implementation; without it no personal
/// link can be made.
/// </summary>
public interface ISurveyPersonalLinkFactory
{
    /// <summary>
    /// Creates a personal link.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="context">The context, or <see langword="null"/>.</param>
    /// <param name="respondent">The respondent, or <see langword="null"/>.</param>
    /// <param name="email">The e-mail address, or <see langword="null"/>.</param>
    /// <param name="expiresUtc">When the link expires, in UTC, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The absolute URL, or <see langword="null"/>.</returns>
    Task<string> CreateAsync(string surveyId, RecordRef? context, PartyRef? respondent, string email, DateTime? expiresUtc, CancellationToken cancellationToken = default);
}
