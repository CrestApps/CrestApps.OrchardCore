using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Makes no personal link; used when the Survey Invitations feature is off.
/// </summary>
public sealed class NullSurveyPersonalLinkFactory : ISurveyPersonalLinkFactory
{
    /// <inheritdoc />
    public Task<string> CreateAsync(string surveyId, RecordRef? context, PartyRef? respondent, string email, DateTime? expiresUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<string>(null);
}
