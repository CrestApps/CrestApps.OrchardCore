using CrestApps.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Core.Scoring;

/// <summary>
/// Grades, regrades and changes the scores of responses. Every change is kept in the response's score history, and
/// observers hear about it once it is saved.
/// </summary>
public sealed class SurveyGradingService : ISurveyGradingService
{
    private readonly ICatalog<SurveyResponse> _responses;
    private readonly ICatalog<Survey> _surveys;
    private readonly ISurveyVersionCache _versionCache;
    private readonly ISurveyScoringEngine _scoringEngine;
    private readonly ISurveyResponseQueries _queries;
    private readonly ISurveyResponseCommitter _committer;
    private readonly ISurveyScoringEventDispatcher _dispatcher;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyGradingService"/> class.
    /// </summary>
    /// <param name="responses">The response store.</param>
    /// <param name="surveys">The survey store.</param>
    /// <param name="versionCache">The version cache.</param>
    /// <param name="scoringEngine">The scoring engine.</param>
    /// <param name="queries">The response queries.</param>
    /// <param name="committer">Saves the changes.</param>
    /// <param name="dispatcher">Tells observers.</param>
    /// <param name="clock">The clock.</param>
    public SurveyGradingService(
        ICatalog<SurveyResponse> responses,
        ICatalog<Survey> surveys,
        ISurveyVersionCache versionCache,
        ISurveyScoringEngine scoringEngine,
        ISurveyResponseQueries queries,
        ISurveyResponseCommitter committer,
        ISurveyScoringEventDispatcher dispatcher,
        IClock clock)
    {
        _responses = responses;
        _surveys = surveys;
        _versionCache = versionCache;
        _scoringEngine = scoringEngine;
        _queries = queries;
        _committer = committer;
        _dispatcher = dispatcher;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<SurveyResponseResult> GradeAsync(
        string responseId,
        IReadOnlyDictionary<string, decimal> scores,
        IReadOnlyDictionary<string, string> comments,
        string feedback,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        var (response, survey, version, definition, error) = await LoadAsync(responseId, cancellationToken);

        if (error != SurveyErrorCode.None)
        {
            return Fail(error, response);
        }

        if (response.Status != SurveyResponseStatus.Completed || definition.Scoring?.Enabled != true)
        {
            return Fail(SurveyErrorCode.InvalidState, response);
        }

        var questions = definition.Pages.SelectMany(page => page.Questions).Where(question => !string.IsNullOrEmpty(question?.Id)).ToDictionary(question => question.Id, StringComparer.Ordinal);
        var now = _clock.UtcNow;
        var changes = new List<ScoreChange>();
        var wasGrading = response.Grading;
        var before = response.Score;

        // Score first, so the most points of each question are known before grader scores are checked.
        _scoringEngine.Score(definition, response);

        foreach (var (questionId, points) in scores ?? new Dictionary<string, decimal>())
        {
            if (!questions.TryGetValue(questionId, out var question))
            {
                return Fail(SurveyErrorCode.InvalidAnswer, response);
            }

            response.Answers.TryGetValue(questionId, out var answer);

            if (answer is null)
            {
                answer = new SurveyAnswer { AnsweredUtc = now };
                response.Answers[questionId] = answer;
            }

            var max = answer.MaxPoints ?? question.Scoring?.Points ?? definition.Scoring.DefaultPoints;

            if (points < 0 || points > max)
            {
                return Fail(SurveyErrorCode.InvalidAnswer, response);
            }

            // Only an answer that waits for a grader, or whose points the grader changed, takes a grader's score. Posted
            // points equal to the automatic score stay automatic, so a later "Score again" after a key fix still moves them.
            if (answer.ManualScore is null && answer.AutoScore is not null && answer.Score == points)
            {
                continue;
            }

            if (answer.Score != points)
            {
                changes.Add(new ScoreChange
                {
                    ChangedUtc = now,
                    Actor = actorId,
                    Kind = ScoreChangeKind.Graded,
                    From = answer.Score,
                    To = points,
                    QuestionId = questionId,
                });
            }

            answer.ManualScore = points;
        }

        foreach (var (questionId, comment) in comments ?? new Dictionary<string, string>())
        {
            if (response.Answers.TryGetValue(questionId, out var answer) && answer is not null)
            {
                answer.GraderComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            }
        }

        if (feedback is not null)
        {
            response.Feedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        }

        response.GradedBy = actorId;
        response.GradedUtc = now;
        _scoringEngine.Score(definition, response);

        return await SaveAsync(response, survey, version, changes, wasGrading, before, actorId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SurveyResponseResult> RecordScoringHelpAsync(string responseId, string questionId, CancellationToken cancellationToken = default)
    {
        var response = string.IsNullOrEmpty(responseId) ? null : await _responses.FindByIdAsync(responseId, cancellationToken);

        if (response is null)
        {
            return Fail(SurveyErrorCode.NotFound, null);
        }

        if (string.IsNullOrEmpty(questionId) || !response.Answers.TryGetValue(questionId, out var answer) || answer is null)
        {
            return Fail(SurveyErrorCode.InvalidAnswer, response);
        }

        answer.AIAssisted = true;
        response.ModifiedUtc = _clock.UtcNow;
        await _responses.UpdateAsync(response, cancellationToken);

        return await _committer.CommitAsync(cancellationToken)
            ? new SurveyResponseResult { Response = response }
            : Fail(SurveyErrorCode.Busy, response);
    }

    /// <inheritdoc />
    public async Task<SurveyResponseResult> RegradeAsync(string responseId, string actorId, string reason, CancellationToken cancellationToken = default)
    {
        var (response, survey, version, definition, error) = await LoadAsync(responseId, cancellationToken);

        if (error != SurveyErrorCode.None)
        {
            return Fail(error, response);
        }

        if (response.Status != SurveyResponseStatus.Completed)
        {
            return Fail(SurveyErrorCode.InvalidState, response);
        }

        var before = response.Score;
        var wasGrading = response.Grading;

        _scoringEngine.Score(definition, response);

        var changes = new List<ScoreChange>
        {
            new()
            {
                ChangedUtc = _clock.UtcNow,
                Actor = actorId,
                Kind = ScoreChangeKind.Regraded,
                From = before,
                To = response.Score,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            },
        };

        return await SaveAsync(response, survey, version, changes, wasGrading, before, actorId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SurveyRegradeResult> RegradeVersionsAsync(
        string surveyId,
        IReadOnlyCollection<string> fromVersionIds,
        string keyVersionId,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var result = new SurveyRegradeResult();

        if (string.IsNullOrEmpty(surveyId) || fromVersionIds is null || fromVersionIds.Count == 0 || string.IsNullOrWhiteSpace(reason))
        {
            return result;
        }

        var keyVersion = await _versionCache.GetAsync(keyVersionId, cancellationToken);
        var survey = await _surveys.FindByIdAsync(surveyId, cancellationToken);

        if (keyVersion is null || survey is null || keyVersion.SurveyId != surveyId)
        {
            return result;
        }

        await _scoringEngine.PrepareAsync();

        var count = 0;

        foreach (var versionId in fromVersionIds.Distinct(StringComparer.Ordinal))
        {
            var version = await _versionCache.GetAsync(versionId, cancellationToken);

            if (version is null || version.SurveyId != surveyId)
            {
                continue;
            }

            var (definition, kept) = SurveyScoringKeys.WithKeyOf(version.Definition, keyVersion.Definition);

            foreach (var question in kept)
            {
                result.SkippedQuestions.Add(new SurveyRegradeSkippedQuestion
                {
                    VersionNumber = version.Number,
                    QuestionId = question.Id,
                    Title = string.IsNullOrWhiteSpace(question.Title) ? question.Name : question.Title,
                });
            }

            var skip = 0;

            while (true)
            {
                var (_, page) = await _queries.ListAsync(new SurveyResponseQuery
                {
                    SurveyIds = [surveyId],
                    VersionNumber = version.Number,
                    Statuses = [SurveyResponseStatus.Completed],

                    // By response id, which a save never changes (each page's saves write the index rows again under new
                    // ids, and start times can tie), so the pages neither skip nor repeat a response.
                    OrderById = true,
                    Skip = skip,
                    Take = SurveysConstants.PageSize,
                }, cancellationToken);

                if (page.Count == 0)
                {
                    break;
                }

                foreach (var item in page)
                {
                    var response = await _responses.FindByIdAsync(item.ItemId, cancellationToken) ?? item;
                    var before = response.Score;
                    var wasGrading = response.Grading;

                    ScoreKeeping(definition, response, kept);

                    // Every later scoring of the response (grading a pending attempt, Score again, finishing it again)
                    // scores with this key, so none of them brings the old key back.
                    response.ScoredWithVersionId = string.Equals(keyVersion.ItemId, version.ItemId, StringComparison.Ordinal) ? null : keyVersion.ItemId;

                    var change = new ScoreChange
                    {
                        ChangedUtc = _clock.UtcNow,
                        Actor = actorId,
                        Kind = ScoreChangeKind.Regraded,
                        From = before,
                        To = response.Score,
                        Reason = $"{reason.Trim()} (key of version {keyVersion.Number})",
                    };

                    response.ScoreHistory ??= [];
                    response.ScoreHistory.Add(change);
                    response.ModifiedUtc = _clock.UtcNow;

                    await _responses.UpdateAsync(response, cancellationToken);
                    Notify(survey, version, response, wasGrading, before, change, actorId);
                    count++;
                }

                if (!await _committer.CommitAsync(cancellationToken))
                {
                    result.Count = count - page.Count;

                    return result;
                }

                skip += page.Count;
            }
        }

        result.Count = count;

        return result;
    }

    /// <inheritdoc />
    public async Task<SurveyResponseResult> ChangeScoreAsync(string responseId, decimal score, string actorId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Fail(SurveyErrorCode.InvalidAnswer, null);
        }

        var (response, survey, version, definition, error) = await LoadAsync(responseId, cancellationToken);

        if (error != SurveyErrorCode.None)
        {
            return Fail(error, response);
        }

        if (response.Status != SurveyResponseStatus.Completed || response.MaxScore is not { } max || response.Grading == GradingStatus.NotScored)
        {
            return Fail(SurveyErrorCode.InvalidState, response);
        }

        if (score < 0 || score > max)
        {
            return Fail(SurveyErrorCode.InvalidAnswer, response);
        }

        var before = response.Score;
        var wasGrading = response.Grading;
        var decimals = Math.Clamp(definition.Scoring?.RoundingDecimals ?? 2, 0, 6);

        response.Score = Math.Round(score, decimals, MidpointRounding.AwayFromZero);
        response.Percent = max > 0 ? Math.Round(score / max * 100m, decimals, MidpointRounding.AwayFromZero) : null;
        response.Grading = GradingStatus.Graded;
        SurveyScoringEngine.ApplyResult(definition, response);

        var change = new ScoreChange
        {
            ChangedUtc = _clock.UtcNow,
            Actor = actorId,
            Kind = ScoreChangeKind.Changed,
            From = before,
            To = response.Score,
            Reason = reason.Trim(),
        };

        response.ScoreHistory ??= [];
        response.ScoreHistory.Add(change);
        response.ModifiedUtc = _clock.UtcNow;

        await _responses.UpdateAsync(response, cancellationToken);

        if (!await _committer.CommitAsync(cancellationToken))
        {
            return Fail(SurveyErrorCode.Busy, response);
        }

        Notify(survey, version, response, wasGrading, before, change, actorId);

        return new SurveyResponseResult { Response = response };
    }

    private async Task<SurveyResponseResult> SaveAsync(
        SurveyResponse response,
        Survey survey,
        SurveyVersion version,
        List<ScoreChange> changes,
        GradingStatus wasGrading,
        decimal? before,
        string actorId,
        CancellationToken cancellationToken)
    {
        response.ScoreHistory ??= [];
        response.ScoreHistory.AddRange(changes);
        response.ModifiedUtc = _clock.UtcNow;

        await _responses.UpdateAsync(response, cancellationToken);

        if (!await _committer.CommitAsync(cancellationToken))
        {
            return Fail(SurveyErrorCode.Busy, response);
        }

        Notify(survey, version, response, wasGrading, before, changes.LastOrDefault(), actorId);

        return new SurveyResponseResult { Response = response };
    }

    private void Notify(Survey survey, SurveyVersion version, SurveyResponse response, GradingStatus wasGrading, decimal? before, ScoreChange change, string actorId)
    {
        var context = new SurveyResponseEventContext
        {
            Survey = survey,
            Version = version,
            Response = response,
            ScoreChange = change,
            ActorId = actorId,
        };

        if (response.Grading == GradingStatus.Graded && wasGrading != GradingStatus.Graded)
        {
            _dispatcher.Dispatch(observer => observer.GradedAsync(context));
        }
        else if (response.Grading == GradingStatus.Graded && before != response.Score)
        {
            _dispatcher.Dispatch(observer => observer.ScoreChangedAsync(context));
        }
    }

    private async Task<(SurveyResponse Response, Survey Survey, SurveyVersion Version, SurveyDefinition Definition, SurveyErrorCode Error)> LoadAsync(string responseId, CancellationToken cancellationToken)
    {
        var response = string.IsNullOrEmpty(responseId) ? null : await _responses.FindByIdAsync(responseId, cancellationToken);

        if (response is null)
        {
            return (null, null, null, null, SurveyErrorCode.NotFound);
        }

        var survey = await _surveys.FindByIdAsync(response.SurveyId, cancellationToken);
        var version = await _versionCache.GetAsync(response.SurveyVersionId, cancellationToken);

        // A preview response may run the draft, which has no stored version. A response that Regrade versions scored
        // with another version's key is scored with that key again.
        var definition = await SurveyScoringKeys.ScoringDefinitionAsync(_versionCache, response, version, version?.Definition ?? survey?.Draft, cancellationToken);

        if (definition is null)
        {
            return (response, survey, version, null, SurveyErrorCode.NotFound);
        }

        response.Answers ??= [];

        // Scoring re-runs the logic: a rule on the date or time reads the completion time in the site's time zone.
        await _scoringEngine.PrepareAsync();

        return (response, survey, version, definition, SurveyErrorCode.None);
    }

    private void ScoreKeeping(SurveyDefinition definition, SurveyResponse response, IReadOnlyList<SurveyQuestion> kept)
    {
        if (kept.Count == 0)
        {
            _scoringEngine.Score(definition, response);

            return;
        }

        // A kept question counts the points it has now: they stand in as a grader's score while the engine adds the
        // total up, then the answer goes back to how it was.
        var saved = new List<(SurveyAnswer Answer, decimal? Manual, decimal? Auto)>();

        foreach (var question in kept)
        {
            if (response.Answers.TryGetValue(question.Id, out var answer) && answer is not null && (answer.ManualScore ?? answer.AutoScore) is { } points)
            {
                saved.Add((answer, answer.ManualScore, answer.AutoScore));
                answer.ManualScore = points;
            }
        }

        try
        {
            _scoringEngine.Score(definition, response);
        }
        finally
        {
            foreach (var (answer, manual, auto) in saved)
            {
                answer.ManualScore = manual;
                answer.AutoScore = auto;
            }
        }
    }

    private static SurveyResponseResult Fail(SurveyErrorCode error, SurveyResponse response)
        => new() { Error = error, Response = response };
}
