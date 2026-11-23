using System.Text.Json;
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

        var count = 0;

        foreach (var versionId in fromVersionIds.Distinct(StringComparer.Ordinal))
        {
            var version = await _versionCache.GetAsync(versionId, cancellationToken);

            if (version is null || version.SurveyId != surveyId)
            {
                continue;
            }

            var (definition, kept) = WithKeyOf(version.Definition, keyVersion.Definition);

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

        // A preview response may run the draft, which has no stored version.
        var definition = version?.Definition ?? survey?.Draft;

        if (definition is null)
        {
            return (response, survey, version, null, SurveyErrorCode.NotFound);
        }

        response.Answers ??= [];

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

    private static (SurveyDefinition Definition, IReadOnlyList<SurveyQuestion> Kept) WithKeyOf(SurveyDefinition definition, SurveyDefinition keyDefinition)
    {
        var copy = JsonSerializer.Deserialize<SurveyDefinition>(JsonSerializer.Serialize(definition));
        var kept = new List<SurveyQuestion>();
        var keys = keyDefinition.Pages
            .SelectMany(page => page.Questions)
            .Where(question => !string.IsNullOrEmpty(question?.Id))
            .GroupBy(question => question.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var question in copy.Pages.SelectMany(page => page.Questions))
        {
            // Only questions of an unchanged type take the other version's key: a changed type scores differently.
            if (question?.Id is null
                || !keys.TryGetValue(question.Id, out var key)
                || !string.Equals(key.ControlType, question.ControlType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // A key that names choices or rows this version does not have (a bank imported again before choices kept
            // their ids) would score every answer 0, so the question keeps its own key and its scores.
            if (!KeyFits(question, key.Scoring))
            {
                kept.Add(question);

                continue;
            }

            question.Scoring = JsonSerializer.Deserialize<SurveyQuestionScoring>(JsonSerializer.Serialize(key.Scoring ?? new SurveyQuestionScoring()));
        }

        return (copy, kept);
    }

    private static bool KeyFits(SurveyQuestion question, SurveyQuestionScoring key)
    {
        if (key is null)
        {
            return true;
        }

        // Only ids the question lists can be checked: a yes/no question keys "true" or "false", and a question whose
        // choices come from another one has none of its own.
        var choiceIds = (question.Choices ?? []).Where(choice => !string.IsNullOrEmpty(choice?.Id)).Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);
        var rowIds = (question.Rows ?? []).Where(row => !string.IsNullOrEmpty(row?.Id)).Select(row => row.Id).ToHashSet(StringComparer.Ordinal);

        if (choiceIds.Count > 0)
        {
            var named = (key.CorrectChoiceIds ?? [])
                .Concat(key.ChoicePoints?.Keys ?? Enumerable.Empty<string>())
                .Concat(key.Pairs?.Values ?? Enumerable.Empty<string>());

            if (named.Any(id => !string.IsNullOrEmpty(id) && !choiceIds.Contains(id)))
            {
                return false;
            }
        }

        if (rowIds.Count > 0 && (key.Pairs?.Keys ?? Enumerable.Empty<string>()).Any(id => !string.IsNullOrEmpty(id) && !rowIds.Contains(id)))
        {
            return false;
        }

        return true;
    }

    private static SurveyResponseResult Fail(SurveyErrorCode error, SurveyResponse response)
        => new() { Error = error, Response = response };
}
