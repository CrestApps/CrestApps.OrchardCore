using System.Data.Common;
using System.Diagnostics;
using System.Linq.Expressions;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Upkeep of responses, a batch at a time: submits the responses left in progress on a closed survey as they were saved,
/// abandons responses idle past their survey's expiry, applies retention (delete or anonymize), deletes old
/// participation records (except those of surveys with a total limit), and submits responses whose deadline was missed.
/// Each response is handled in its own child shell scope, with a session of its own that commits on its own: a
/// response that fails, even when its save fails, is logged and skipped, and never takes the other responses' changes
/// with it. After <see cref="MaxUpkeepFailures"/> failures in a row it is no longer tried, so failing responses can't
/// fill every batch; a step that succeeds clears the count, and an admin can clear it to have upkeep try again.
/// Failures that aren't counted can't fill a batch either: each step reads past the responses it has seen, a failure
/// from the server's environment leaves its survey out of the rest of the step, and a response whose failure couldn't
/// be counted is skipped for the rest of the run.
/// </summary>
public sealed class SurveyMaintenanceService
{
    /// <summary>
    /// The number of failures in a row after which upkeep stops trying a response.
    /// </summary>
    public const int MaxUpkeepFailures = 5;

    /// <summary>
    /// The most batches of old participation records one run deletes.
    /// </summary>
    public const int MaxParticipationBatches = 200;

    /// <summary>
    /// The most pages of responses one step reads in a run. Responses that don't use the step's budget (a failure that
    /// couldn't be counted, a survey left out, a response not idle yet) are read past, up to this many pages.
    /// </summary>
    public const int MaxPagesPerStep = 20;

    // The most surveys a step leaves out in a run, well under SQL Server's limit of 2,100 parameters.
    private const int MaxLeftOutSurveys = 1000;

    // How long one run may spend deleting old participation records. Each start, completion and reopen writes a record,
    // so one batch an hour falls behind on a busy site; the run keeps deleting batches until it catches up or this ends.
    private static readonly TimeSpan _participationBudget = TimeSpan.FromSeconds(30);

    private readonly SurveyStore _surveyStore;
    private readonly SurveyResponseStore _responseStore;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IEnumerable<ISurveyOverrideService> _overrides;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyMaintenanceService"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="responseStore">The response store.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="overrides">The accommodations, when the feature that keeps them is on.</param>
    public SurveyMaintenanceService(
        SurveyStore surveyStore,
        SurveyResponseStore responseStore,
        ISession session,
        IClock clock,
        ILogger<SurveyMaintenanceService> logger,
        IEnumerable<ISurveyOverrideService> overrides)
    {
        _surveyStore = surveyStore;
        _responseStore = responseStore;
        _session = session;
        _clock = clock;
        _logger = logger;
        _overrides = overrides;
    }

    private enum UpkeepOutcome
    {
        // Handled, or its failure was counted: it uses the step's budget.
        Handled,

        // Not handled, and the survey's other responses are left out of the rest of the step: a failure from the
        // server's environment, or (when abandoning) a response that isn't idle yet.
        LeaveSurvey,

        // Failed, and the failure couldn't be counted: it is read past for the rest of the run.
        NotCounted,

        // Not handled because the step doesn't apply to it yet (an accommodation keeps its survey open to it): it is read
        // past without using the step's budget.
        Skipped,
    }

    /// <summary>
    /// Runs one round of upkeep. It must run in a shell scope: each response is handled in a child scope of it.
    /// </summary>
    /// <param name="batchSize">The most records each step handles.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task RunAsync(int batchSize = SurveysConstants.PageSize, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        // The surveys read in the run, each once. A step finds the surveys it needs by the survey index and reads only
        // those, never every survey of the site.
        var surveys = new Dictionary<string, Survey>(StringComparer.Ordinal);

