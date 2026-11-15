using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Programs.Core.Targets;

/// <summary>
/// Requires a course: the person is enrolled with the due date, and completes it by completing the course.
/// </summary>
public sealed class CourseRequiredLearningTarget : IRequiredLearningTarget
{
    private const int MaxOptions = 500;

    private readonly CourseLookup _courses;
    private readonly IContentManager _contentManager;
    private readonly IEnrollmentStore _enrollments;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    public CourseRequiredLearningTarget(CourseLookup courses, IContentManager contentManager, IEnrollmentStore enrollments, IServiceProvider services, IStringLocalizer<CourseRequiredLearningTarget> stringLocalizer)
    {
        _courses = courses;
        _contentManager = contentManager;
        _enrollments = enrollments;
        _services = services;
        S = stringLocalizer;
    }

    public string Kind => ProgramsConstants.TargetKinds.Course;

    public string DisplayName => S["Course"];

    public async Task<IReadOnlyList<RequiredLearningTargetOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
        => (await _courses.ListCoursesAsync(MaxOptions, cancellationToken)).Select(course => new RequiredLearningTargetOption(course.ContentItemId, course.DisplayText)).ToArray();

    public async Task<string> GetDisplayTextAsync(string targetId, CancellationToken cancellationToken = default)
    {
        var item = await _contentManager.GetAsync(targetId, VersionOptions.Latest);

        return item is not null && item.Has<CoursePart>() ? item.DisplayText ?? targetId : null;
    }

    public Task StartAsync(string targetId, string userId, DateTime? dueUtc, string sourceId, string actor, CancellationToken cancellationToken = default)
        => _services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = targetId,
            Source = ProgramsConstants.RequirementSource,
            SourceId = sourceId,
            DueUtc = dueUtc,
            Actor = actor,
        }, cancellationToken);

    public async Task<RequiredLearningTargetStatus> GetStatusAsync(string targetId, string userId, CancellationToken cancellationToken = default)
    {
        var enrollments = await _enrollments.ListByCourseForUserAsync(userId, targetId, cancellationToken);
        var completed = enrollments.Where(enrollment => enrollment.Status == EnrollmentStatus.Completed).Max(enrollment => enrollment.CompletedUtc);

        return new RequiredLearningTargetStatus(
            enrollments.Any(enrollment => enrollment.StartedUtc is not null || enrollment.Status == EnrollmentStatus.Completed),
            completed);
    }
}

/// <summary>
/// Requires a learning path: the person is started on it with the due date, and completes it by completing the path.
/// </summary>
public sealed class LearningPathRequiredLearningTarget : IRequiredLearningTarget
{
    private const int MaxOptions = 500;

    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    public LearningPathRequiredLearningTarget(ISession session, IContentManager contentManager, IServiceProvider services, IStringLocalizer<LearningPathRequiredLearningTarget> stringLocalizer)
    {
        _session = session;
        _contentManager = contentManager;
        _services = services;
        S = stringLocalizer;
    }

    public string Kind => ProgramsConstants.TargetKinds.LearningPath;

    public string DisplayName => S["Learning path"];

    public async Task<IReadOnlyList<RequiredLearningTargetOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
        => (await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == ProgramsConstants.LearningPathContentType && index.Published)
                .OrderBy(index => index.DisplayText)
                .Take(MaxOptions)
                .ListAsync(cancellationToken))
            .Select(path => new RequiredLearningTargetOption(path.ContentItemId, path.DisplayText))
            .ToArray();

    public async Task<string> GetDisplayTextAsync(string targetId, CancellationToken cancellationToken = default)
    {
        var item = await _contentManager.GetAsync(targetId, VersionOptions.Latest);

        return item is not null && item.Has<LearningPathPart>() ? item.DisplayText ?? targetId : null;
    }

    public Task StartAsync(string targetId, string userId, DateTime? dueUtc, string sourceId, string actor, CancellationToken cancellationToken = default)
        => _services.GetRequiredService<ILearningPathService>().StartAsync(new LearningPathStartRequest
        {
            PathId = targetId,
            UserId = userId,
            Source = ProgramsConstants.RequirementSource,
            SourceId = sourceId,
            DueUtc = dueUtc,
            Actor = actor,
        }, cancellationToken);

    public async Task<RequiredLearningTargetStatus> GetStatusAsync(string targetId, string userId, CancellationToken cancellationToken = default)
    {
        var progress = await _services.GetRequiredService<ILearningPathService>().FindProgressAsync(targetId, userId, cancellationToken);

        return new RequiredLearningTargetStatus(
            progress is not null && (progress.CompletedCourseIds.Count > 0 || progress.State == LearningPathState.Completed),
            progress?.State == LearningPathState.Completed ? progress.CompletedUtc : null);
    }
}

/// <summary>
/// Requires a certification path: the person is started on it, and completes it by holding the certificate.
/// </summary>
public sealed class CertificationPathRequiredLearningTarget : IRequiredLearningTarget
{
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    public CertificationPathRequiredLearningTarget(IServiceProvider services, IStringLocalizer<CertificationPathRequiredLearningTarget> stringLocalizer)
    {
        _services = services;
        S = stringLocalizer;
    }

    public string Kind => ProgramsConstants.TargetKinds.CertificationPath;

    public string DisplayName => S["Certification path"];

    public async Task<IReadOnlyList<RequiredLearningTargetOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var options = new List<RequiredLearningTargetOption>();

        foreach (var path in await Paths.ListAsync(cancellationToken))
        {
            options.Add(new RequiredLearningTargetOption(path.ItemId, await NameAsync(path, cancellationToken)));
        }

        return options.OrderBy(option => option.DisplayText, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public async Task<string> GetDisplayTextAsync(string targetId, CancellationToken cancellationToken = default)
    {
        var path = await Paths.FindByIdAsync(targetId, cancellationToken);

        return path is null ? null : await NameAsync(path, cancellationToken);
    }

    public Task StartAsync(string targetId, string userId, DateTime? dueUtc, string sourceId, string actor, CancellationToken cancellationToken = default)
        => Paths.StartAsync(targetId, userId, actor, cancellationToken);

    public async Task<RequiredLearningTargetStatus> GetStatusAsync(string targetId, string userId, CancellationToken cancellationToken = default)
    {
        var progress = await Paths.FindProgressAsync(targetId, userId, cancellationToken);
        var certified = progress?.State is CertificationPathState.Certified or CertificationPathState.RenewalOpen;

        var issued = progress?.Cycles.Where(cycle => cycle.CompletedUtc is not null).Max(cycle => cycle.CompletedUtc);

        return new RequiredLearningTargetStatus(progress is not null, certified ? issued ?? progress.ModifiedUtc ?? progress.CreatedUtc : null);
    }

    private ICertificationPathService Paths => _services.GetRequiredService<ICertificationPathService>();

    private async Task<string> NameAsync(CertificationPath path, CancellationToken cancellationToken)
    {
        var certifications = _services.GetService<CrestApps.OrchardCore.Skills.Services.ICertificationManager>();
        var certification = certifications is null ? null : await certifications.FindByIdAsync(path.CertificationId, cancellationToken);

        return certification?.Name ?? path.CertificationId;
    }
}
