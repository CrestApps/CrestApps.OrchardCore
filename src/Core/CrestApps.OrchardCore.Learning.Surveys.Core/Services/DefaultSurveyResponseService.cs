using System.Globalization;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Learning.Surveys.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Surveys.Core.Services;

/// <summary>
/// Takes learners' answers to surveys. A write runs under one lock per enrollment and survey on a fresh read of the
/// response, saves the response before it reports to Learning (whose progress write commits the session), so the
/// learner's answers are kept even when Learning does not take the result.
/// </summary>
internal sealed class DefaultSurveyResponseService : ISurveyResponseService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly SurveyResponseStore _store;
    private readonly ISurveyContentSource _contentSource;
    private readonly ICourseStructureService _courseStructure;
    private readonly CourseReleaseEvaluator _release;
    private readonly IEnrollmentStore _enrollments;
    private readonly ILearnerProgressService _progress;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DefaultSurveyResponseService(
        SurveyResponseStore store,
        ISurveyContentSource contentSource,
        ICourseStructureService courseStructure,
        CourseReleaseEvaluator release,
        IEnrollmentStore enrollments,
        ILearnerProgressService progress,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        ILogger<DefaultSurveyResponseService> logger)
    {
        _store = store;
        _contentSource = contentSource;
        _courseStructure = courseStructure;
        _release = release;
        _enrollments = enrollments;
        _progress = progress;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    public async Task<bool> CanSeeResultsAsync(string surveyId, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var survey = await FindSurveyAsync(surveyId);

        if (survey is null)
        {
            return false;
        }

        if (await _courseStructure.IsCourseStaffAsync(userId, survey.CourseId))
        {
            return true;
        }

        // Only the person's own enrollment: an active one, within its access dates, once the item is released to them, or
        // a completed one, whose learner may review every item of the course.
        var own = (await _enrollments.ListByCourseForUserAsync(userId, survey.CourseId, cancellationToken))
            .Where(candidate => string.Equals(candidate.UserId, userId, StringComparison.Ordinal) &&
                string.Equals(candidate.CourseId, survey.CourseId, StringComparison.Ordinal))
            .ToArray();
        var active = own.FirstOrDefault(candidate => candidate.Status == EnrollmentStatus.Active);

        if (active is null)
        {
            return own.Any(candidate => candidate.Status == EnrollmentStatus.Completed);
        }

        return EnrollmentAccess.HasAccess(active, _clock.UtcNow)
            && (await _release.EvaluateAsync(survey.Entry, active, await _progress.GetAsync(active.ItemId, cancellationToken))).IsReleased;
    }

    public async Task<SurveyResponseResult> AnswerAsync(SurveyAnswerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.UserId))
        {
            return SurveyResponseResult.Fail(SurveyErrors.NotEnrolled);
        }

        var survey = await FindSurveyAsync(request.SurveyId);

        if (survey is null)
        {
            return SurveyResponseResult.Fail(SurveyErrors.NotSurvey);
        }

        // Only the learner's own active enrollment in the survey's course, or their completed one: feedback at the end of a
        // course is mostly given once it is completed. The user id is checked again so another learner's enrollment is
        // never used.
        var own = (await _enrollments.ListByCourseForUserAsync(request.UserId, survey.CourseId, cancellationToken))
            .Where(candidate => string.Equals(candidate.UserId, request.UserId, StringComparison.Ordinal) &&
                string.Equals(candidate.CourseId, survey.CourseId, StringComparison.Ordinal))
            .ToArray();
        var enrollment = own.FirstOrDefault(candidate => candidate.Status == EnrollmentStatus.Active)
            ?? own.FirstOrDefault(candidate => candidate.Status == EnrollmentStatus.Completed);

        if (enrollment is null)
        {
            return SurveyResponseResult.Fail(SurveyErrors.NotEnrolled);
        }

        var now = _clock.UtcNow;

        if (enrollment.Status == EnrollmentStatus.Completed)
        {
            // A learner who completed the course may answer while they may still review it, as course access decides.
            if (enrollment.Grants.Count == 0 || (enrollment.AccessEndsUtc is not null && enrollment.AccessEndsUtc <= now))
            {
                return SurveyResponseResult.Fail(SurveyErrors.AccessEnded);
            }
        }
        else
        {
            if (!EnrollmentAccess.HasAccess(enrollment, now))
            {
                return SurveyResponseResult.Fail(SurveyErrors.AccessEnded);
            }

            if (!(await _release.EvaluateAsync(survey.Entry, enrollment, await _progress.GetAsync(enrollment.ItemId, cancellationToken))).IsReleased)
            {
                return SurveyResponseResult.Fail(SurveyErrors.NotAvailableYet);
            }
        }

        if (SurveyRules.IsClosed(survey.Part, now))
        {
            return SurveyResponseResult.Fail(SurveyErrors.Closed);
        }

        var checkedAnswers = SurveyRules.CheckAnswers(survey.Part, request.Answers);

        if (checkedAnswers.Error is not null)
        {
            return SurveyResponseResult.Fail(checkedAnswers.Error, checkedAnswers.QuestionId);
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync($"LearningSurvey:{enrollment.ItemId}:{survey.Item.ContentItemId}", _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return SurveyResponseResult.Fail(SurveyErrors.Busy);
        }

        try
        {
            var response = await _store.FindForEnrollmentAsync(enrollment.ItemId, survey.Item.ContentItemId, cancellationToken);

            if (response is not null && !survey.Part.AllowChangeAnswer)
            {
                return new SurveyResponseResult { Error = SurveyErrors.AlreadyAnswered, Response = response };
            }

            if (response is null)
            {
                response = new SurveyResponse
                {
                    ItemId = IdGenerator.GenerateId(),
                    EnrollmentId = enrollment.ItemId,
                    UserId = enrollment.UserId,
                    CourseId = survey.CourseId,
                    SurveyId = survey.Item.ContentItemId,
                    SurveyTitle = survey.Item.DisplayText,
                    Answers = checkedAnswers.Answers,
                    SubmittedUtc = now,
                };

                await _store.CreateAsync(response, cancellationToken);
            }
            else
            {
                response.Answers = checkedAnswers.Answers;
                response.SurveyTitle = survey.Item.DisplayText;
                response.UpdatedUtc = now;

                await _store.UpdateAsync(response, cancellationToken);
            }

            await _session.SaveChangesAsync(cancellationToken);

            var update = await _progress.RecordWithRetryAsync(enrollment, new CourseItemResult
            {
                ItemId = response.SurveyId,
                Submitted = true,
            }, cancellationToken);

            if (update?.RefusedReason is not null && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("The survey answer of item {ItemId} was not recorded on enrollment {EnrollmentId}: {Reason}.", response.SurveyId, enrollment.ItemId, update.RefusedReason);
            }

            return new SurveyResponseResult { Response = response };
        }
        finally
        {
            await locker.DisposeAsync();
        }
    }

    public async Task<SurveyResponse> FindAsync(string enrollmentId, string surveyId, CancellationToken cancellationToken = default)
        => await _store.FindForEnrollmentAsync(enrollmentId, surveyId, cancellationToken);

    public async Task<SurveySummary> SummarizeAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        var item = await _contentSource.GetPublishedAsync(surveyId);
        var part = item.FindSurveyPart() ?? new SurveyPart();

        return SurveyRules.Summarize(part, await _store.ListBySurveyAsync(surveyId, cancellationToken));
    }

    public Task<(int Count, IReadOnlyList<SurveyResponse> Items)> ListAsync(string surveyId, int skip, int take, CancellationToken cancellationToken = default)
        => _store.PageBySurveyAsync(surveyId, skip, take, cancellationToken);

    private async Task<SurveyTarget> FindSurveyAsync(string surveyId)
    {
        var item = await _contentSource.GetPublishedAsync(surveyId);

        if (item is null || !item.Has<SurveyPart>())
        {
            return null;
        }

        var courseId = _courseStructure.GetCourseId(item);
        var course = string.IsNullOrEmpty(courseId) ? null : await _courseStructure.GetCourseAsync(courseId);

        if (course is null)
        {
            return null;
        }

        var entry = (await _courseStructure.GetOutlineAsync(course)).Find(item.ContentItemId);

        if (entry?.Part is null || !string.Equals(entry.Part.Kind, SurveysConstants.SurveyKind, StringComparison.Ordinal))
        {
            return null;
        }

        var outlineItem = entry.Item ?? item;

        return new SurveyTarget(outlineItem, courseId, outlineItem.FindSurveyPart() ?? item.FindSurveyPart(), entry);
    }

    private sealed record SurveyTarget(ContentItem Item, string CourseId, SurveyPart Part, CourseOutlineEntry Entry);
}

