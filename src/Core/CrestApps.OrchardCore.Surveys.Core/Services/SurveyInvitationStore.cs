using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

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
    /// Finds an invitation of a collector by its seal lookup value.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <param name="sealLookup">The seal lookup value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invitation, or <see langword="null"/>.</returns>
    public async Task<SurveyInvitation> FindBySealLookupAsync(string collectorId, string sealLookup, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(collectorId) || string.IsNullOrEmpty(sealLookup))
        {
            return null;
        }

        return await Session.Query<SurveyInvitation, SurveyInvitationIndex>(index => index.CollectorId == collectorId && index.SealLookup == sealLookup, collection: SurveysConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lists the invitations of a collector made before seal lookup values were kept, oldest first.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <param name="statuses">The statuses read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invitations.</returns>
    public async Task<IReadOnlyList<SurveyInvitation>> ListWithoutSealLookupAsync(string collectorId, IReadOnlyCollection<SurveyInvitationStatus> statuses, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(collectorId))
        {
            return [];
        }

        var names = (statuses ?? []).Select(status => status.ToString()).ToArray();
        var query = Session.Query<SurveyInvitation, SurveyInvitationIndex>(index => index.CollectorId == collectorId && index.SealLookup == null, collection: SurveysConstants.CollectionName);

        if (names.Length > 0)
        {
            query = query.Where(index => index.Status.IsIn(names));
        }

        return (await query.OrderBy(index => index.Id).ListAsync(cancellationToken)).ToList();
    }

    /// <summary>
    /// Counts the e-mail invitations (to an address, not a user) sent or reminded since a time.
    /// </summary>
    /// <param name="sinceUtc">The time, in UTC.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count.</returns>
    public Task<int> CountEmailedSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
        => Session.QueryIndex<SurveyInvitationIndex>(
            index => index.Email != null && index.UserId == null && (index.SentUtc >= sinceUtc || index.LastReminderUtc >= sinceUtc),
            collection: SurveysConstants.CollectionName)
            .CountAsync(cancellationToken);

    /// <summary>
    /// Lists the invitations of a survey, oldest first.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invitations.</returns>
    public async Task<IReadOnlyList<SurveyInvitation>> ListBySurveyAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return [];
        }

        return (await Session.Query<SurveyInvitation, SurveyInvitationIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.CollectionName)
            .OrderBy(index => index.Id)
            .ListAsync(cancellationToken)).ToList();
    }

    /// <summary>
    /// Lists the invitations of a collector, oldest first.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <param name="statuses">The statuses read, or <see langword="null"/> for all.</param>
    /// <param name="take">The most invitations read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invitations.</returns>
    public async Task<IReadOnlyList<SurveyInvitation>> ListByCollectorAsync(string collectorId, IReadOnlyCollection<SurveyInvitationStatus> statuses, int take, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(collectorId) || take <= 0)
        {
            return [];
        }

        var query = Session.Query<SurveyInvitation, SurveyInvitationIndex>(index => index.CollectorId == collectorId, collection: SurveysConstants.CollectionName);

        if (statuses is { Count: > 0 })
        {
            var names = statuses.Select(status => status.ToString()).ToArray();
            query = query.Where(index => index.Status.IsIn(names));
        }

        var items = await query.OrderBy(index => index.Id).Take(take).ListAsync(cancellationToken);

        return items.ToList();
    }

    /// <summary>
    /// Pages the invitations of a collector, newest first.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total count and the page.</returns>
    public async Task<(int Count, IReadOnlyList<SurveyInvitation> Items)> PageByCollectorAsync(string collectorId, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(collectorId))
        {
            return (0, []);
        }

        var query = Session.Query<SurveyInvitation, SurveyInvitationIndex>(index => index.CollectorId == collectorId, collection: SurveysConstants.CollectionName);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, items.ToList());
    }

    /// <summary>
    /// Gets the YesSql session, for queries of the core services.
    /// </summary>
    internal ISession YesSqlSession => Session;
}
