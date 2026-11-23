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
    /// Lists a collector's invitations of one user, one recipient or one e-mail address, oldest first, by the index,
    /// so inviting someone doesn't read the whole collector. Give exactly one of the three.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <param name="userId">The user, or <see langword="null"/>.</param>
    /// <param name="recipientRef">The recipient as Kind:Id, or <see langword="null"/>.</param>
    /// <param name="email">The e-mail address, or <see langword="null"/>; compared without case.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invitations, at most 10.</returns>
    public async Task<IReadOnlyList<SurveyInvitation>> ListForRecipientAsync(string collectorId, string userId, string recipientRef, string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(collectorId))
        {
            return [];
        }

        IQuery<SurveyInvitation, SurveyInvitationIndex> query;

        if (!string.IsNullOrEmpty(userId))
        {
            query = Session.Query<SurveyInvitation, SurveyInvitationIndex>(index => index.CollectorId == collectorId && index.UserId == userId, collection: SurveysConstants.CollectionName);
        }
        else if (!string.IsNullOrEmpty(recipientRef))
        {
            var key = recipientRef.Length > 255 ? recipientRef[..255] : recipientRef;
            query = Session.Query<SurveyInvitation, SurveyInvitationIndex>(index => index.CollectorId == collectorId && index.RecipientRef == key, collection: SurveysConstants.CollectionName);
        }
        else if (!string.IsNullOrWhiteSpace(email))
        {
            var key = email.Trim().ToLowerInvariant();
            key = key.Length > 254 ? key[..254] : key;
            query = Session.Query<SurveyInvitation, SurveyInvitationIndex>(index => index.CollectorId == collectorId && index.Email == key, collection: SurveysConstants.CollectionName);
        }
        else
        {
            return [];
        }

        return (await query.OrderBy(index => index.Id).Take(10).ListAsync(cancellationToken)).ToList();
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
    /// Counts the invitation e-mails (to an address, not a user) sent since a time: each invitation first sent since
    /// then is one e-mail, and each invitation reminded since then is one more. An invitation reminded twice in that
    /// time (a manual resend) counts its reminders once, since only the last reminder time is kept.
    /// </summary>
    /// <param name="sinceUtc">The time, in UTC.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count.</returns>
    public async Task<int> CountEmailedSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var sent = await Session.QueryIndex<SurveyInvitationIndex>(
            index => index.Email != null && index.UserId == null && index.SentUtc >= sinceUtc,
            collection: SurveysConstants.CollectionName)
            .CountAsync(cancellationToken);
        var reminded = await Session.QueryIndex<SurveyInvitationIndex>(
            index => index.Email != null && index.UserId == null && index.LastReminderUtc >= sinceUtc,
            collection: SurveysConstants.CollectionName)
            .CountAsync(cancellationToken);

        return sent + reminded;
    }

    /// <summary>
    /// Visits the invitations of a survey a page at a time, oldest first, and saves those the visit changed. Each page is
    /// written and let go of before the next is read, so a survey with many invitations is never held in memory at once.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="visit">The visit; returns <see langword="true"/> when it changed the invitation.</param>
    /// <param name="pageSize">The number of invitations read at a time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many invitations were saved.</returns>
    public async Task<int> ForEachBySurveyAsync(string surveyId, Func<SurveyInvitation, Task<bool>> visit, int pageSize = 200, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visit);

        if (string.IsNullOrEmpty(surveyId))
        {
            return 0;
        }

        // Saving can write an invitation's index row again under a new id, so the walk stops at the last row there when
        // it began: a saved invitation is never visited twice.
        var last = await Session.QueryIndex<SurveyInvitationIndex>(index => index.SurveyId == surveyId, collection: CollectionName)
            .OrderByDescending(index => index.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var upToId = last?.Id ?? 0;
        var afterId = 0L;
        var saved = 0;

        while (afterId < upToId)
        {
            var rows = (await Session.QueryIndex<SurveyInvitationIndex>(index => index.SurveyId == surveyId && index.Id > afterId && index.Id <= upToId, collection: CollectionName)
                .OrderBy(index => index.Id)
                .Take(pageSize)
                .ListAsync(cancellationToken)).ToList();

            if (rows.Count == 0)
            {
                break;
            }

            afterId = rows[^1].Id;

            var invitations = await GetAsync(rows.Select(row => row.ItemId).ToArray(), cancellationToken);

            foreach (var invitation in invitations)
            {
                if (await visit(invitation))
                {
                    await UpdateAsync(invitation, cancellationToken);
                    saved++;
                }
            }

            await Session.FlushAsync(cancellationToken);

            foreach (var invitation in invitations)
            {
                Session.Detach(invitation, CollectionName);
            }
        }

        return saved;
    }

    /// <summary>
    /// Visits every invitation of a collector in some statuses, oldest first, a page at a time: each page's changes are
    /// written and let go of before the next is read, so a large list is never held in memory at once and no invitation
    /// hides the ones after it. The walk stops at the last row there when it began, so a saved invitation (whose index
    /// row is written again under a new id) is never visited twice.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <param name="statuses">The statuses visited.</param>
    /// <param name="visit">The visit, which saves what it changes through this store.</param>
    /// <param name="pageSize">The number of invitations read at a time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task VisitByCollectorAsync(string collectorId, IReadOnlyCollection<SurveyInvitationStatus> statuses, Func<SurveyInvitation, Task> visit, int pageSize = 500, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        ArgumentNullException.ThrowIfNull(visit);

        if (string.IsNullOrEmpty(collectorId) || statuses.Count == 0)
        {
            return;
        }

        var names = statuses.Select(status => status.ToString()).ToArray();
        var last = await Session.QueryIndex<SurveyInvitationIndex>(index => index.CollectorId == collectorId && index.Status.IsIn(names), collection: CollectionName)
            .OrderByDescending(index => index.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var upToId = last?.Id ?? 0;
        var afterId = 0L;

        while (afterId < upToId)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rows = (await Session.QueryIndex<SurveyInvitationIndex>(index => index.CollectorId == collectorId && index.Status.IsIn(names) && index.Id > afterId && index.Id <= upToId, collection: CollectionName)
                .OrderBy(index => index.Id)
                .Take(Math.Max(1, pageSize))
                .ListAsync(cancellationToken)).ToList();

            if (rows.Count == 0)
            {
                break;
            }

            afterId = rows[^1].Id;

            var invitations = await GetAsync(rows.Select(row => row.ItemId).ToArray(), cancellationToken);

            foreach (var invitation in invitations)
            {
                await visit(invitation);
            }

            await Session.FlushAsync(cancellationToken);

            foreach (var invitation in invitations)
            {
                Session.Detach(invitation, CollectionName);
            }
        }
    }

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
    /// Lists the first sends of a collector that were held back (see
    /// <see cref="CrestApps.OrchardCore.Surveys.Core.Invitations.SurveyInvitationHold"/>), oldest first, by the
    /// index's hold mark, so they are found however many invitations were created and never sent.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <param name="take">The most invitations read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The held invitations.</returns>
    public async Task<IReadOnlyList<SurveyInvitation>> ListHeldByCollectorAsync(string collectorId, int take, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(collectorId) || take <= 0)
        {
            return [];
        }

        var pending = nameof(SurveyInvitationStatus.Pending);
        var items = await Session.Query<SurveyInvitation, SurveyInvitationIndex>(
            index => index.CollectorId == collectorId && index.Held && index.Status == pending,
            collection: SurveysConstants.CollectionName)
            .OrderBy(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken);

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