/// <summary>
/// Reads the survey part of a content item that may not have one, or may not exist.
/// </summary>
public static class SurveyContentItems
{
    /// <summary>
    /// Gets the survey part of a content item.
    /// </summary>
    /// <param name="item">The content item; may be <see langword="null"/>.</param>
    /// <returns>The part, or <see langword="null"/>.</returns>
    public static SurveyPart FindSurveyPart(this ContentItem item)
        => item is not null && item.TryGet<SurveyPart>(out var part) ? part : null;
}

/// <summary>
/// The rules of a survey that need no store: when it is closed, when learners see the totals, which answers a
/// question takes, and the totals.
/// </summary>
public static class SurveyRules
{
    /// <summary>
    /// Gets whether a survey stopped taking answers.
    /// </summary>
    /// <param name="part">The survey.</param>
    /// <param name="utcNow">The time now, in UTC.</param>
    /// <returns><see langword="true"/> once the close date has passed.</returns>
    public static bool IsClosed(SurveyPart part, DateTime utcNow)
        => part?.CloseUtc is { } close && utcNow >= DateTime.SpecifyKind(close, DateTimeKind.Utc);

    /// <summary>
    /// Gets whether a learner sees the totals of a survey.
    /// </summary>
    /// <param name="part">The survey.</param>
    /// <param name="answered">Whether the learner answered.</param>
    /// <param name="utcNow">The time now, in UTC.</param>
    /// <returns><see langword="true"/> when the learner sees them.</returns>
    public static bool LearnerSeesResults(SurveyPart part, bool answered, DateTime utcNow)
        => part?.ResultsVisibility switch
        {
            SurveyResultsVisibility.Always => true,
            SurveyResultsVisibility.AfterAnswer => answered,
            SurveyResultsVisibility.AfterClose => IsClosed(part, utcNow),
            _ => false,
        };

