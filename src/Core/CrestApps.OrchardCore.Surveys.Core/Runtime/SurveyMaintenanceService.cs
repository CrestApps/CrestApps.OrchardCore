using System.Data.Common;
using System.Diagnostics;
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

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Upkeep of responses, a batch at a time: abandons responses idle past their survey's expiry, applies retention
/// (delete or anonymize), deletes old participation records, and submits responses whose deadline was missed.
/// Each response is handled in its own child shell scope, with a session of its own that commits on its own: a
/// response that fails, even when its save fails, is logged and skipped, and never takes the other responses' changes
/// with it. After <see cref="MaxUpkeepFailures"/> failures in a row it is no longer tried, so failing responses can't
/// fill every batch; a step that succeeds clears the count, and an admin can clear it to have upkeep try again.
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

    /// <summary>
    /// Runs one round of upkeep. It must run in a shell scope: each response is handled in a child scope of it.
    /// </summary>
    /// <param name="batchSize">The most records each step handles.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task RunAsync(int batchSize = SurveysConstants.PageSize, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var surveys = new Dictionary<string, Survey>(StringComparer.Ordinal);

        await AbandonIdleAsync(now, batchSize, surveys, cancellationToken);
        await ApplyRetentionAsync(now, batchSize, surveys, cancellationToken);
        await SubmitOverdueAsync(now, batchSize, cancellationToken);
        await DeleteExpiredParticipationAsync(now, batchSize, cancellationToken);
    }

    /// <summary>
    /// Gets whether an upkeep failure comes from the server's environment rather than from the response, such as an
    /// anonymity secret this server can't read or a transient database error. Such a failure is logged but not counted
    /// on the response, so a problem with the server never stops upkeep of a response for good.
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

    private async Task AbandonIdleAsync(DateTime now, int batchSize, Dictionary<string, Survey> surveys, CancellationToken cancellationToken)
    {
        var inProgress = nameof(SurveyResponseStatus.InProgress);

        // The shortest expiry a survey can have is one hour: anything more recent cannot be idle yet.
        var threshold = now.AddHours(-1);
        var candidates = await _responseStore.YesSqlSession.QueryIndex<SurveyResponseIndex>(
                index => index.Status == inProgress && index.LastActivityUtc < threshold && index.UpkeepFailures < MaxUpkeepFailures,
                collection: SurveysConstants.ResponsesCollectionName)
            .OrderBy(index => index.LastActivityUtc)
            .Take(batchSize)
            .ListAsync(cancellationToken);

        var idle = new List<(SurveyResponseIndex Row, DateTime IdleBefore)>();

        foreach (var candidate in candidates)
        {
            var survey = await SurveyAsync(candidate.SurveyId, surveys, cancellationToken);
            var hours = Math.Max(survey?.Access?.IncompleteExpiryHours ?? 168, 1);
            var idleBefore = now.AddHours(-hours);

            if (candidate.LastActivityUtc < idleBefore && candidate.DeadlineUtc is null)
            {
                idle.Add((candidate, idleBefore));
            }
        }

        await EndReadAsync(cancellationToken);

        foreach (var (row, idleBefore) in idle)
        {
            await TryAsync("abandon", row, async services => Done(await services.GetRequiredService<SurveyResponseService>().AbandonAsync(row.ItemId, idleBefore, cancellationToken)), cancellationToken);
        }
    }

    private async Task ApplyRetentionAsync(DateTime now, int batchSize, Dictionary<string, Survey> surveys, CancellationToken cancellationToken)
    {
        var withRetention = await _session.Query<Survey, SurveyIndex>(collection: SurveysConstants.CollectionName).ListAsync(cancellationToken);
        var inProgress = nameof(SurveyResponseStatus.InProgress);
        var handled = 0;

        foreach (var survey in withRetention)
        {
            surveys[survey.ItemId] = survey;

            if (survey.Retention?.Days is not { } days || days <= 0 || handled >= batchSize)
            {
                continue;
            }

            var before = now.AddDays(-days);
            var surveyId = survey.ItemId;
            var anonymize = survey.Retention.Action == RetentionAction.Anonymize;

            // An anonymized response stays, so its index column leaves it out; otherwise the oldest anonymized responses
            // would fill every batch and the step would never reach the rest.
            var query = _responseStore.YesSqlSession.QueryIndex<SurveyResponseIndex>(
                index => index.SurveyId == surveyId && index.Status != inProgress && index.LastActivityUtc < before && index.UpkeepFailures < MaxUpkeepFailures,
                collection: SurveysConstants.ResponsesCollectionName);

            if (anonymize)
            {
                query = query.Where(index => !index.Anonymized);
            }

            var old = await query
                .OrderBy(index => index.LastActivityUtc)
                .Take(batchSize - handled)
                .ListAsync(cancellationToken);

            await EndReadAsync(cancellationToken);

            foreach (var row in old)
            {
                handled++;

                if (anonymize)
                {
                    await TryAsync("anonymize", row, services => AnonymizeAsync(services, row.ItemId, now, cancellationToken), cancellationToken);
                }
                else
                {
                    await TryAsync("delete", row, async services => Done(await services.GetRequiredService<SurveyResponseService>().DeleteAsync(row.ItemId, actorId: null, cancellationToken)), cancellationToken);
                }
            }
        }
    }

    private async Task SubmitOverdueAsync(DateTime now, int batchSize, CancellationToken cancellationToken)
    {
        var inProgress = nameof(SurveyResponseStatus.InProgress);
        var cutoff = now.AddMinutes(-2);
        var overdue = await _responseStore.YesSqlSession.QueryIndex<SurveyResponseIndex>(
                index => index.Status == inProgress && index.DeadlineUtc != null && index.DeadlineUtc <= cutoff && index.UpkeepFailures < MaxUpkeepFailures,
                collection: SurveysConstants.ResponsesCollectionName)
            .OrderBy(index => index.DeadlineUtc)
            .Take(batchSize)
            .ListAsync(cancellationToken);

        await EndReadAsync(cancellationToken);

        foreach (var response in overdue)
        {
            await TryAsync("submit at its deadline", response, async services => Done(await services.GetRequiredService<SurveyResponseService>().SubmitAtDeadlineAsync(response.ItemId, cancellationToken)), cancellationToken);
        }
    }

    // Deletes old participation records a batch at a time until none are left, the run's time is up, or the batch limit
    // is reached. Each batch commits in its own child scope, so the next one reads past it; a batch that fails ends the
    // cleanup until the next run.
    private async Task DeleteExpiredParticipationAsync(DateTime now, int batchSize, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();

        for (var batch = 0; batch < MaxParticipationBatches; batch++)
        {
            var deleted = 0;

            try
            {
                await ShellScope.UsingChildScopeAsync(async scope =>
                {
                    deleted = await scope.ServiceProvider.GetRequiredService<SurveyParticipationService>().DeleteExpiredAsync(now, batchSize, cancellationToken);
                    await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);
                });
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Survey upkeep could not delete old participation records. It tries again on the next run.");

                return;
            }

            if (deleted < batchSize || Stopwatch.GetElapsedTime(started) >= _participationBudget)
            {
                return;
            }
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

    // Runs one upkeep step of a response in a child scope whose session commits on its own. The step returns false when
    // the response was busy (its lock was held, or a concurrency check refused the save): nothing of it was committed and
    // none of its side effects ran. A failure, or a busy response, is logged and counted on the response in another child
    // scope, unless it comes from the server's environment; after MaxUpkeepFailures in a row, upkeep stops trying it. A
    // step that succeeds clears the count.
    private async Task TryAsync(string what, SurveyResponseIndex row, Func<IServiceProvider, Task<bool>> work, CancellationToken cancellationToken)
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

            return;
        }

        if (failure is not null && IsEnvironmental(failure))
        {
            _logger.LogWarning(failure, "Survey upkeep could not {Action} the response '{ResponseId}' because of a problem on this server. It is tried again on the next run.", what, row.ItemId);

            return;
        }

        var failures = await SetFailuresAsync(row.ItemId, clear: false, cancellationToken);

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
    }

    // Counts a failure on a response, or clears its count, in a child scope of its own. Returns the new count, or 0 when
    // the response could not take the write (it is then left as it is).
    private async Task<int> SetFailuresAsync(string responseId, bool clear, CancellationToken cancellationToken)
    {
        var failures = 0;

        try
        {
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                var responses = scope.ServiceProvider.GetRequiredService<SurveyResponseService>();
                var result = clear
                    ? await responses.ClearUpkeepFailuresAsync(responseId, cancellationToken)
                    : await responses.CountUpkeepFailureAsync(responseId, cancellationToken);

                failures = result.Error == SurveyErrorCode.None ? result.Response?.UpkeepFailures ?? 0 : 0;
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
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
