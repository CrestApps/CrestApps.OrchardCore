using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Cohorts;

/// <summary>
/// Hears that people joined or left a cohort, and enrolls them in, or takes them off, the courses and paths the cohort
/// is linked to. The work runs after the request, in its own scope, because enrolling commits the session.
/// </summary>
internal sealed class LearningCohortMembershipObserver : ICohortMembershipObserver
{
    public Task MembersAddedAsync(Cohort cohort, IReadOnlyList<string> userIds, string actor, CancellationToken cancellationToken = default)
    {
        var cohortId = cohort.CohortId;
        var people = userIds.ToArray();

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<CohortLearningSync>().MembersAddedAsync(cohortId, people, actor));

        return Task.CompletedTask;
    }

    public Task MembersRemovedAsync(Cohort cohort, IReadOnlyList<string> userIds, string actor, CancellationToken cancellationToken = default)
    {
        var cohortId = cohort.CohortId;
        var people = userIds.ToArray();

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<CohortLearningSync>().MembersRemovedAsync(cohortId, people, actor));

        return Task.CompletedTask;
    }
}

/// <summary>
/// Names the courses and paths a cohort is linked to, so the cohort is not deleted from under them. It reads the links
/// itself: the cohort service asks it, so it cannot need the cohort service.
/// </summary>
internal sealed class LearningCohortUsage : ICohortUsage
{
    private readonly global::YesSql.ISession _session;

    internal readonly IStringLocalizer S;

    public LearningCohortUsage(global::YesSql.ISession session, IStringLocalizer<LearningCohortUsage> stringLocalizer)
    {
        _session = session;
        S = stringLocalizer;
    }

    public async Task<IReadOnlyList<string>> ListUsesAsync(string cohortId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cohortId))
        {
            return [];
        }

        var links = await _session.Query<CohortLearningLink, CohortLearningLinkIndex>(index => index.CohortId == cohortId, LearningConstants.CollectionName).ListAsync(cancellationToken);

        return links.Select(link => S["the course or learning path '{0}'", link.DisplayText ?? link.TargetId].Value).ToArray();
    }
}

/// <summary>
/// Applies the cohorts of a course or path when it is published, and forgets them when it is deleted. The work runs
/// after the request, in its own scope, so the published version is committed and enrolling commits nothing else.
/// </summary>
internal sealed class LearningCohortsContentHandler : ContentHandlerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LearningCohortsContentHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task PublishedAsync(PublishContentContext context)
    {
        if (context.ContentItem.Has(nameof(LearningCohortsPart)) || context.PreviousItem?.Has(nameof(LearningCohortsPart)) == true)
        {
            var targetId = context.ContentItem.ContentItemId;
            var actor = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<CohortLearningSync>().ApplyAsync(targetId, actor));
        }

        return Task.CompletedTask;
    }

    public override Task RemovedAsync(RemoveContentContext context)
    {
        if (context.NoActiveVersionLeft)
        {
            var targetId = context.ContentItem.ContentItemId;

            ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<CohortLearningSync>().ForgetAsync(targetId));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Enrolls the people of a linked cohort in a course, each with a grant of their own per cohort, so leaving one cohort
/// takes away only its grant. Someone who completed the course is not enrolled again: joining a cohort is not a retake.
/// </summary>
internal sealed class CourseCohortLearningTarget : ICohortLearningTarget
{
    private readonly IEnrollmentService _enrollments;
    private readonly IEnrollmentStore _enrollmentStore;
    private readonly ILogger _logger;

    public CourseCohortLearningTarget(IEnrollmentService enrollments, IEnrollmentStore enrollmentStore, ILogger<CourseCohortLearningTarget> logger)
    {
        _enrollments = enrollments;
        _enrollmentStore = enrollmentStore;
        _logger = logger;
    }

    public string PartName => nameof(CoursePart);

    public async Task JoinAsync(string targetId, string cohortId, string userId, string actor, CancellationToken cancellationToken = default)
    {
        var enrollments = await _enrollmentStore.ListByCourseForUserAsync(userId, targetId, cancellationToken);

        if (!enrollments.Any(enrollment => enrollment.IsOpen) && enrollments.Any(enrollment => enrollment.Status == EnrollmentStatus.Completed))
        {
            return;
        }

        var result = await _enrollments.EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = targetId,
            Source = LearningConstants.EnrollmentSources.Cohort,
            SourceId = CohortEnrollmentGrants.SourceId(cohortId, targetId, userId),
            Actor = actor,
        }, cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning("The cohort sync could not enroll a person in a course: {ErrorCode}.", result.ErrorCode);
        }
    }

    public Task LeaveAsync(string targetId, string cohortId, string userId, bool inAnotherLinkedCohort, string actor, CancellationToken cancellationToken = default)
        => _enrollments.RevokeGrantAsync(
            LearningConstants.EnrollmentSources.Cohort,
            CohortEnrollmentGrants.SourceId(cohortId, targetId, userId),
            actor,
            "No longer in a cohort linked to the course",
            cancellationToken);
}
