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
/// response that fails is logged and skipped, its writes rolled back, so one bad response never blocks the rest.
/// </summary>
public sealed class SurveyMaintenanceService
{
    private readonly SurveyStore _surveyStore;
    private readonly SurveyResponseStore _responseStore;
    private readonly SurveyResponseService _responses;
    private readonly SurveyParticipationService _participation;
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
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public SurveyMaintenanceService(
        SurveyStore surveyStore,
        SurveyResponseStore responseStore,
        SurveyResponseService responses,
        SurveyParticipationService participation,
        ISession session,
        IClock clock,
        ILogger<SurveyMaintenanceService> logger)
    {
        _surveyStore = surveyStore;
        _responseStore = responseStore;
        _responses = responses;
        _participation = participation;
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

        await _participation.DeleteExpiredAsync(now, batchSize, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }

    private async Task AbandonIdleAsync(DateTime now, int batchSize, Dictionary<string, Survey> surveys, CancellationToken cancellationToken)
    {
        var inProgress = nameof(SurveyResponseStatus.InProgress);

        // The shortest expiry a survey can have is one hour: anything more recent cannot be idle yet.
        var threshold = now.AddHours(-1);
        var candidates = await _responseStore.YesSqlSession.QueryIndex<SurveyResponseIndex>(
                index => index.Status == inProgress && index.LastActivityUtc < threshold,
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
                await TryAsync("abandon", candidate.ItemId, () => _responses.AbandonAsync(candidate.ItemId, idleBefore, cancellationToken));
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
            var old = await _responseStore.YesSqlSession.Query<SurveyResponse, SurveyResponseIndex>(
                    index => index.SurveyId == surveyId && index.Status != inProgress && index.LastActivityUtc < before,
                    collection: SurveysConstants.ResponsesCollectionName)
                .OrderBy(index => index.LastActivityUtc)
                .Take(batchSize - handled)
                .ListAsync(cancellationToken);

            foreach (var response in old)
            {
                handled++;

                if (survey.Retention.Action == RetentionAction.Anonymize)
                {
                    if (!Anonymize(response))
                    {
                        continue;
                    }

                    response.ModifiedUtc = now;

                    // Saved right away, so a later response that fails and is rolled back can't take this change with it.
                    await _responseStore.UpdateAsync(response, cancellationToken);
                    await _session.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    await TryAsync("delete", response.ItemId, () => _responses.DeleteAsync(response.ItemId, actorId: null, cancellationToken));
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
                index => index.Status == inProgress && index.DeadlineUtc != null && index.DeadlineUtc <= cutoff,
                collection: SurveysConstants.ResponsesCollectionName)
            .OrderBy(index => index.DeadlineUtc)
            .Take(batchSize)
            .ListAsync(cancellationToken);

        foreach (var response in overdue)
        {
            await TryAsync("submit at its deadline", response.ItemId, () => _responses.SubmitAtDeadlineAsync(response.ItemId, cancellationToken));
        }
    }

    // Runs the upkeep of one response. A failure is logged and its pending writes are rolled back, so the session stays
    // usable and the next response is still handled.
    private async Task TryAsync(string what, string responseId, Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Survey upkeep could not {Action} the response '{ResponseId}'. It is skipped and tried again on the next run.", what, responseId);

            await _session.ResetAsync();
        }
    }

    /// <summary>
    /// Removes who responded from a response: user, respondent, IP address and invitation.
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
