using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Records the result of a course item when a learner's response to its survey changes: completed, graded, rescored or
/// voided. The provider of the item's kind builds the result (a course survey is submitted; a quiz combines attempts).
/// </summary>
public sealed class LearningSurveyResultObserver : ISurveyResponseObserver
{
    private readonly ILearningSurveyService _learningSurveys;
    private readonly ICourseStructureService _courseStructure;
    private readonly IEnrollmentStore _enrollments;
    private readonly ILearnerProgressService _progress;
    private readonly IEnumerable<ILearningSurveyItemResultProvider> _providers;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningSurveyResultObserver"/> class.
    /// </summary>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="courseStructure">The course structure service.</param>
    /// <param name="enrollments">The enrollment store.</param>
    /// <param name="progress">The learner progress service.</param>
    /// <param name="providers">The result providers by course item kind.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="logger">The logger.</param>
    public LearningSurveyResultObserver(
        ILearningSurveyService learningSurveys,
        ICourseStructureService courseStructure,
        IEnrollmentStore enrollments,
        ILearnerProgressService progress,
        IEnumerable<ILearningSurveyItemResultProvider> providers,
        IHttpContextAccessor httpContextAccessor,
        ILogger<LearningSurveyResultObserver> logger)
    {
        _learningSurveys = learningSurveys;
        _courseStructure = courseStructure;
        _enrollments = enrollments;
        _progress = progress;
        _providers = providers;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task CompletedAsync(SurveyResponseEventContext context)
        => RecordAsync(context, deleted: false);

    /// <inheritdoc />
    public Task GradedAsync(SurveyResponseEventContext context)
        => RecordAsync(context, deleted: false);

    /// <inheritdoc />
    public Task ScoreChangedAsync(SurveyResponseEventContext context)
        => RecordAsync(context, deleted: false);

    /// <inheritdoc />
    public Task VoidedAsync(SurveyResponseEventContext context)
        => RecordAsync(context, deleted: false);

    /// <inheritdoc />
    public Task DeletedAsync(SurveyResponseEventContext context)
        => RecordAsync(context, deleted: true);

    private async Task RecordAsync(SurveyResponseEventContext context, bool deleted)
    {
        var response = context?.Response;

        if (response is null || response.IsTest || response.Context is not { } responseContext)
        {
            return;
        }

        var item = await _learningSurveys.FindOwnerItemAsync(context.Survey);

        if (item is null)
        {
            return;
        }

        var courseId = _courseStructure.GetCourseId(item);
        var course = await _courseStructure.GetCourseAsync(courseId);
        var entry = course is null ? null : (await _courseStructure.GetOutlineAsync(course)).Find(item.ContentItemId);
        var provider = _providers.FirstOrDefault(candidate => string.Equals(candidate.Kind, entry?.Part?.Kind, StringComparison.Ordinal));

        if (provider is null)
        {
            return;
        }

        var enrollment = await FindEnrollmentAsync(responseContext, courseId);

        if (enrollment is null)
        {
            return;
        }

        // The stored list may not hold this response's latest state yet: this response is taken as the event gives it, and
        // a deleted or voided response does not count.
        var others = string.Equals(responseContext.Kind, LearningSurveyContexts.EnrollmentKind, StringComparison.Ordinal)
            ? await _learningSurveys.ListEnrollmentResponsesAsync(context.Survey.ItemId, enrollment.ItemId)
            : [];
        var responses = others
            .Where(candidate => candidate.ItemId != response.ItemId)
            .Append(response)
            .Where(candidate => candidate.Status != SurveyResponseStatus.Voided && (candidate.ItemId != response.ItemId || !deleted))
            .ToList();

        var result = provider.Build(entry.Item ?? item, enrollment, context.Version?.Definition, responses);

        if (result is null)
        {
            return;
        }

        var update = await _progress.RecordWithRetryAsync(enrollment, result);

        if (update?.RefusedReason is not null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("The result of item {ItemId} was not recorded on enrollment {EnrollmentId}: {Reason}.", item.ContentItemId, enrollment.ItemId, update.RefusedReason);
        }
    }

    private async Task<Enrollment> FindEnrollmentAsync(RecordRef responseContext, string courseId)
    {
        if (string.Equals(responseContext.Kind, LearningSurveyContexts.EnrollmentKind, StringComparison.Ordinal))
        {
            var enrollment = await _enrollments.FindByIdAsync(responseContext.Id);

            return enrollment is not null && string.Equals(enrollment.CourseId, courseId, StringComparison.Ordinal) ? enrollment : null;
        }

        // An anonymous course survey keeps no enrollment: the learner who just completed it is the signed-in user of the
        // request that completed it, and nothing that links them to the answers is stored.
        if (string.Equals(responseContext.Kind, LearningSurveyContexts.CourseKind, StringComparison.Ordinal)
            && string.Equals(responseContext.Id, courseId, StringComparison.Ordinal))
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            var own = await _enrollments.ListByCourseForUserAsync(userId, courseId);

            return own.FirstOrDefault(candidate => string.Equals(candidate.UserId, userId, StringComparison.Ordinal) && candidate.Status == EnrollmentStatus.Active)
                ?? own.FirstOrDefault(candidate => string.Equals(candidate.UserId, userId, StringComparison.Ordinal) && candidate.Status == EnrollmentStatus.Completed);
        }

        return null;
    }
}
