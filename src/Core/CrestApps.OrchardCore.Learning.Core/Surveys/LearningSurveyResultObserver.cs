using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Records the result of a course item when a learner's response to its survey changes: completed, graded, rescored or
/// voided. The provider of the item's kind builds the result (a course survey is submitted; a quiz combines attempts).
/// The result of an anonymous survey is its completion only, recorded by the day only (the start of the day in the site's
/// time zone): no score or pass reaches the gradebook, and the learner's progress never gives the exact time that would
/// link them to their anonymous response. An anonymous course survey's response names no enrollment, so the learner is
/// the signed-in user of the request in which they completed it; every other event on it (graded, rescored, voided or
/// deleted, by a grader, a workflow or a task) is someone else's request and changes nobody's progress.
/// </summary>
public sealed class LearningSurveyResultObserver : ISurveyResponseObserver
{
    private readonly ILearningSurveyService _learningSurveys;
    private readonly ICourseStructureService _courseStructure;
    private readonly IEnrollmentStore _enrollments;
    private readonly ILearnerProgressService _progress;
    private readonly IEnumerable<ILearningSurveyItemResultProvider> _providers;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningSurveyResultObserver"/> class.
    /// </summary>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="courseStructure">The course structure service.</param>
    /// <param name="enrollments">The enrollment store.</param>
    /// <param name="progress">The learner progress service.</param>
    /// <param name="providers">The result providers by course item kind.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="serviceProvider">The services, for the survey versions of earlier attempts.</param>
    public LearningSurveyResultObserver(
        ILearningSurveyService learningSurveys,
        ICourseStructureService courseStructure,
        IEnrollmentStore enrollments,
        ILearnerProgressService progress,
        IEnumerable<ILearningSurveyItemResultProvider> providers,
        IHttpContextAccessor httpContextAccessor,
        ISiteService siteService,
        IClock clock,
        ILogger<LearningSurveyResultObserver> logger,
        IServiceProvider serviceProvider)
    {
        _learningSurveys = learningSurveys;
        _courseStructure = courseStructure;
        _enrollments = enrollments;
        _progress = progress;
        _providers = providers;
        _httpContextAccessor = httpContextAccessor;
        _siteService = siteService;
        _clock = clock;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public Task CompletedAsync(SurveyResponseEventContext context)
        => RecordAsync(context, deleted: false, byRespondent: context is { CausedByWorkflow: false } && string.IsNullOrEmpty(context.ActorId));

    /// <inheritdoc />
    public Task GradedAsync(SurveyResponseEventContext context)
        => RecordAsync(context, deleted: false, byRespondent: false);

    /// <inheritdoc />
    public Task ScoreChangedAsync(SurveyResponseEventContext context)
        => RecordAsync(context, deleted: false, byRespondent: false);

    /// <inheritdoc />
    public Task VoidedAsync(SurveyResponseEventContext context)
        => RecordAsync(context, deleted: false, byRespondent: false);

    /// <inheritdoc />
    public Task DeletedAsync(SurveyResponseEventContext context)
        => RecordAsync(context, deleted: true, byRespondent: false);

    private async Task RecordAsync(SurveyResponseEventContext context, bool deleted, bool byRespondent)
    {
        var response = context?.Response;

        if (response is null || response.IsTest || response.Context is not { } responseContext)
        {
            return;
        }

        var courseContext = string.Equals(responseContext.Kind, LearningSurveyContexts.CourseKind, StringComparison.Ordinal);

        // The learner of an anonymous course survey is known only in the request in which they completed it.
        if (courseContext && !byRespondent)
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

        var result = provider.Build(entry.Item ?? item, enrollment, await DefinitionOfLatestGradedAsync(context, responses), responses);

        if (result is null)
        {
            return;
        }

        // An anonymous survey's response keeps no enrollment; its completion reaches the progress by the day only, and
        // nothing else: a score next to the learner's name would match the score shown next to the anonymous label.
        if (context.Survey.Access?.Anonymity == AnonymityMode.Anonymous || courseContext)
        {
            result = CompletionOnly(result);
            result.AnonymousDayUtc = await StartOfTodayUtcAsync();
        }

        var update = await _progress.RecordWithRetryAsync(enrollment, result);

        if (update?.RefusedReason is not null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("The result of item {ItemId} was not recorded on enrollment {EnrollmentId}: {Reason}.", item.ContentItemId, enrollment.ItemId, update.RefusedReason);
        }
    }

    // The definition of the version the latest graded attempt ran on: a combined score that belongs to no one attempt
    // (an average) is judged against it, never against the version of whichever attempt changed. Each attempt also
    // carries the pass Surveys decided against its own version, which a method that picks one attempt takes.
    private async Task<SurveyDefinition> DefinitionOfLatestGradedAsync(SurveyResponseEventContext context, IReadOnlyList<SurveyResponse> responses)
    {
        var latest = responses
            .Where(candidate => candidate.Status == SurveyResponseStatus.Completed && candidate.Grading == GradingStatus.Graded && candidate.Percent is not null)
            .OrderByDescending(candidate => candidate.CompletedUtc ?? candidate.StartedUtc)
            .FirstOrDefault();

        if (latest is null
            || string.IsNullOrEmpty(latest.SurveyVersionId)
            || string.Equals(latest.SurveyVersionId, context.Version?.ItemId, StringComparison.Ordinal))
        {
            return context.Version?.Definition;
        }

        var version = _serviceProvider.GetService<ISurveyVersionCache>() is { } versions
            ? await versions.GetAsync(latest.SurveyVersionId)
            : null;

        return version?.Definition ?? context.Version?.Definition;
    }

    /// <summary>
    /// Keeps only the completion of a result, for an anonymous survey: no score and no pass, and the work counts as handed
    /// in and graded once a response is completed, because a later grade of an anonymous response reaches nobody.
    /// </summary>
    /// <param name="result">The result the item's provider built.</param>
    /// <returns>The completion-only result.</returns>
    public static CourseItemResult CompletionOnly(CourseItemResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var done = result.Submitted || result.Graded;

        return new CourseItemResult
        {
            ItemId = result.ItemId,
            PassDecided = true,
            CompletedWithoutPassDecision = done,
            Submitted = done,
            Graded = done,
            Attempts = result.Attempts,
        };
    }

    private async Task<DateTime> StartOfTodayUtcAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();

        return StartOfDayUtc(_clock.UtcNow, site?.TimeZoneId);
    }