    /// <summary>
    /// Checks answers against the questions of a survey, and keeps only the answers of its questions: the chosen
    /// choices as the question spells them, a rating from 1 to 5, or the written answer, trimmed.
    /// </summary>
    /// <param name="part">The survey.</param>
    /// <param name="answers">The answers by question id.</param>
    /// <returns>The answers to keep, or the error and the question it is about.</returns>
    public static (Dictionary<string, List<string>> Answers, string Error, string QuestionId) CheckAnswers(SurveyPart part, IDictionary<string, IList<string>> answers)
    {
        ArgumentNullException.ThrowIfNull(part);

        var kept = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var question in part.Questions.Where(question => !string.IsNullOrEmpty(question.Id)))
        {
            var given = answers is not null && answers.TryGetValue(question.Id, out var values)
                ? (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToList()
                : [];

            if (given.Count == 0)
            {
                if (question.Required)
                {
                    return (null, SurveyErrors.Required, question.Id);
                }

                continue;
            }

            switch (question.Type)
            {
                case SurveyQuestionType.Text:
                    var text = string.Join(Environment.NewLine, given);

                    if (text.Length > SurveysConstants.MaxTextAnswerLength)
                    {
                        return (null, SurveyErrors.InvalidAnswer, question.Id);
                    }

                    kept[question.Id] = [text];
                    break;

                case SurveyQuestionType.Rating:
                    if (given.Count != 1 ||
                        !int.TryParse(given[0], NumberStyles.None, CultureInfo.InvariantCulture, out var rating) ||
                        rating < SurveysConstants.MinRating ||
                        rating > SurveysConstants.MaxRating)
                    {
                        return (null, SurveyErrors.InvalidAnswer, question.Id);
                    }

                    kept[question.Id] = [rating.ToString(CultureInfo.InvariantCulture)];
                    break;

                default:
                    if (question.Type == SurveyQuestionType.SingleChoice && given.Count != 1)
                    {
                        return (null, SurveyErrors.InvalidAnswer, question.Id);
                    }

                    var chosen = new List<string>();

                    foreach (var value in given)
                    {
                        var option = question.Options.FirstOrDefault(candidate => string.Equals(candidate?.Trim(), value, StringComparison.Ordinal));

                        if (option is null)
                        {
                            return (null, SurveyErrors.InvalidAnswer, question.Id);
                        }

                        if (!chosen.Contains(option, StringComparer.Ordinal))
                        {
                            chosen.Add(option);
                        }
                    }

                    kept[question.Id] = chosen;
                    break;
            }
        }

        return (kept, null, null);
    }

