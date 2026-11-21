using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Stores invitations.
/// </summary>
public sealed class SurveyInvitationStore : DocumentCatalog<SurveyInvitation, SurveyInvitationIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyInvitationStore(ISession session)
        : base(session)
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <summary>
    /// Finds an invitation by the hash of its token.
    /// </summary>
    /// <param name="tokenHash">The hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invitation, or <see langword="null"/>.</returns>
    public async Task<SurveyInvitation> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(tokenHash))
        {
            return null;
        }

        return await Session.Query<SurveyInvitation, SurveyInvitationIndex>(index => index.TokenHash == tokenHash, collection: SurveysConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the YesSql session, for queries of the core services.
    /// </summary>
    internal ISession YesSqlSession => Session;
}