    /// <summary>
    /// Gets the start of the day a time falls on in a time zone, as UTC.
    /// </summary>
    /// <param name="utc">The time, in UTC.</param>
    /// <param name="timeZoneId">The time zone id; UTC when empty or unknown.</param>
    /// <returns>The start of the day, in UTC.</returns>
    public static DateTime StartOfDayUtc(DateTime utc, string timeZoneId)
    {
        var zone = CalendarTime.GetZone(timeZoneId);
        var day = NodaTime.Instant.FromDateTimeUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).InZone(zone).Date;

        return zone.AtStartOfDay(day).ToDateTimeUtc();
    }

    private async Task<Enrollment> FindEnrollmentAsync(RecordRef responseContext, string courseId)
    {
        if (string.Equals(responseContext.Kind, LearningSurveyContexts.EnrollmentKind, StringComparison.Ordinal))
        {
            var enrollment = await _enrollments.FindByIdAsync(responseContext.Id);

            return enrollment is not null && string.Equals(enrollment.CourseId, courseId, StringComparison.Ordinal) ? enrollment : null;
        }

        // An anonymous course survey keeps no enrollment: the learner who just completed it is the signed-in user of the
        // request that completed it (only a completion the respondent caused gets here), and nothing that links them to
        // the answers is stored.
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
