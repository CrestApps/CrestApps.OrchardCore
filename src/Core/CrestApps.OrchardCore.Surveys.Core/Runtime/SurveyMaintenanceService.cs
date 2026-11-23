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

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyMaintenanceService"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="responseStore">The response store.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public SurveyMaintenanceService(
        SurveyStore surveyStore,
        SurveyResponseStore responseStore,
        ISession session,
        IClock clock,
        ILogger<SurveyMaintenanceService> logger)
    {
        _surveyStore = surveyStore;
        _responseStore = responseStore;
        _session = session;
        _clock = clock;
        _logger = logger;
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
    private async Task SubmitClosedAsync(DateTime now, int batchSize, Dictionary<string, Survey> surveys, CancellationToken cancellationToken)
    {
        var inProgress = nameof(SurveyResponseStatus.InProgress);
        var closed = nameof(SurveyStatus.Closed);
        var archived = nameof(SurveyStatus.Archived);
        var handled = 0;
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

            handled += await WalkAsync(
                batchSize - handled,
                batchSize,
                async (after, leftOut) =>
                {
                    var query = _responseStore.YesSqlSession.QueryIndex<SurveyResponseIndex>(
                        index => index.SurveyId == surveyId && index.Status == inProgress && !index.IsTest && index.UpkeepFailures < MaxUpkeepFailures,
                        collection: SurveysConstants.ResponsesCollectionName);

                    if (after is not null)
                    {
                        var lastId = after.Id;

                        query = query.Where(index => index.Id > lastId);
                    }

                    return (await query
                        .OrderBy(index => index.Id)
                        .Take(batchSize)
                        .ListAsync(cancellationToken)).ToList();
                },
                row => TryAsync("submit after its survey closed", row, async services => Done(await services.GetRequiredService<SurveyResponseService>().SubmitAtCloseAsync(row.ItemId, cancellationToken)), cancellationToken),
                cancellationToken);
        }
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
    private async Task DeleteExpiredParticipationAsync(DateTime now, int batchSize, Dictionary<string, Survey> surveys, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        long afterId = 0;
        var kept = (await SurveyIdsAsync(index => index.TotalLimit != null, SurveyParticipationService.KeepsRecords, surveys, cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        for (var batch = 0; batch < MaxParticipationBatches; batch++)
        {
            IReadOnlyList<SurveyParticipationIndex> rows = [];

            try
            {
                await ShellScope.UsingChildScopeAsync(async scope =>
                    rows = await scope.ServiceProvider.GetRequiredService<SurveyParticipationService>().ListExpiredAsync(now, afterId, batchSize, kept, cancellationToken));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Survey upkeep could not read old participation records. It tries again on the next run.");

                return;
            }

            if (rows.Count == 0)
            {
                return;
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

                    return;
                }
            }

            afterId = rows[^1].Id;

            if (rows.Count < batchSize || Stopwatch.GetElapsedTime(started) >= _participationBudget)
            {
                return;
            }
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