    /// <summary>
    /// Counts the answers of a survey: each choice in the question's order, each rating from 1 to 5 with the average,
    /// and the written answers.
    /// </summary>
    /// <param name="part">The survey.</param>
    /// <param name="responses">The responses.</param>
    /// <returns>The totals.</returns>
    public static SurveySummary Summarize(SurveyPart part, IReadOnlyCollection<SurveyResponse> responses)
    {
        ArgumentNullException.ThrowIfNull(part);

        responses ??= [];

        var summary = new SurveySummary { ResponseCount = responses.Count };

        foreach (var question in part.Questions.Where(question => !string.IsNullOrEmpty(question.Id)))
        {
            var totals = new SurveyQuestionSummary
            {
                QuestionId = question.Id,
                Text = question.Text,
                Type = question.Type,
            };

            if (question.Type == SurveyQuestionType.Rating)
            {
                for (var rating = SurveysConstants.MinRating; rating <= SurveysConstants.MaxRating; rating++)
                {
                    totals.Counts[rating.ToString(CultureInfo.InvariantCulture)] = 0;
                }
            }
            else if (question.Type != SurveyQuestionType.Text)
            {
                foreach (var option in question.Options.Where(option => !string.IsNullOrWhiteSpace(option)))
                {
                    totals.Counts.TryAdd(option, 0);
                }
            }

            var ratings = new List<int>();

            foreach (var response in responses)
            {
                if (response.Answers is null || !response.Answers.TryGetValue(question.Id, out var values) || values is null || values.Count == 0)
                {
                    continue;
                }

                totals.AnswerCount++;

                if (question.Type == SurveyQuestionType.Text)
                {
                    totals.TextAnswers.Add(values[0]);

                    continue;
                }

                foreach (var value in values)
                {
                    // An answer to a choice the author removed since is not counted.
                    if (totals.Counts.TryGetValue(value, out var count))
                    {
                        totals.Counts[value] = count + 1;
                    }

                    if (question.Type == SurveyQuestionType.Rating && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var rating))
                    {
                        ratings.Add(rating);
                    }
                }
            }

            if (ratings.Count > 0)
            {
                totals.Average = Math.Round((decimal)ratings.Sum() / ratings.Count, 2, MidpointRounding.AwayFromZero);
            }

            summary.Questions.Add(totals);
        }

        return summary;
    }
}
