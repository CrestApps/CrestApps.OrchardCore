using System.Diagnostics;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Upkeep of responses, a batch at a time: abandons responses idle past their survey's expiry, applies retention
/// (delete or anonymize), deletes old participation records, and submits responses whose deadline was missed. A
/// response that fails is logged and skipped, its writes rolled back, so one bad response never blocks the rest; after
/// <see cref="MaxUpkeepFailures"/> failures in a row it is no longer tried, so failing responses can't fill every batch.
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
    private readonly SurveyResponseService _responses;
    private readonly SurveyParticipationService _participation;
    private readonly SurveyResponseFiles _files;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyMaintenanceService"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="responseStore">The response store.</param>
    /// <param name="responses">The response service.</param>
    /// <param name="participation">The participation service.</param>
    /// <param name="files">The response files.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public SurveyMaintenanceService(
        SurveyStore surveyStore,
        SurveyResponseStore responseStore,
        SurveyResponseService responses,
        SurveyParticipationService participation,
        SurveyResponseFiles files,
        ISession session,
        IClock clock,
        ILogger<SurveyMaintenanceService> logger)
    {
        _surveyStore = surveyStore;
        _responseStore = responseStore;
        _responses = responses;
        _participation = participation;
        _files = files;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Runs one round of upkeep.
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

        foreach (var candidate in candidates)
        {
            var survey = await SurveyAsync(candidate.SurveyId, surveys, cancellationToken);
            var hours = Math.Max(survey?.Access?.IncompleteExpiryHours ?? 168, 1);
            var idleBefore = now.AddHours(-hours);

            if (candidate.LastActivityUtc < idleBefore && candidate.DeadlineUtc is null)
            {
                await TryAsync("abandon", candidate.ItemId, () => _responses.AbandonAsync(candidate.ItemId, idleBefore, cancellationToken), cancellationToken);
            }
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

            // Index rows, not documents: each response is saved on its own, and a save forgets the documents the session
            // read before it, so each one is read again by id.
            foreach (var row in old)
            {
                handled++;

                if (anonymize)
                {
                    await TryAsync("anonymize", row.ItemId, () => AnonymizeAsync(row.ItemId, now, cancellationToken), cancellationToken);
                }
                else
                {
                    await TryAsync("delete", row.ItemId, () => _responses.DeleteAsync(row.ItemId, actorId: null, cancellationToken), cancellationToken);
                }
            }
        }

        await _session.SaveChangesAsync(cancellationToken);
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

        foreach (var response in overdue)
        {
            await TryAsync("submit at its deadline", response.ItemId, () => _responses.SubmitAtDeadlineAsync(response.ItemId, cancellationToken), cancellationToken);
        }
    }

    // Deletes old participation records a batch at a time until none are left, the run's time is up, or the batch limit
    // is reached. Each batch is saved, so the next one reads past it.
    private async Task DeleteExpiredParticipationAsync(DateTime now, int batchSize, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();

        for (var batch = 0; batch < MaxParticipationBatches; batch++)
        {
            var deleted = await _participation.DeleteExpiredAsync(now, batchSize, cancellationToken);

            await _session.SaveChangesAsync(cancellationToken);

            if (deleted < batchSize || Stopwatch.GetElapsedTime(started) >= _participationBudget)
            {
                return;
            }
        }
    }

    // Anonymizes one response and removes its files. The response is saved first, so a failed save never loses a file;
    // a file that can't be removed now is left to the orphan sweep, since nothing refers to it any more.
    private async Task AnonymizeAsync(string responseId, DateTime now, CancellationToken cancellationToken)
    {
        var response = await _responseStore.FindByIdAsync(responseId, cancellationToken);

        if (response is null)
        {
            return;
        }

        Anonymize(response);
        response.AnonymizedUtc = now;
        response.ModifiedUtc = now;

        // Saved right away, so a later response that fails and is rolled back can't take this change with it.
        await _responseStore.UpdateAsync(response, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        // Every file the response owns goes, signatures included: a file's uploader would name the respondent.
        await _files.DeleteAllAsync(response, cancellationToken);
    }

    // Runs the upkeep of one response. A failure is logged, its pending writes are rolled back so the session stays
    // usable and the next response is still handled, and it is counted on the response: after MaxUpkeepFailures in a
    // row, upkeep stops trying it.
    private async Task TryAsync(string what, string responseId, Func<Task> work, CancellationToken cancellationToken)
    {
        try
        {
            await work();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await _session.ResetAsync();

            var failures = await CountFailureAsync(responseId, cancellationToken);

            if (failures >= MaxUpkeepFailures)
            {
                _logger.LogError(exception, "Survey upkeep could not {Action} the response '{ResponseId}'. It failed {Failures} times in a row and is no longer tried.", what, responseId, failures);
            }
            else
            {
                _logger.LogError(exception, "Survey upkeep could not {Action} the response '{ResponseId}'. It is skipped and tried again on the next run.", what, responseId);
            }
        }
    }

    // Counts a failed upkeep run on a response. A response that can't even take this write is left as it is.
    private async Task<int> CountFailureAsync(string responseId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _responseStore.FindByIdAsync(responseId, cancellationToken);

            if (response is null)
            {
                return 0;
            }

            response.UpkeepFailures++;
            await _responseStore.UpdateAsync(response, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return response.UpkeepFailures;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Survey upkeep could not count a failure on the response '{ResponseId}'.", responseId);
            await _session.ResetAsync();

            return 0;
        }
    }

    /// <summary>
    /// Removes who responded from a response: user, respondent, IP address and invitation, and the answers' references
    /// to files (the caller removes the files themselves, whose uploader names the respondent).
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns><see langword="true"/> when something was removed.</returns>
    public static bool Anonymize(SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var changed = response.UserId is not null || response.Respondent is not null || response.IpAddress is not null || response.InvitationId is not null;

        response.UserId = null;
        response.Respondent = null;
        response.IpAddress = null;
        response.InvitationId = null;

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
