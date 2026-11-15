using System.Globalization;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Runs attempts on the server: it draws and copies the questions, fixes the deadline, saves each answer, accepts the
/// submit once, submits at the deadline, grades, regrades and voids, and reports the quiz score to Learning.
/// </summary>
public sealed class DefaultAssessmentAttemptService : IAssessmentAttemptService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long after the deadline an answer sent before it may still arrive.
    /// </summary>
    private static readonly TimeSpan _grace = TimeSpan.FromSeconds(5);

    private readonly AssessmentAttemptStore _attemptStore;
    private readonly AssessmentOverrideStore _overrideStore;
    private readonly IAssessmentContentSource _contentSource;
    private readonly ICourseStructureService _courseStructure;
    private readonly ICourseAccessService _courseAccess;
    private readonly EnrollmentStore _enrollmentStore;
    private readonly ILearnerProgressService _progressService;
    private readonly INumberSequence _numberSequence;
    private readonly IDistributedLock _distributedLock;
    private readonly EnrollmentWriteGate _writeGate;
    private readonly LearningSecretVerifier _secrets;
    private readonly ILeasedWorkQueue<AttemptDeadlineWork> _deadlines;
    private readonly IEnumerable<IQuestionScorer> _scorers;
    private readonly QuestionTypeOptions _questionTypes;
    private readonly AttemptReviewPolicy _reviewPolicy;
    private readonly IEnumerable<IAttemptObserver> _observers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAssessmentAttemptService"/> class.
    /// </summary>
    public DefaultAssessmentAttemptService(
        AssessmentAttemptStore attemptStore,
        AssessmentOverrideStore overrideStore,
        IAssessmentContentSource contentSource,
        ICourseStructureService courseStructure,
        ICourseAccessService courseAccess,
        EnrollmentStore enrollmentStore,
        ILearnerProgressService progressService,
        INumberSequence numberSequence,
        IDistributedLock distributedLock,
        EnrollmentWriteGate writeGate,
        LearningSecretVerifier secrets,
        ILeasedWorkQueue<AttemptDeadlineWork> deadlines,
        IEnumerable<IQuestionScorer> scorers,
        IOptions<QuestionTypeOptions> questionTypes,
        AttemptReviewPolicy reviewPolicy,
        IEnumerable<IAttemptObserver> observers,
        IClock clock,
        ILogger<DefaultAssessmentAttemptService> logger)
    {
        _attemptStore = attemptStore;
        _overrideStore = overrideStore;
        _contentSource = contentSource;
        _courseStructure = courseStructure;
        _courseAccess = courseAccess;
        _enrollmentStore = enrollmentStore;
        _progressService = progressService;
        _numberSequence = numberSequence;
        _distributedLock = distributedLock;
        _writeGate = writeGate;
        _secrets = secrets;
        _deadlines = deadlines;
        _scorers = scorers;
        _questionTypes = questionTypes.Value;
        _reviewPolicy = reviewPolicy;
        _observers = observers;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AttemptResult> StartAsync(StartAttemptRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.QuizId))
        {
            return AttemptResult.Fail(AttemptErrors.NotFound);
        }

        var quiz = await _contentSource.GetItemAsync(request.QuizId);
        var part = quiz?.Get<QuizPart>(nameof(QuizPart));

        if (part is null)
        {
            return AttemptResult.Fail(AttemptErrors.NotFound);
        }

        var access = await _courseAccess.GetItemAccessAsync(request.User, quiz);
        var staff = access.Reason == CourseItemAccessReason.Staff;

        if (!staff && (access.Reason != CourseItemAccessReason.Enrolled || access.Enrollment?.Status != EnrollmentStatus.Active))
        {
            return AttemptResult.Fail(AttemptErrors.NoAccess);
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync($"Learning:Attempt:{request.UserId}:{request.QuizId}", _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return AttemptResult.Fail(AttemptErrors.Busy);
        }

        var started = new List<Func<Task>>();
        AttemptResult startResult;

        await using (locker)
        {
            var now = _clock.UtcNow;
            var attempts = await _attemptStore.ListAsync(request.UserId, request.QuizId, cancellationToken);
            var open = attempts.FirstOrDefault(attempt => attempt.IsOpen);

            if (open is not null)
            {
                if (open.DeadlineUtc is null || now < open.DeadlineUtc.Value + _grace)
                {
                    return new AttemptResult { Attempt = open };
                }

                // The expired attempt is submitted under its own lock, as every attempt write is.
                await WriteAsync(open.ItemId, async (fresh, notifications) =>
                {
                    if (fresh.IsOpen)
                    {
                        await SubmitCoreAsync(fresh, autoSubmitted: true, notifications, cancellationToken);
                    }

                    return new AttemptResult { Attempt = fresh };
                }, cancellationToken);
            }

            var entry = await _overrideStore.FindLatestAsync(request.UserId, request.QuizId, cancellationToken);
            var opens = entry?.OpensUtc ?? part.OpensUtc;
            var closes = entry?.ClosesUtc ?? part.ClosesUtc;

            if ((opens is not null && now < opens) || (closes is not null && now >= closes))
            {
                return AttemptResult.Fail(AttemptErrors.Closed);
            }

            var counted = attempts.Where(attempt => attempt.Status != AttemptStatus.Voided).ToList();

            if (part.MaxAttempts is { } max && counted.Count >= max + (entry?.ExtraAttempts ?? 0))
            {
                return AttemptResult.Fail(AttemptErrors.NoAttemptsLeft);
            }

            var last = counted.LastOrDefault(attempt => attempt.SubmittedUtc is not null);

            if (part.DelayMinutes > 0 && last?.SubmittedUtc is { } submitted && now < submitted.AddMinutes(part.DelayMinutes))
            {
                return new AttemptResult { ErrorCode = AttemptErrors.TooSoon, AvailableFromUtc = submitted.AddMinutes(part.DelayMinutes) };
            }

            switch (await _secrets.CheckAsync(AssessmentsConstants.QuizAccessCodeSecret, request.QuizId, request.UserId, request.AccessCode, cancellationToken))
            {
                case SecretCheck.LockedOut:
                    return AttemptResult.Fail(AttemptErrors.TooManyAttempts);
                case SecretCheck.Rejected:
                    return AttemptResult.Fail(AttemptErrors.WrongAccessCode);
            }

            if (!string.IsNullOrWhiteSpace(part.HonorStatement) && !request.HonorConfirmed)
            {
                return AttemptResult.Fail(AttemptErrors.HonorRequired);
            }

            var random = new Random();
            var items = await DrawAsync(part, random);

            if (items.Count == 0)
            {
                return AttemptResult.Fail(AttemptErrors.NoQuestions);
            }

            var itemPart = quiz.Get<CourseItemPart>(nameof(CourseItemPart));
            var attempt = new AssessmentAttempt
            {
                UserId = request.UserId,
                EnrollmentId = staff ? null : access.Enrollment?.ItemId,
                CourseId = _courseStructure.GetCourseId(quiz),
                QuizId = quiz.ContentItemId,
                QuizTitle = quiz.DisplayText,
                Number = "A" + (await _numberSequence.NextAsync(AssessmentsConstants.AttemptNumberSeries, cancellationToken: cancellationToken)).ToString("D6", CultureInfo.InvariantCulture),
                Sequence = counted.Count + 1,
                Status = AttemptStatus.InProgress,
                StartedUtc = now,
                Items = items,
                MaxScore = items.Sum(item => item.Points),
                PassMark = part.PassMark ?? itemPart?.PassMark,
                ReviewPolicy = part.ReviewPolicy,
                QuizClosesUtc = closes,
                HonorStatement = string.IsNullOrWhiteSpace(part.HonorStatement) ? null : part.HonorStatement,
            };

            if (part.TimeLimitMinutes is { } minutes and > 0)
            {
                attempt.DeadlineUtc = now.AddMinutes(minutes + (entry?.ExtraMinutes ?? 0));
            }

            if (closes is not null && (attempt.DeadlineUtc is null || attempt.DeadlineUtc > closes))
            {
                attempt.DeadlineUtc = closes;
            }

            await _attemptStore.CreateAsync(attempt, cancellationToken);

            if (attempt.DeadlineUtc is { } deadline)
            {
                await _deadlines.EnqueueAsync(new AttemptDeadlineWork { AttemptId = attempt.ItemId, NextAttemptUtc = deadline }, cancellationToken);
            }

            // Commit before the lock is let go, so a second start for the same learner and quiz finds this attempt.
            await _writeGate.CommitAsync(cancellationToken);
            started.Add(() => NotifyAsync(observer => observer.StartedAsync(attempt, cancellationToken)));
            startResult = new AttemptResult { Attempt = attempt };
        }

        foreach (var notify in started)
        {
            await notify();
        }

        return startResult;
    }

    /// <inheritdoc />
    public Task<AttemptResult> SaveResponseAsync(string attemptId, string userId, int index, AttemptResponse response, CancellationToken cancellationToken = default)
        => WriteOwnAsync(attemptId, userId, (attempt, notifications) => SaveResponseCoreAsync(attempt, index, response, notifications, cancellationToken), cancellationToken);

    private async Task<AttemptResult> SaveResponseCoreAsync(AssessmentAttempt attempt, int index, AttemptResponse response, IList<Func<Task>> notifications, CancellationToken cancellationToken)
    {
        if (!attempt.IsOpen)
        {
            return new AttemptResult { Attempt = attempt, ErrorCode = AttemptErrors.NotInProgress };
        }

        var now = _clock.UtcNow;

        if (attempt.DeadlineUtc is { } deadline && now > deadline + _grace)
        {
            await SubmitCoreAsync(attempt, autoSubmitted: true, notifications, cancellationToken);

            return new AttemptResult { Attempt = attempt, ErrorCode = AttemptErrors.DeadlinePassed };
        }

        if (index < 0 || index >= attempt.Items.Count)
        {
            return new AttemptResult { Attempt = attempt, ErrorCode = AttemptErrors.NotFound };
        }

        var quiz = await _contentSource.GetItemAsync(attempt.QuizId);

        if (quiz?.Get<QuizPart>(nameof(QuizPart))?.Navigation == QuizNavigation.Sequential
            && attempt.Items.Any(item => item.Index > index && item.Response is not null))
        {
            return new AttemptResult { Attempt = attempt, ErrorCode = AttemptErrors.ItemLocked };
        }

        response ??= new AttemptResponse();
        response.AnsweredUtc = now;
        attempt.Items[index].Response = response;

        await _attemptStore.UpdateAsync(attempt, cancellationToken);

        return new AttemptResult { Attempt = attempt };
    }

    /// <inheritdoc />
    public Task<AttemptResult> SubmitAsync(string attemptId, string userId, CancellationToken cancellationToken = default)
        => WriteOwnAsync(attemptId, userId, async (attempt, notifications) =>
        {
            if (!attempt.IsOpen)
            {
                return new AttemptResult { Attempt = attempt };
            }

            var late = attempt.DeadlineUtc is { } deadline && _clock.UtcNow > deadline + _grace;

            await SubmitCoreAsync(attempt, autoSubmitted: late, notifications, cancellationToken);

            return new AttemptResult { Attempt = attempt };
        }, cancellationToken);

    /// <inheritdoc />
    public Task<AttemptResult> SubmitAtDeadlineAsync(string attemptId, CancellationToken cancellationToken = default)
        => WriteAsync(attemptId, async (attempt, notifications) =>
        {
            if (attempt.IsOpen && attempt.DeadlineUtc is { } deadline && _clock.UtcNow >= deadline)
            {
                await SubmitCoreAsync(attempt, autoSubmitted: true, notifications, cancellationToken);
            }

            return new AttemptResult { Attempt = attempt };
        }, cancellationToken);

    /// <inheritdoc />
    public Task<AttemptResult> GradeAsync(string attemptId, IDictionary<int, ManualScore> scores, string feedback, string actor, CancellationToken cancellationToken = default)
        => WriteAsync(attemptId, (attempt, notifications) => GradeCoreAsync(attempt, scores, feedback, actor, notifications, cancellationToken), cancellationToken);

    private async Task<AttemptResult> GradeCoreAsync(AssessmentAttempt attempt, IDictionary<int, ManualScore> scores, string feedback, string actor, IList<Func<Task>> notifications, CancellationToken cancellationToken)
    {
        if (attempt.Status is not (AttemptStatus.NeedsGrading or AttemptStatus.Graded))
        {
            return new AttemptResult { Attempt = attempt, ErrorCode = AttemptErrors.NotInProgress };
        }

        foreach (var (index, score) in scores ?? new Dictionary<int, ManualScore>())
        {
            if (index < 0 || index >= attempt.Items.Count || score is null || score.Points < 0 || score.Points > attempt.Items[index].Points)
            {
                return new AttemptResult { Attempt = attempt, ErrorCode = AttemptErrors.InvalidScore };
            }
        }

        var before = attempt.Score;
        var wasGraded = attempt.Status == AttemptStatus.Graded;

        foreach (var (index, score) in scores ?? new Dictionary<int, ManualScore>())
        {
            attempt.Items[index].ManualScore = score.Points;
            attempt.Items[index].GraderComment = string.IsNullOrWhiteSpace(score.Comment) ? null : score.Comment.Trim();
        }

        attempt.Feedback = string.IsNullOrWhiteSpace(feedback) ? attempt.Feedback : feedback.Trim();
        attempt.GradedBy = actor;
        attempt.GradedUtc = _clock.UtcNow;
        Total(attempt);

        var change = new AttemptScoreChange { ChangedUtc = _clock.UtcNow, Actor = actor, Kind = wasGraded ? "Overridden" : "Graded", From = before, To = attempt.Score };
        attempt.ScoreHistory.Add(change);

        await _attemptStore.UpdateAsync(attempt, cancellationToken);
        await ReportAsync(attempt, cancellationToken);

        if (attempt.Status == AttemptStatus.Graded)
        {
            notifications.Add(() => NotifyAsync(observer => wasGraded ? observer.ScoreChangedAsync(attempt, change, cancellationToken) : observer.GradedAsync(attempt, cancellationToken)));
        }

        return new AttemptResult { Attempt = attempt };
    }

    /// <inheritdoc />
    public Task<AttemptResult> RegradeAsync(string attemptId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Task.FromResult(AttemptResult.Fail(AttemptErrors.ReasonRequired));
        }

        return WriteAsync(attemptId, (attempt, notifications) => RegradeCoreAsync(attempt, actor, reason, notifications, cancellationToken), cancellationToken);
    }

    private async Task<AttemptResult> RegradeCoreAsync(AssessmentAttempt attempt, string actor, string reason, IList<Func<Task>> notifications, CancellationToken cancellationToken)
    {
        if (attempt.Status is AttemptStatus.InProgress or AttemptStatus.Voided or AttemptStatus.Abandoned)
        {
            return new AttemptResult { Attempt = attempt, ErrorCode = AttemptErrors.NotInProgress };
        }

        var before = attempt.Score;

        foreach (var item in attempt.Items)
        {
            item.AutoScore = Scorer(item.QuestionType)?.Score(item);
        }

        Total(attempt);

        var change = new AttemptScoreChange { ChangedUtc = _clock.UtcNow, Actor = actor, Kind = "Regraded", From = before, To = attempt.Score, Reason = reason.Trim() };
        attempt.ScoreHistory.Add(change);

        await _attemptStore.UpdateAsync(attempt, cancellationToken);
        await ReportAsync(attempt, cancellationToken);
        notifications.Add(() => NotifyAsync(observer => observer.ScoreChangedAsync(attempt, change, cancellationToken)));

        return new AttemptResult { Attempt = attempt };
    }

    /// <inheritdoc />
    public Task<AttemptResult> VoidAsync(string attemptId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Task.FromResult(AttemptResult.Fail(AttemptErrors.ReasonRequired));
        }

        return WriteAsync(attemptId, async (attempt, notifications) =>
        {
            if (attempt.Status == AttemptStatus.Voided)
            {
                return new AttemptResult { Attempt = attempt };
            }

            var change = new AttemptScoreChange { ChangedUtc = _clock.UtcNow, Actor = actor, Kind = "Voided", From = attempt.Score, To = null, Reason = reason.Trim() };

            attempt.Status = AttemptStatus.Voided;
            attempt.ScoreHistory.Add(change);

            await _attemptStore.UpdateAsync(attempt, cancellationToken);
            await ReportAsync(attempt, cancellationToken);
            notifications.Add(() => NotifyAsync(observer => observer.ScoreChangedAsync(attempt, change, cancellationToken)));

            return new AttemptResult { Attempt = attempt };
        }, cancellationToken);
    }

    /// <inheritdoc />
    public bool CanReview(AssessmentAttempt attempt)
        => _reviewPolicy.CanReview(attempt);

    private async Task SubmitCoreAsync(AssessmentAttempt attempt, bool autoSubmitted, IList<Func<Task>> notifications, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        attempt.AutoSubmitted = autoSubmitted;
        attempt.SubmittedUtc = autoSubmitted && attempt.DeadlineUtc is { } deadline && deadline < now ? deadline : now;

        foreach (var item in attempt.Items)
        {
            item.AutoScore = Scorer(item.QuestionType)?.Score(item);
        }

        Total(attempt);
        attempt.ScoreHistory.Add(new AttemptScoreChange { ChangedUtc = now, Actor = attempt.UserId, Kind = "Submitted", To = attempt.Score });

        await _attemptStore.UpdateAsync(attempt, cancellationToken);
        await ReportAsync(attempt, cancellationToken);
        notifications.Add(() => NotifyAsync(observer => observer.SubmittedAsync(attempt, cancellationToken)));
        notifications.Add(() => NotifyAsync(observer => attempt.Status == AttemptStatus.NeedsGrading
            ? observer.NeedsGradingAsync(attempt, cancellationToken)
            : observer.GradedAsync(attempt, cancellationToken)));
    }

    private static void Total(AssessmentAttempt attempt)
    {
        var allScored = attempt.Items.All(item => item.Score is not null);

        attempt.Score = attempt.Items.Sum(item => item.Score ?? 0);
        attempt.Percent = attempt.MaxScore > 0 ? Math.Round(attempt.Score.Value * 100 / attempt.MaxScore, 2, MidpointRounding.AwayFromZero) : 0;

        if (attempt.Status != AttemptStatus.Voided)
        {
            attempt.Status = allScored ? AttemptStatus.Graded : AttemptStatus.NeedsGrading;
        }

        attempt.Passed = allScored ? attempt.PassMark is null || attempt.Percent >= attempt.PassMark : null;
    }

    private async Task ReportAsync(AssessmentAttempt attempt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(attempt.EnrollmentId))
        {
            return;
        }

        var enrollment = await _enrollmentStore.FindByIdAsync(attempt.EnrollmentId, cancellationToken);

        if (enrollment is null)
        {
            return;
        }

        var attempts = await _attemptStore.ListAsync(attempt.UserId, attempt.QuizId, cancellationToken);
        var current = attempts.Select(entry => entry.ItemId == attempt.ItemId ? attempt : entry).ToList();

        if (current.All(entry => entry.ItemId != attempt.ItemId))
        {
            current.Add(attempt);
        }

        var quiz = await _contentSource.GetItemAsync(attempt.QuizId);
        var method = quiz?.Get<QuizPart>(nameof(QuizPart))?.GradingMethod ?? GradingMethod.Highest;
        var counted = current.Where(entry => entry.Status != AttemptStatus.Voided).ToList();
        var score = QuizGradeCalculator.Calculate(counted, method);
        var passMark = attempt.PassMark;

        try
        {
            await _progressService.RecordResultAsync(enrollment, new CourseItemResult
            {
                ItemId = attempt.QuizId,
                Score = score,
                Passed = score is null ? null : passMark is null || score >= passMark,
                Submitted = counted.Any(entry => entry.SubmittedUtc is not null),
                Graded = score is not null,
                Attempts = counted.Count,
                ResetScore = score is null,
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The result of attempt {AttemptId} could not be recorded on its enrollment.", attempt.ItemId);

            throw;
        }
    }

    private async Task<List<AttemptItem>> DrawAsync(QuizPart part, Random random)
    {
        var selected = new List<ContentItem>();
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var question in await _contentSource.GetQuestionsAsync(part.QuestionIds))
        {
            if (used.Add(question.ContentItemId))
            {
                selected.Add(question);
            }
        }

        foreach (var rule in part.DrawRules.Where(rule => rule is not null && !string.IsNullOrEmpty(rule.BankId) && rule.Count > 0))
        {
            var pool = (await _contentSource.GetBankQuestionsAsync(rule.BankId))
                .Where(question => !used.Contains(question.ContentItemId) && _questionTypes.IsQuestion(question.ContentType))
                .Where(question =>
                {
                    var questionPart = question.Get<QuestionPart>(nameof(QuestionPart));

                    return (string.IsNullOrEmpty(rule.Tag) || (questionPart?.Tags?.Contains(rule.Tag, StringComparer.OrdinalIgnoreCase) ?? false))
                        && (rule.Difficulty is null || questionPart?.Difficulty == rule.Difficulty);
                })
                .ToList();

            for (var i = pool.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            foreach (var question in pool.Take(rule.Count))
            {
                used.Add(question.ContentItemId);
                selected.Add(question);
            }
        }

        if (part.ShuffleQuestions)
        {
            for (var i = selected.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (selected[i], selected[j]) = (selected[j], selected[i]);
            }
        }

        var items = new List<AttemptItem>();

        foreach (var question in selected)
        {
            var type = _questionTypes.FindByContentType(question.ContentType);
            var scorer = type is null ? null : Scorer(type.Name);

            if (scorer is null)
            {
                continue;
            }

            var questionPart = question.Get<QuestionPart>(nameof(QuestionPart)) ?? new QuestionPart();
            var item = new AttemptItem
            {
                Index = items.Count,
                QuestionId = question.ContentItemId,
                QuestionVersionId = question.ContentItemVersionId,
                QuestionType = type.Name,
                Title = question.DisplayText,
                Prompt = questionPart.Prompt,
                Points = Math.Max(questionPart.Points, 0),
                Feedback = questionPart.GeneralFeedback,
            };

            scorer.Snapshot(question, item, random);
            items.Add(item);
        }

        return items;
    }

    private IQuestionScorer Scorer(string questionType)
        => _scorers.FirstOrDefault(scorer => string.Equals(scorer.QuestionType, questionType, StringComparison.Ordinal));

    // Every write to an attempt runs under one lock per attempt on a fresh read; the session is committed before the
    // lock is let go and the observers run after the commit. A write that reaches the enrollment (the quiz result) takes
    // the enrollment lock inside this one; enrollment writes never take an attempt lock, so the order cannot deadlock.
    private Task<AttemptResult> WriteAsync(string attemptId, Func<AssessmentAttempt, IList<Func<Task>>, Task<AttemptResult>> work, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(attemptId))
        {
            return Task.FromResult(AttemptResult.Fail(AttemptErrors.NotFound));
        }

        return _writeGate.RunLockedAsync(
            AttemptLockKey(attemptId),
            async notifications =>
            {
                var attempt = await _attemptStore.FindFreshAsync(attemptId, cancellationToken);

                return attempt is null ? AttemptResult.Fail(AttemptErrors.NotFound) : await work(attempt, notifications);
            },
            () => AttemptResult.Fail(AttemptErrors.Busy),
            cancellationToken);
    }

    private Task<AttemptResult> WriteOwnAsync(string attemptId, string userId, Func<AssessmentAttempt, IList<Func<Task>>, Task<AttemptResult>> work, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult(AttemptResult.Fail(AttemptErrors.NotFound));
        }

        return WriteAsync(attemptId, (attempt, notifications) => string.Equals(attempt.UserId, userId, StringComparison.Ordinal)
            ? work(attempt, notifications)
            : Task.FromResult(AttemptResult.Fail(AttemptErrors.NotFound)), cancellationToken);
    }

    /// <summary>
    /// Gets the lock key of an attempt.
    /// </summary>
    /// <param name="attemptId">The attempt.</param>
    /// <returns>The key.</returns>
    public static string AttemptLockKey(string attemptId)
        => "Learning:AttemptWrite:" + attemptId;

    private async Task NotifyAsync(Func<IAttemptObserver, Task> notify)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await notify(observer);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "An attempt observer {Observer} failed.", observer.GetType().Name);
            }
        }
    }
}
