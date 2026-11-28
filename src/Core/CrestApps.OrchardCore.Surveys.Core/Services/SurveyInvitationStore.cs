using System.Linq.Expressions;
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
    /// then is one e-mail, and each invitation reminded since then is one more. Only the last reminder time is kept, so
    /// this counts every e-mail of a site day because an invitation gets at most one reminder a day: scheduled reminders
    /// are at least a day apart, and a manual resend skips an invitation already reminded that day.
    /// <para>
    /// One known gap: the day the clocks go back has 25 hours, so with reminders every day an invitation can get a
    /// scheduled reminder at the start of that site day and another 24 hours later, still the same site day. Its last
    /// reminder time replaces the first, so that day counts one e-mail fewer than went out. The limit is a safeguard,
    /// not a quota, and the error is at most one e-mail per invitation once a year.
    /// </para>
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
    /// <param name="emails"><see langword="true"/> to visit only e-mail invitations (an address and no user),
    /// <see langword="false"/> only the others, <see langword="null"/> all of them.</param>
    /// <param name="stop">Asked before each invitation; the walk ends once it returns <see langword="true"/>.</param>
    /// <param name="pageSize">The number of invitations read at a time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task VisitByCollectorAsync(string collectorId, IReadOnlyCollection<SurveyInvitationStatus> statuses, Func<SurveyInvitation, Task> visit, bool? emails = null, Func<bool> stop = null, int pageSize = 500, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        ArgumentNullException.ThrowIfNull(visit);

        if (string.IsNullOrEmpty(collectorId) || statuses.Count == 0)
        {
            return;
        }

        var names = statuses.Select(status => status.ToString()).ToArray();

        await VisitWhereAsync(index => index.CollectorId == collectorId && index.Status.IsIn(names), emails, visit, stop, pageSize, cancellationToken);
    }

    /// <summary>
    /// Visits the first sends of a collector that were held back (see
    /// <see cref="CrestApps.OrchardCore.Surveys.Core.Invitations.SurveyInvitationHold"/>), oldest first, by the index's
    /// hold mark, a page at a time as <see cref="VisitByCollectorAsync"/> does: each page is written and let go of before
    /// the next is read, and the walk stops at the last row there when it began, so an invitation held again (whose index
    /// row is written again under a new id) isn't visited twice.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <param name="visit">The visit, which saves what it changes through this store.</param>
    /// <param name="stop">Asked before each invitation; the walk ends once it returns <see langword="true"/>.</param>
    /// <param name="pageSize">The number of invitations read at a time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task VisitHeldByCollectorAsync(string collectorId, Func<SurveyInvitation, Task> visit, Func<bool> stop = null, int pageSize = 500, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visit);

        if (string.IsNullOrEmpty(collectorId))
        {
            return;
        }

        var pending = nameof(SurveyInvitationStatus.Pending);

        await VisitWhereAsync(index => index.CollectorId == collectorId && index.Held && index.Status == pending, emails: null, visit, stop, pageSize, cancellationToken);
    }

    /// <summary>
    /// Visits the invitations of a collector in some statuses that are due a reminder, by the index only: reminders
    /// didn't stop, fewer than <paramref name="maxReminders"/> were sent, and the last reminder (or the send, before the
    /// first reminder) is at or before <paramref name="lastContactBefore"/>. A page at a time, oldest first, as
    /// <see cref="VisitByCollectorAsync"/> does, so a large list with nothing due reads nothing but the index.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <param name="statuses">The statuses visited.</param>
    /// <param name="lastContactBefore">The latest last contact of an invitation due a reminder, in UTC.</param>
    /// <param name="maxReminders">The most reminders an invitation gets.</param>
    /// <param name="visit">The visit, which saves what it changes through this store.</param>
    /// <param name="emails"><see langword="true"/> to visit only e-mail invitations (an address and no user),
    /// <see langword="false"/> only the others, <see langword="null"/> all of them.</param>
    /// <param name="stop">Asked before each invitation; the walk ends once it returns <see langword="true"/>.</param>
    /// <param name="pageSize">The number of invitations read at a time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task VisitDueForReminderAsync(string collectorId, IReadOnlyCollection<SurveyInvitationStatus> statuses, DateTime lastContactBefore, int maxReminders, Func<SurveyInvitation, Task> visit, bool? emails = null, Func<bool> stop = null, int pageSize = 500, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        ArgumentNullException.ThrowIfNull(visit);

        if (string.IsNullOrEmpty(collectorId) || statuses.Count == 0 || maxReminders <= 0)
        {
            return;
        }

        await VisitWhereAsync(DueForReminder(collectorId, statuses, lastContactBefore, maxReminders), emails, visit, stop, pageSize, cancellationToken);
    }

    /// <summary>
    /// Gets whether a collector has work for a send, by the index only: a held first send (when
    /// <paramref name="emails"/> is <see langword="true"/>), or an invitation in some statuses that is due a reminder as
    /// <see cref="VisitDueForReminderAsync"/> reads them, or, with no <paramref name="lastContactBefore"/>, any
    /// invitation in those statuses (a manual resend). So a list with nothing to send costs a query or two and no lock.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <param name="statuses">The statuses reminded.</param>
    /// <param name="lastContactBefore">The latest last contact of an invitation due a reminder, in UTC, or
    /// <see langword="null"/> for every invitation in <paramref name="statuses"/>.</param>
    /// <param name="maxReminders">The most reminders an invitation gets; ignored without <paramref name="lastContactBefore"/>.</param>
    /// <param name="emails"><see langword="true"/> for e-mail invitations (an address and no user), <see langword="false"/>
    /// for the others.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when there is something to send.</returns>
    public async Task<bool> HasWorkAsync(string collectorId, IReadOnlyCollection<SurveyInvitationStatus> statuses, DateTime? lastContactBefore, int maxReminders, bool emails, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        if (string.IsNullOrEmpty(collectorId))
        {
            return false;
        }

        if (emails)
        {
            var pending = nameof(SurveyInvitationStatus.Pending);
            var held = await QueryRows(index => index.CollectorId == collectorId && index.Held && index.Status == pending, emails: true).FirstOrDefaultAsync(cancellationToken);

            if (held is not null)
            {
                return true;
            }
        }

        if (statuses.Count == 0)
        {
            return false;
        }

        if (lastContactBefore is { } before)
        {
            return maxReminders > 0 && await QueryRows(DueForReminder(collectorId, statuses, before, maxReminders), emails).FirstOrDefaultAsync(cancellationToken) is not null;
        }

        var names = statuses.Select(status => status.ToString()).ToArray();

        return await QueryRows(index => index.CollectorId == collectorId && index.Status.IsIn(names), emails).FirstOrDefaultAsync(cancellationToken) is not null;
    }

    /// <summary>
    /// Reads the index row of an invitation as the database has it now, after this session's own pending changes are
    /// written: what other requests committed (a completion, an open, another send) shows here even when the
    /// invitation was loaded earlier. Saving an invitation writes its row again under a new id, so the row's id tells
    /// whether anyone saved it since.
    /// </summary>
    /// <param name="itemId">The invitation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row, or <see langword="null"/> when the invitation no longer exists.</returns>
    public async Task<SurveyInvitationIndex> FindRowAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        await Session.FlushAsync(cancellationToken);

        return await Session.QueryIndex<SurveyInvitationIndex>(index => index.ItemId == itemId, collection: CollectionName)
            .OrderByDescending(index => index.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Loads an invitation again from the database, in place of the copy this session holds: the copy is let go of
    /// (after this session's pending changes are written), so the invitation returned carries what other requests
    /// committed since the copy was loaded. Save the invitation returned, never the old copy.
    /// </summary>
    /// <param name="itemId">The invitation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invitation, or <see langword="null"/> when it no longer exists.</returns>
    public async Task<SurveyInvitation> ReloadAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        await Session.FlushAsync(cancellationToken);

        var held = await FindByIdAsync(itemId, cancellationToken);

        if (held is null)
        {
            return null;
        }

        Session.Detach(held, CollectionName);

        return await FindByIdAsync(itemId, cancellationToken);
    }

    /// <summary>
    /// Loads the current copy of an invitation from the database in place of a copy the caller holds: this session's
    /// pending changes are written, the held copy is let go of (when this session still tracks it; a copy loaded before
    /// a commit no longer is), and the invitation is read again. Save the copy returned, never the old one: a copy this
    /// session no longer tracks would be saved as a new document.
    /// </summary>
    /// <param name="copy">The copy held.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current copy, or <see langword="null"/> when the invitation no longer exists.</returns>
    public async Task<SurveyInvitation> LoadCurrentAsync(SurveyInvitation copy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(copy);

        await Session.FlushAsync(cancellationToken);
        Session.Detach(copy, CollectionName);

        return await FindByIdAsync(copy.ItemId, cancellationToken);
    }

    /// <summary>
    /// Loads the current copies of invitations in one read, as <see cref="LoadCurrentAsync(SurveyInvitation, CancellationToken)"/>
    /// does for one.
    /// </summary>
    /// <param name="copies">The copies held.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current copies of those that still exist.</returns>
    public async Task<IReadOnlyCollection<SurveyInvitation>> LoadCurrentAsync(IReadOnlyCollection<SurveyInvitation> copies, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(copies);

        if (copies.Count == 0)
        {
            return [];
        }

        await Session.FlushAsync(cancellationToken);

        foreach (var copy in copies)
        {
            Session.Detach(copy, CollectionName);
        }

        return await GetAsync(copies.Select(copy => copy.ItemId).Distinct(StringComparer.Ordinal).ToArray(), cancellationToken);
    }

    /// <summary>
    /// Applies a change to the current copy of an invitation and writes it at once, instead of saving a copy loaded
    /// earlier: what other requests committed since that copy was loaded (a completion, an open, a send's marks) is kept,
    /// and only the fields <paramref name="apply"/> sets change.
    /// <para>
    /// A gap of milliseconds remains between the read and the write, in which another request's commit would be
    /// overwritten; closing it needs a concurrency check on every writer of invitations, which the stores don't have.
    /// </para>
    /// </summary>
    /// <param name="copy">The copy held.</param>
    /// <param name="apply">Changes the current copy; returns <see langword="true"/> when it changed it.</param>
    /// <param name="detach">Whether to let go of the current copy once it is written, so a long walk doesn't keep every
    /// copy it wrote.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current copy, or the copy held when the invitation no longer exists.</returns>
    public async Task<SurveyInvitation> UpdateCurrentAsync(SurveyInvitation copy, Func<SurveyInvitation, bool> apply, bool detach = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(copy);
        ArgumentNullException.ThrowIfNull(apply);

        var current = await LoadCurrentAsync(copy, cancellationToken);

        if (current is null)
        {
            return copy;
        }

        if (apply(current))
        {
            await UpdateAsync(current, cancellationToken);
            await Session.FlushAsync(cancellationToken);
        }

        if (detach)
        {
            Session.Detach(current, CollectionName);
        }

        return current;
    }

    /// <summary>
    /// Lets go of a copy of an invitation this session tracks, once its changes are written.
    /// </summary>
    /// <param name="invitation">The invitation.</param>
    public void Detach(SurveyInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        Session.Detach(invitation, CollectionName);
    }

    /// <summary>
    /// Writes this session's pending changes now, inside its transaction, so a send's marks reach the database right
    /// after the e-mail went out rather than at the end of a page.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public Task FlushAsync(CancellationToken cancellationToken = default)
        => Session.FlushAsync(cancellationToken);

    /// <summary>
    /// Commits this session's changes, so other requests and senders see them before a lock is let go of. The session
    /// stays usable; everything it loaded before is let go of.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public Task CommitAsync(CancellationToken cancellationToken = default)
        => Session.SaveChangesAsync(cancellationToken);

    // The index filter of the invitations of a collector due a reminder.
    private static Expression<Func<SurveyInvitationIndex, bool>> DueForReminder(string collectorId, IReadOnlyCollection<SurveyInvitationStatus> statuses, DateTime lastContactBefore, int maxReminders)
    {
        var names = statuses.Select(status => status.ToString()).ToArray();

        return index => index.CollectorId == collectorId
            && index.Status.IsIn(names)
            && index.RemindersStopped == false
            && index.RemindersSent < maxReminders
            && ((index.LastReminderUtc != null && index.LastReminderUtc <= lastContactBefore)
                || (index.LastReminderUtc == null && index.SentUtc != null && index.SentUtc <= lastContactBefore));
    }

    /// <summary>
    /// Visits the invitations of a collector in some statuses whose link expired, by the index only, a page at a time.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <param name="statuses">The statuses visited.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="visit">The visit, which saves what it changes through this store.</param>
    /// <param name="pageSize">The number of invitations read at a time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task VisitExpiredAsync(string collectorId, IReadOnlyCollection<SurveyInvitationStatus> statuses, DateTime utcNow, Func<SurveyInvitation, Task> visit, int pageSize = 500, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        ArgumentNullException.ThrowIfNull(visit);

        if (string.IsNullOrEmpty(collectorId) || statuses.Count == 0)
        {
            return;
        }

        var names = statuses.Select(status => status.ToString()).ToArray();

        await VisitWhereAsync(index => index.CollectorId == collectorId && index.Status.IsIn(names) && index.ExpiresUtc != null && index.ExpiresUtc <= utcNow, emails: null, visit, stop: null, pageSize, cancellationToken);
    }

    // Reads the index rows that match a filter, narrowed to the e-mail invitations (an address and no user), to the
    // others, or not at all.
    private IQueryIndex<SurveyInvitationIndex> QueryRows(Expression<Func<SurveyInvitationIndex, bool>> filter, bool? emails)
    {
        var query = Session.QueryIndex(filter, collection: CollectionName);

        return emails switch
        {
            true => query.Where(index => index.UserId == null && index.Email != null),
            false => query.Where(index => index.UserId != null || index.Email == null),
            _ => query,
        };
    }

    // Visits the invitations whose index rows match, a page at a time, up to the last row there when the walk began, until
    // the stop condition holds.
    private async Task VisitWhereAsync(Expression<Func<SurveyInvitationIndex, bool>> filter, bool? emails, Func<SurveyInvitation, Task> visit, Func<bool> stop, int pageSize, CancellationToken cancellationToken)
    {
        if (stop?.Invoke() == true)
        {
            return;
        }

        var last = await QueryRows(filter, emails)
            .OrderByDescending(index => index.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var upToId = last?.Id ?? 0;
        var afterId = 0L;
        var stopped = false;

        while (afterId < upToId && !stopped)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var from = afterId;
            var rows = (await QueryRows(filter, emails)
                .Where(index => index.Id > from && index.Id <= upToId)
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
                if (stop?.Invoke() == true)
                {
                    stopped = true;

                    break;
                }

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