        // Responses of a closed survey are submitted before idle ones are abandoned, so a response left in progress at
        // the close is submitted as it was saved rather than dropped.
        await SubmitClosedAsync(now, batchSize, surveys, cancellationToken);
        await AbandonIdleAsync(now, batchSize, surveys, cancellationToken);
        await ApplyRetentionAsync(now, batchSize, surveys, cancellationToken);
        await SubmitOverdueAsync(now, batchSize, cancellationToken);
        await DeleteExpiredParticipationAsync(now, batchSize, surveys, cancellationToken);
        await FollowUpDeletesAsync(now, cancellationToken);
    }

    // Follows up survey deletes (SurveyDeletion): finishes a delete that stopped half way (the survey is still marked
    // deleting after SurveyManager.DeletionFollowUp), and once the survey is gone removes what a send, a reminder or a test
    // response that was already running wrote for it after the delete; the note is forgotten after the follow-up time.
    // Each delete runs in a child scope of its own; one that fails is logged and tried again on the next run.
    private async Task FollowUpDeletesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var notes = (await _session.Query<SurveyDeletion>(SurveysConstants.CollectionName).ListAsync(cancellationToken))
            .Select(note => (note.Id, note.SurveyId, StartedUtc: DateTime.SpecifyKind(note.StartedUtc, DateTimeKind.Utc)))
            .ToList();

        await EndReadAsync(cancellationToken);

        foreach (var (noteId, surveyId, startedUtc) in notes)
        {
            try
            {
                await ShellScope.UsingChildScopeAsync(async scope =>
                {
                    var services = scope.ServiceProvider;
                    var session = services.GetRequiredService<ISession>();
                    var survey = string.IsNullOrEmpty(surveyId) ? null : await services.GetRequiredService<SurveyStore>().FindByIdAsync(surveyId, cancellationToken);

                    if (survey is not null)
                    {
                        if (survey.DeletingUtc is { } deleting
                            && now - DateTime.SpecifyKind(deleting, DateTimeKind.Utc) >= SurveyManager.DeletionFollowUp
                            && services.GetService<ISurveyManager>() is { } manager)
                        {
                            await manager.DeleteSurveyAsync(surveyId, cancellationToken);
                            await session.SaveChangesAsync(cancellationToken);
                        }

                        return;
                    }

                    var removed = string.IsNullOrEmpty(surveyId) ? 0 : await services.GetRequiredService<SurveyDataPurger>().DeleteBatchAsync(surveyId, SurveyDataPurger.BatchSize, cancellationToken);

                    if (removed == 0 && now - startedUtc >= SurveyManager.DeletionFollowUp
                        && (await session.GetAsync<SurveyDeletion>([noteId], SurveysConstants.CollectionName, cancellationToken)).FirstOrDefault() is { } note)
                    {
                        session.Delete(note, SurveysConstants.CollectionName);
                        await session.SaveChangesAsync(cancellationToken);
                    }
                });
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Survey upkeep could not follow up the delete of the survey '{SurveyId}'. It tries again on the next run.", surveyId);
            }
        }
    }

    /// <summary>
    /// Gets whether an upkeep failure comes from the server's environment rather than from the response, such as an
    /// anonymity secret this server can't read or a transient database error. Such a failure is logged but not counted
    /// on the response, so a problem with the server never stops upkeep of a response for good; the survey's other
    /// responses are left until the next run, so they don't fill the batch.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns><see langword="true"/> when the failure is not the response's own.</returns>
    public static bool IsEnvironmental(Exception exception)
        => exception switch
        {
            SurveyAnonymitySecretUnreadableException => true,
            TimeoutException => true,
            DbException { IsTransient: true } => true,
            _ => exception?.InnerException is { } inner && IsEnvironmental(inner),
        };

    /// <summary>
    /// Removes who responded from a response: user, respondent, IP address, invitation and the key its participation
    /// was counted under, and the answers' references to files (the caller removes the files themselves, whose uploader
    /// names the respondent).
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns><see langword="true"/> when something was removed.</returns>
    public static bool Anonymize(SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var changed = response.UserId is not null || response.Respondent is not null || response.IpAddress is not null || response.InvitationId is not null || response.RespondentKey is not null;

        response.UserId = null;
        response.Respondent = null;
        response.IpAddress = null;
        response.InvitationId = null;
        response.RespondentKey = null;

        foreach (var answer in response.Answers.Values)
        {
            if (answer?.AttachmentIds is { Count: > 0 })
            {
                answer.AttachmentIds = [];
                changed = true;
            }
        }

        return changed;
    }

    private async Task AbandonIdleAsync(DateTime now, int batchSize, Dictionary<string, Survey> surveys, CancellationToken cancellationToken)
    {
        var inProgress = nameof(SurveyResponseStatus.InProgress);

        // The shortest expiry a survey can have is one hour: anything more recent cannot be idle yet. A response with a
        // deadline is submitted at its deadline instead.
        var threshold = now.AddHours(-1);

        // Oldest activity first: once a response of a survey isn't idle yet, none of that survey's later ones is, so the
        // survey is left out of the rest of the step and the responses of surveys with a long expiry never fill a batch.
        await WalkAsync(
            batchSize,
            batchSize,
            async (after, leftOut) =>
            {
                var query = LeaveOut(
                    _responseStore.YesSqlSession.QueryIndex<SurveyResponseIndex>(
                        index => index.Status == inProgress && index.DeadlineUtc == null && index.LastActivityUtc < threshold && index.UpkeepFailures < MaxUpkeepFailures,
                        collection: SurveysConstants.ResponsesCollectionName),
                    leftOut);

                if (after is not null)
                {
                    var lastActivity = after.LastActivityUtc;
                    var lastId = after.Id;

                    query = query.Where(index => index.LastActivityUtc > lastActivity || (index.LastActivityUtc == lastActivity && index.Id > lastId));
                }

                var rows = (await query
                    .OrderBy(index => index.LastActivityUtc)
                    .ThenBy(index => index.Id)
                    .Take(batchSize)
                    .ListAsync(cancellationToken)).ToList();

                // The surveys are read with the page, before the run's read transaction ends.
                foreach (var surveyId in rows.Select(row => row.SurveyId).Where(id => id is not null).Distinct(StringComparer.Ordinal))
                {
                    await SurveyAsync(surveyId, surveys, cancellationToken);
                }

                return rows;
            },
            row =>
            {
                var survey = row.SurveyId is null ? null : surveys.GetValueOrDefault(row.SurveyId);
                var hours = Math.Max(survey?.Access?.IncompleteExpiryHours ?? 168, 1);
                var idleBefore = now.AddHours(-hours);

                if (row.LastActivityUtc >= idleBefore)
                {
                    return Task.FromResult(UpkeepOutcome.LeaveSurvey);
                }

                return TryAsync("abandon", row, async services => Done(await services.GetRequiredService<SurveyResponseService>().AbandonAsync(row.ItemId, idleBefore, cancellationToken)), cancellationToken);
            },
            cancellationToken);
    }

    private async Task ApplyRetentionAsync(DateTime now, int batchSize, Dictionary<string, Survey> surveys, CancellationToken cancellationToken)
    {
        var inProgress = nameof(SurveyResponseStatus.InProgress);
        var handled = 0;

        foreach (var id in await SurveyIdsAsync(index => index.RetentionDays > 0, survey => survey.Retention?.Days > 0, surveys, cancellationToken))
        {
            var survey = await SurveyAsync(id, surveys, cancellationToken);

            if (survey?.Retention?.Days is not { } days || days <= 0 || handled >= batchSize)
            {
                continue;
            }

            var before = now.AddDays(-days);
            var surveyId = survey.ItemId;
            var anonymize = survey.Retention.Action == RetentionAction.Anonymize;

            handled += await WalkAsync(
                batchSize - handled,
                batchSize,
                async (after, leftOut) =>
                {
                    // An anonymized response stays, so its index column leaves it out; otherwise the oldest anonymized
                    // responses would fill every batch and the step would never reach the rest.
                    var query = LeaveOut(
                        _responseStore.YesSqlSession.QueryIndex<SurveyResponseIndex>(
                            index => index.SurveyId == surveyId && index.Status != inProgress && index.LastActivityUtc < before && index.UpkeepFailures < MaxUpkeepFailures,
                            collection: SurveysConstants.ResponsesCollectionName),
                        leftOut);

                    if (anonymize)
                    {
                        query = query.Where(index => !index.Anonymized);
                    }

                    if (after is not null)
                    {
                        var lastActivity = after.LastActivityUtc;
                        var lastId = after.Id;

                        query = query.Where(index => index.LastActivityUtc > lastActivity || (index.LastActivityUtc == lastActivity && index.Id > lastId));
                    }

                    return (await query
                        .OrderBy(index => index.LastActivityUtc)
                        .ThenBy(index => index.Id)
                        .Take(batchSize)
                        .ListAsync(cancellationToken)).ToList();
                },
                row => anonymize
                    ? TryAsync("anonymize", row, services => AnonymizeAsync(services, row.ItemId, now, cancellationToken), cancellationToken)
                    : TryAsync("delete", row, async services => Done(await services.GetRequiredService<SurveyResponseService>().DeleteAsync(row.ItemId, actorId: null, cancellationToken)), cancellationToken),
                cancellationToken);
        }
    }

    // Submits the responses left in progress on a survey that closed (staff closed or archived it, or its closing date
    // passed), as they were saved before the close: a closed survey takes no new answers. A respondent whose
    // accommodation closes later keeps answering; their response is skipped until then. Test responses are left alone.
    //
    // On a site upgraded from a version without this step (SurveyCloseStepStart), a survey that closed before the step
    // started is left alone, except for the responses active since then: the earlier version left its responses in
    // progress for idle expiry, and completing (scoring, notifying, grading) them now would act on old data.
    //
    // A response whose respondent has an accommodation that closes later is left out of the query when the
    // accommodation names the user, and read past without using the step's budget otherwise (an audience), so
    // accommodated responses never starve the other surveys' close submissions.
    private async Task SubmitClosedAsync(DateTime now, int batchSize, Dictionary<string, Survey> surveys, CancellationToken cancellationToken)
    {
        var handled = 0;
        var stepStart = await ReadCloseStepStartAsync(cancellationToken);
        var closed = nameof(SurveyStatus.Closed);
        var archived = nameof(SurveyStatus.Archived);
        var closedIds = await SurveyIdsAsync(
            index => index.Status == closed || index.Status == archived || (index.ClosesUtc != null && index.ClosesUtc <= now),
            survey => SurveyAvailability.IsClosed(survey, surveyOverride: null, collector: null, now),
            surveys,
            cancellationToken);

        foreach (var surveyId in closedIds)
        {
            if (handled >= batchSize)
            {
                return;
            }

            // One indexed read tells whether the closed survey has anything to submit, so a closed survey with nothing in
            // progress costs no document load.
            if ((await ReadClosedPageAsync(surveyId, activeSince: null, [], after: null, batchSize: 1, cancellationToken)).Count == 0
                || await SurveyAsync(surveyId, surveys, cancellationToken) is not { DeletingUtc: null } survey
                || !SurveyAvailability.IsClosed(survey, surveyOverride: null, collector: null, now))
            {
                continue;
            }

            DateTime? activeSince = stepStart is { } start && !ClosedSince(survey, start, now) ? start : null;
            string[] stillOpenUsers = null;
            handled += await WalkAsync(
                batchSize - handled,
                batchSize,
                async (after, leftOut) =>
                {
                    var rows = await ReadClosedPageAsync(surveyId, activeSince, stillOpenUsers ?? [], after, batchSize, cancellationToken);

                    // The accommodations are read only for a survey that has responses to submit, once.
                    if (stillOpenUsers is null && rows.Count > 0)
                    {
                        stillOpenUsers = await StillOpenUsersAsync(survey, now, cancellationToken);

                        if (stillOpenUsers.Length > 0)
                        {
                            rows = await ReadClosedPageAsync(surveyId, activeSince, stillOpenUsers, after, batchSize, cancellationToken);
                        }
                    }

                    return rows;
                },
                row => SubmitAtCloseAsync(row, cancellationToken),
                cancellationToken);
        }
    }

    // Reads a page of a closed survey's responses in progress: active since a time when given, and not of the users whose
    // own accommodation keeps the survey open.
    private async Task<List<SurveyResponseIndex>> ReadClosedPageAsync(string surveyId, DateTime? activeSince, string[] stillOpenUsers, SurveyResponseIndex after, int batchSize, CancellationToken cancellationToken)
    {
        var inProgress = nameof(SurveyResponseStatus.InProgress);
        var query = _responseStore.YesSqlSession.QueryIndex<SurveyResponseIndex>(
            index => index.SurveyId == surveyId && index.Status == inProgress && !index.IsTest && index.UpkeepFailures < MaxUpkeepFailures,
            collection: SurveysConstants.ResponsesCollectionName);

        if (activeSince is { } since)
        {
            query = query.Where(index => index.LastActivityUtc >= since);
        }

        if (stillOpenUsers.Length > 0)
        {
            query = query.Where(index => index.UserId == null || index.UserId.IsNotIn(stillOpenUsers));
        }

        if (after is not null)
        {
            var lastId = after.Id;

            query = query.Where(index => index.Id > lastId);
        }

        return (await query
            .OrderBy(index => index.Id)
            .Take(batchSize)
            .ListAsync(cancellationToken)).ToList();
    }

    // Submits one response of a closed survey. One whose respondent's accommodation keeps the survey open (an audience's,
    // which the query can't leave out) is read past without using the step's budget.
    private async Task<UpkeepOutcome> SubmitAtCloseAsync(SurveyResponseIndex row, CancellationToken cancellationToken)
    {
        var stillOpen = false;
        var outcome = await TryAsync("submit after its survey closed", row, async services =>
        {
            var result = await services.GetRequiredService<SurveyResponseService>().SubmitAtCloseAsync(row.ItemId, cancellationToken);

            stillOpen = result.Error == SurveyErrorCode.InvalidState && result.Response?.Status == SurveyResponseStatus.InProgress;

            return Done(result);
        }, cancellationToken);

        return stillOpen ? UpkeepOutcome.Skipped : outcome;
    }

    // Whether a survey closed at or after a time: its closing date passed since then, or staff closed it since then. A
    // survey staff closed before the time, or whose closing time isn't known, did not.
    private static bool ClosedSince(Survey survey, DateTime since, DateTime now)
    {
        if (SurveyAvailability.IsClosedByStatus(survey, collector: null))
        {
            return survey.ClosedUtc is { } closed && closed >= since;
        }

        return survey.Access?.ClosesUtc is { } closes && closes >= since && closes <= now;
    }

    // The users whose own accommodation (one that names them) keeps a survey closed by its date open now. A survey staff
    // closed is closed to everyone, and a list too long for one query is left to the read-past.
    private async Task<string[]> StillOpenUsersAsync(Survey survey, DateTime now, CancellationToken cancellationToken)
    {
        if (SurveyAvailability.IsClosedByStatus(survey, collector: null)
            || survey.Access?.ClosesUtc is null
            || _overrides.FirstOrDefault() is not { } overrides)
        {
            return [];
        }

        var users = (await overrides.ListAsync(survey.ItemId, cancellationToken))
            .Where(item => item is not null && !string.IsNullOrEmpty(item.UserId) && item.ClosesUtc is { } closes && closes > now)
            .Select(item => item.UserId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return users.Length <= MaxLeftOutSurveys ? users : [];
    }

    // Reads when the close step started on a site upgraded from a version without it, or null on a new site.
    private async Task<DateTime?> ReadCloseStepStartAsync(CancellationToken cancellationToken)
    {
        var start = await _session.Query<SurveyCloseStepStart>(SurveysConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);

        return start is null ? null : DateTime.SpecifyKind(start.StartedUtc, DateTimeKind.Utc);
    }

    private Task<int> SubmitOverdueAsync(DateTime now, int batchSize, CancellationToken cancellationToken)
    {
        var inProgress = nameof(SurveyResponseStatus.InProgress);
        var cutoff = now.AddMinutes(-2);

        return WalkAsync(
            batchSize,
            batchSize,
            async (after, leftOut) =>
            {
                var query = LeaveOut(
                    _responseStore.YesSqlSession.QueryIndex<SurveyResponseIndex>(
                        index => index.Status == inProgress && index.DeadlineUtc != null && index.DeadlineUtc <= cutoff && index.UpkeepFailures < MaxUpkeepFailures,
                        collection: SurveysConstants.ResponsesCollectionName),
                    leftOut);

                if (after is not null)
                {
                    var lastDeadline = after.DeadlineUtc;
                    var lastId = after.Id;

                    query = query.Where(index => index.DeadlineUtc > lastDeadline || (index.DeadlineUtc == lastDeadline && index.Id > lastId));
                }

                return (await query
                    .OrderBy(index => index.DeadlineUtc)
                    .ThenBy(index => index.Id)
                    .Take(batchSize)
                    .ListAsync(cancellationToken)).ToList();
            },
            row => TryAsync("submit at its deadline", row, async services => Done(await services.GetRequiredService<SurveyResponseService>().SubmitAtDeadlineAsync(row.ItemId, cancellationToken)), cancellationToken),
            cancellationToken);
    }

    // Deletes old participation records a batch at a time until none are left, the run's time is up, or the batch limit
    // is reached. Each batch commits in its own child scope, and the next one reads past it by index id. A batch that
    // fails is deleted again one record at a time, so one record that can't be deleted (a document that can't be read,
    // say) never stops the cleanup: it is logged and read past. The cleanup stops until the next run only when nothing
    // of a batch could be deleted.
    // The records of a survey with a total limit are kept, so its total counts every response for as long as it runs.
    // When few enough surveys keep records the query leaves them out; past that they are read past in memory, and a run
    // goes on from the index id the last one stopped at (SurveyParticipationCleanupCursor), starting over once it reached
    // the newest record, so the kept records (the oldest ones) can never fill every run and stop the cleanup.
    private async Task DeleteExpiredParticipationAsync(DateTime now, int batchSize, Dictionary<string, Survey> surveys, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var kept = (await SurveyIdsAsync(index => index.TotalLimit != null, SurveyParticipationService.KeepsRecords, surveys, cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var afterId = await ReadParticipationCursorAsync(cancellationToken);
        var reachedEnd = false;

        for (var batch = 0; batch < ParticipationBatchLimit; batch++)
        {
            IReadOnlyList<SurveyParticipationIndex> rows = [];

            try
            {
                await ShellScope.UsingChildScopeAsync(async scope =>
                    rows = await scope.ServiceProvider.GetRequiredService<SurveyParticipationService>().ListExpiredAsync(now, afterId, batchSize, kept, KeptSurveysInQueryLimit, cancellationToken));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Survey upkeep could not read old participation records. It tries again on the next run.");

                return;
            }

            if (rows.Count == 0)
            {
                reachedEnd = true;

                break;
            }

            var ids = rows
                .Where(row => row.SurveyId is null || !kept.Contains(row.SurveyId))
                .Select(row => row.ItemId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (ids.Length > 0 && await DeleteParticipationAsync(ids, cancellationToken) is { } batchFailure)
            {
                var deletedAny = false;

                foreach (var id in ids)
                {
                    if (await DeleteParticipationAsync([id], cancellationToken) is { } failure)
                    {
                        _logger.LogWarning(failure, "Survey upkeep could not delete the old participation record '{ParticipationId}'. It is skipped and tried again on the next run.", id);
                    }
                    else
                    {
                        deletedAny = true;
                    }
                }

                if (!deletedAny)
                {
                    _logger.LogError(batchFailure, "Survey upkeep could not delete old participation records. It tries again on the next run.");

                    // The next run starts with this batch again.
                    break;
                }
            }

            afterId = rows[^1].Id;

            if (rows.Count < batchSize)
            {
                reachedEnd = true;

                break;
            }

            if (Stopwatch.GetElapsedTime(started) >= _participationBudget)
            {
                break;
            }
        }

        await SaveParticipationCursorAsync(reachedEnd ? 0 : afterId, cancellationToken);
    }

    /// <summary>
    /// Gets or sets the most batches of old participation records one run reads. Tests lower it.
    /// </summary>
    internal int ParticipationBatchLimit { get; set; } = MaxParticipationBatches;

    /// <summary>
    /// Gets or sets the most kept surveys the participation query leaves out. Tests lower it.
    /// </summary>
    internal int KeptSurveysInQueryLimit { get; set; } = SurveyParticipationService.MaxKeptSurveysInQuery;

    // Reads where the participation cleanup goes on from, or 0 when the last run reached the end or it can't be read.
    private async Task<long> ReadParticipationCursorAsync(CancellationToken cancellationToken)
    {
        long afterId = 0;

        try
        {
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                var cursor = await scope.ServiceProvider.GetRequiredService<ISession>()
                    .Query<SurveyParticipationCleanupCursor>(SurveysConstants.CollectionName)
                    .FirstOrDefaultAsync(cancellationToken);

                afterId = Math.Max(cursor?.AfterId ?? 0, 0);
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Survey upkeep could not read where the participation cleanup stopped. It starts from the oldest record.");
        }

        return afterId;
    }

    // Stores where the next participation cleanup goes on from, in a child scope of its own.
    private async Task SaveParticipationCursorAsync(long afterId, CancellationToken cancellationToken)
    {
        try
        {
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                var session = scope.ServiceProvider.GetRequiredService<ISession>();
                var cursor = await session.Query<SurveyParticipationCleanupCursor>(SurveysConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);

                if ((cursor is null && afterId == 0) || cursor?.AfterId == afterId)
                {
                    return;
                }

                cursor ??= new SurveyParticipationCleanupCursor();
                cursor.AfterId = afterId;
                await session.SaveAsync(cursor, false, SurveysConstants.CollectionName, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Survey upkeep could not store where the participation cleanup stopped. The next run starts from the oldest record.");
        }
    }

    // Deletes participation records in a child scope of their own. Returns the failure, or null when they were deleted.
    private static async Task<Exception> DeleteParticipationAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        try
        {
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                await scope.ServiceProvider.GetRequiredService<SurveyParticipationService>().DeleteAsync(ids, cancellationToken);
                await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);
            });

            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return exception;
        }
    }

    // Anonymizes one response and removes its files. The response is committed first, so a failed save never loses a
    // file; a file that can't be removed now is left to the orphan sweep, since nothing refers to it any more.
    private static async Task<bool> AnonymizeAsync(IServiceProvider services, string responseId, DateTime now, CancellationToken cancellationToken)
    {
        var responseStore = services.GetRequiredService<SurveyResponseStore>();
        var response = await responseStore.FindByIdAsync(responseId, cancellationToken);

        if (response is null)
        {
            return true;
        }

        Anonymize(response);
        response.AnonymizedUtc = now;
        response.ModifiedUtc = now;

        await responseStore.UpdateAsync(response, cancellationToken);
        await services.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

        // Every file the response owns goes, signatures included: a file's uploader would name the respondent.
        await services.GetRequiredService<SurveyResponseFiles>().DeleteAllAsync(response, cancellationToken);

        return true;
    }

    // Reads a step's responses a page at a time, past the ones it has already seen, and handles them until the step has
    // used its budget, it reached the last page, or it read MaxPagesPerStep pages. Only a response that was handled, or
    // whose failure was counted, uses the budget: a failure from the server's environment leaves its survey out of the
    // rest of the step, and a failure that couldn't be counted is read past, so neither fills the batch and keeps the
    // other surveys' responses waiting. Returns how many responses used the budget.
    private async Task<int> WalkAsync(
        int budget,
        int pageSize,
        Func<SurveyResponseIndex, IReadOnlyCollection<string>, Task<List<SurveyResponseIndex>>> readPageAsync,
        Func<SurveyResponseIndex, Task<UpkeepOutcome>> handleAsync,
        CancellationToken cancellationToken)
    {
        var leftOut = new HashSet<string>(StringComparer.Ordinal);
        SurveyResponseIndex after = null;
        var used = 0;

        for (var page = 0; page < MaxPagesPerStep && used < budget && leftOut.Count < MaxLeftOutSurveys; page++)
        {
            var rows = await readPageAsync(after, leftOut);

            // The run's session only reads: its transaction ends before the child scopes write.
            await EndReadAsync(cancellationToken);

            foreach (var row in rows)
            {
                if (used >= budget)
                {
                    break;
                }

                after = row;

                if (row.SurveyId is not null && leftOut.Contains(row.SurveyId))
                {
                    continue;
                }

                var outcome = await handleAsync(row);

                if (outcome == UpkeepOutcome.Handled)
                {
                    used++;
                }
                else if (outcome == UpkeepOutcome.LeaveSurvey && row.SurveyId is not null)
                {
                    leftOut.Add(row.SurveyId);
                }
            }

            if (rows.Count < pageSize)
            {
                break;
            }
        }

        return used;
    }

    // Leaves out the responses of the surveys a step left out.
    private static IQueryIndex<SurveyResponseIndex> LeaveOut(IQueryIndex<SurveyResponseIndex> query, IReadOnlyCollection<string> leftOut)
    {
        if (leftOut.Count == 0)
        {
            return query;
        }

        var surveyIds = leftOut.ToArray();

        return query.Where(index => index.SurveyId == null || index.SurveyId.IsNotIn(surveyIds));
    }

    // Runs one upkeep step of a response in a child scope whose session commits on its own. The step returns false when
    // the response was busy (its lock was held, or a concurrency check refused the save): nothing of it was committed and
    // none of its side effects ran. A failure, or a busy response, is logged and counted on the response in another child
    // scope, unless it comes from the server's environment; after MaxUpkeepFailures in a row, upkeep stops trying it. A
    // step that succeeds clears the count.
    private async Task<UpkeepOutcome> TryAsync(string what, SurveyResponseIndex row, Func<IServiceProvider, Task<bool>> work, CancellationToken cancellationToken)
    {
        Exception failure = null;
        var done = false;

        try
        {
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                done = await work(scope.ServiceProvider);

                if (done)
                {
                    // Writes the step's observers made after its commit (the audit trail, for one).
                    await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);
                }
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            failure = exception;
            done = false;
        }

        if (done)
        {
            if (row.UpkeepFailures > 0)
            {
                await SetFailuresAsync(row.ItemId, clear: true, cancellationToken);
            }

            return UpkeepOutcome.Handled;
        }

        if (failure is not null && IsEnvironmental(failure))
        {
            _logger.LogWarning(failure, "Survey upkeep could not {Action} the response '{ResponseId}' because of a problem on this server. It and the other responses of its survey are tried again on the next run.", what, row.ItemId);

            return UpkeepOutcome.LeaveSurvey;
        }

        if (await SetFailuresAsync(row.ItemId, clear: false, cancellationToken) is not { } failures)
        {
            _logger.LogError(failure, "Survey upkeep could not {Action} the response '{ResponseId}', nor count the failure on it. It is skipped and tried again on the next run.", what, row.ItemId);

            return UpkeepOutcome.NotCounted;
        }

        if (failures >= MaxUpkeepFailures)
        {
            _logger.LogError(failure, "Survey upkeep could not {Action} the response '{ResponseId}'. It failed {Failures} times in a row and is no longer tried until an admin retries it.", what, row.ItemId, failures);
        }
        else if (failure is null)
        {
            _logger.LogWarning("Survey upkeep could not {Action} the response '{ResponseId}' because it was being changed. It is tried again on the next run.", what, row.ItemId);
        }
        else
        {
            _logger.LogError(failure, "Survey upkeep could not {Action} the response '{ResponseId}'. It is skipped and tried again on the next run.", what, row.ItemId);
        }

        return UpkeepOutcome.Handled;
    }

    // Counts a failure on a response, or clears its count, in a child scope of its own. Returns the new count, or null
    // when the response could not take the write (it is then left as it is).
    private async Task<int?> SetFailuresAsync(string responseId, bool clear, CancellationToken cancellationToken)
    {
        int? failures = null;

        try
        {
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                var responses = scope.ServiceProvider.GetRequiredService<SurveyResponseService>();
                var result = clear
                    ? await responses.ClearUpkeepFailuresAsync(responseId, cancellationToken)
                    : await responses.CountUpkeepFailureAsync(responseId, cancellationToken);

                failures = result.Error == SurveyErrorCode.None && result.Response is not null ? result.Response.UpkeepFailures : null;
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            failures = null;
            _logger.LogWarning(exception, "Survey upkeep could not update the failure count of the response '{ResponseId}'.", responseId);
        }

        return failures;
    }

    // Ends the run's own read transaction before the child scopes write, so its connection never holds a lock they wait
    // on. The run's session only reads.
    private Task EndReadAsync(CancellationToken cancellationToken)
        => _session.SaveChangesAsync(cancellationToken);

    private static bool Done(SurveyResponseResult result)
        => result.Error != SurveyErrorCode.Busy;

    // The ids of the surveys a step needs, found by the survey index. A row indexed before the upkeep columns existed (its
    // retention days are empty) is decided from its survey until SurveyUpkeepReindexStep reaches it.
    private async Task<IReadOnlyList<string>> SurveyIdsAsync(
        Expression<Func<SurveyIndex, bool>> indexed,
        Func<Survey, bool> unindexed,
        Dictionary<string, Survey> surveys,
        CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in await _session.QueryIndex(indexed, collection: SurveysConstants.CollectionName).OrderBy(index => index.Id).ListAsync(cancellationToken))
        {
            if (!string.IsNullOrEmpty(row.ItemId) && seen.Add(row.ItemId))
            {
                ids.Add(row.ItemId);
            }
        }

        foreach (var row in await _session.QueryIndex<SurveyIndex>(index => index.RetentionDays == null, collection: SurveysConstants.CollectionName).OrderBy(index => index.Id).ListAsync(cancellationToken))
        {
            if (!string.IsNullOrEmpty(row.ItemId)
                && !seen.Contains(row.ItemId)
                && await SurveyAsync(row.ItemId, surveys, cancellationToken) is { } survey
                && unindexed(survey)
                && seen.Add(row.ItemId))
            {
                ids.Add(row.ItemId);
            }
        }

        return ids;
    }

    private async Task<Survey> SurveyAsync(string surveyId, Dictionary<string, Survey> surveys, CancellationToken cancellationToken)
    {
        if (!surveys.TryGetValue(surveyId, out var survey))
        {
            survey = await _surveyStore.FindByIdAsync(surveyId, cancellationToken);
            surveys[surveyId] = survey;
        }

        return survey;
    }
}
