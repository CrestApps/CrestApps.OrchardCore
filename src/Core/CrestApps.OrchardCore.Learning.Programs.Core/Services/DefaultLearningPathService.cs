using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Programs.Core.Services;

/// <summary>
/// Starts people on learning paths and moves them through the stages from their enrollments. Each change is saved
/// under a lock per path and person before any course enrollment, because enrolling commits the session.
/// </summary>
public sealed class DefaultLearningPathService : ILearningPathService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private readonly LearningPathProgressStore _progress;
    private readonly IContentManager _contentManager;
    private readonly IEnrollmentStore _enrollmentStore;
    private readonly IServiceProvider _services;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultLearningPathService"/> class.
    /// </summary>
    public DefaultLearningPathService(
        LearningPathProgressStore progress,
        IContentManager contentManager,
        IEnrollmentStore enrollmentStore,
        IServiceProvider services,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        ILogger<DefaultLearningPathService> logger,
        IStringLocalizer<DefaultLearningPathService> stringLocalizer)
    {
        _progress = progress;
        _contentManager = contentManager;
        _enrollmentStore = enrollmentStore;
        _services = services;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<LearningPathStartResult> StartAsync(LearningPathStartRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.UserId))
        {
            return new LearningPathStartResult(null, false, S["Choose who starts the path."]);
        }

        var path = await LoadPathAsync(request.PathId);

        if (path is null)
        {
            return new LearningPathStartResult(null, false, S["The learning path was not found or is not published."]);
        }

        if (LearningPathRules.CourseIds(path).Count == 0)
        {
            return new LearningPathStartResult(null, false, S["The learning path has no courses."]);
        }

        LearningPathProgress progress;
        var created = false;
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey(request.PathId, request.UserId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return new LearningPathStartResult(null, false, S["Someone else is changing this person's path. Try again in a moment."]);
        }

        await using (locker)
        {
            progress = await FindFreshAsync(request.PathId, request.UserId, cancellationToken);

            if (progress is null)
            {
                var completed = await CompletedCoursesAsync(request.UserId, cancellationToken);
                var open = LearningPathRules.OpenStage(path, completed);
                var stages = LearningPathRules.Stages(path).Count;

                progress = new LearningPathProgress
                {
                    PathId = request.PathId,
                    UserId = request.UserId,
                    Source = request.Source,
                    SourceId = request.SourceId,
                    DueUtc = request.DueUtc,
                    StartedBy = request.Actor,
                    StartedUtc = _clock.UtcNow,
                    OpenedStage = Math.Min(open, Math.Max(0, stages - 1)),
                    CompletedCourseIds = LearningPathRules.CourseIds(path).Where(completed.Contains).ToList(),
                    State = open >= stages ? LearningPathState.Completed : LearningPathState.InProgress,
                    CompletedUtc = open >= stages ? _clock.UtcNow : null,
                };

                await _progress.CreateAsync(progress, cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);
                created = true;
            }
        }

        if (created)
        {
            await AfterChangeAsync(path, progress, justCompleted: progress.State == LearningPathState.Completed, request.Actor, cancellationToken);
        }

        return new LearningPathStartResult(progress, created);
    }

    /// <inheritdoc/>
    public async Task<LearningPathProgress> EvaluateAsync(string pathId, string userId, CancellationToken cancellationToken = default)
    {
        var path = await LoadPathAsync(pathId);

        if (path is null || string.IsNullOrEmpty(userId))
        {
            return null;
        }

        LearningPathProgress progress;
        LearningPathState previous;
        var changed = false;
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey(pathId, userId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            _logger.LogWarning("The progress on a learning path could not be locked; it is evaluated on the next change.");

            return null;
        }

        await using (locker)
        {
            progress = await FindFreshAsync(pathId, userId, cancellationToken);

            if (progress is null || progress.State == LearningPathState.Completed)
            {
                return progress;
            }

            previous = progress.State;

            var completed = await CompletedCoursesAsync(userId, cancellationToken);
            var open = LearningPathRules.OpenStage(path, completed);
            var stages = LearningPathRules.Stages(path).Count;
            var completedIds = LearningPathRules.CourseIds(path).Where(completed.Contains).ToList();
            var opened = Math.Min(open, Math.Max(0, stages - 1));

            if (opened != progress.OpenedStage || !completedIds.SequenceEqual(progress.CompletedCourseIds) || open >= stages)
            {
                progress.OpenedStage = Math.Max(progress.OpenedStage, opened);
                progress.CompletedCourseIds = completedIds;

                if (open >= stages)
                {
                    progress.State = LearningPathState.Completed;
                    progress.CompletedUtc = _clock.UtcNow;
                }

                await _progress.UpdateAsync(progress, cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);
                changed = true;
            }
        }

        if (changed)
        {
            await AfterChangeAsync(path, progress, justCompleted: previous != LearningPathState.Completed && progress.State == LearningPathState.Completed, progress.StartedBy, cancellationToken);
        }

        return progress;
    }

    /// <inheritdoc/>
    public async Task EvaluateCourseAsync(string userId, string courseId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(courseId))
        {
            return;
        }

        foreach (var progress in await _progress.ListByUserAsync(userId, cancellationToken))
        {
            if (progress.State != LearningPathState.InProgress)
            {
                continue;
            }

            var path = await LoadPathAsync(progress.PathId);

            if (path is not null && LearningPathRules.CourseIds(path).Contains(courseId))
            {
                await EvaluateAsync(progress.PathId, userId, cancellationToken);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SupportsIssuingAsync(LearningPathProgress progress, CancellationToken cancellationToken = default)
    {
        if (progress?.CompletedUtc is null || string.IsNullOrEmpty(progress.UserId))
        {
            return false;
        }

        var path = await LoadPathAsync(progress.PathId);

        if (path is null)
        {
            return false;
        }

        var evidence = (await _enrollmentStore.ListByUserAsync(progress.UserId, cancellationToken))
            .Where(CompletionEvidence.SupportsIssuing)
            .Select(enrollment => enrollment.CourseId)
            .ToHashSet(StringComparer.Ordinal);

        return LearningPathRules.OpenStage(path, evidence) >= LearningPathRules.Stages(path).Count;
    }

    /// <inheritdoc/>
    public Task<LearningPathProgress> FindProgressAsync(string pathId, string userId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(pathId) || string.IsNullOrEmpty(userId) ? Task.FromResult<LearningPathProgress>(null) : FindFreshAsync(pathId, userId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<LearningPathProgress>> ListForUserAsync(string userId, CancellationToken cancellationToken = default)
        => _progress.ListByUserAsync(userId, cancellationToken);

    /// <inheritdoc/>
    public Task<(int Count, IReadOnlyList<LearningPathProgress> Items)> PageAsync(string pathId, LearningPathState? state, int skip, int take, CancellationToken cancellationToken = default)
        => _progress.PageAsync(pathId, state, skip, take, cancellationToken);

    private async Task AfterChangeAsync(LearningPathPart path, LearningPathProgress progress, bool justCompleted, string actor, CancellationToken cancellationToken)
    {
        if (progress.State == LearningPathState.InProgress)
        {
            await EnrollOpenCoursesAsync(path, progress, actor, cancellationToken);
        }

        if (justCompleted)
        {
            foreach (var observer in _services.GetServices<ILearningPathObserver>())
            {
                await observer.CompletedAsync(progress, cancellationToken);
            }
        }
    }

    private async Task EnrollOpenCoursesAsync(LearningPathPart path, LearningPathProgress progress, string actor, CancellationToken cancellationToken)
    {
        var enrollmentService = _services.GetRequiredService<IEnrollmentService>();
        var completed = progress.CompletedCourseIds.ToHashSet(StringComparer.Ordinal);

        foreach (var courseId in LearningPathRules.CourseIdsThrough(path, progress.OpenedStage).Where(courseId => !completed.Contains(courseId)))
        {
            var result = await enrollmentService.EnrollAsync(new EnrollmentRequest
            {
                UserId = progress.UserId,
                CourseId = courseId,
                Source = ProgramsConstants.PathSource,
                SourceId = progress.ItemId,
                DueUtc = progress.DueUtc,
                Actor = actor,
            }, cancellationToken);

            if (!string.IsNullOrEmpty(result.ErrorCode))
            {
                _logger.LogWarning("A learning path could not enroll a person in one of its courses: {ErrorCode}.", result.ErrorCode);
            }
        }
    }

    private async Task<HashSet<string>> CompletedCoursesAsync(string userId, CancellationToken cancellationToken)
        => (await _enrollmentStore.ListByUserAsync(userId, cancellationToken))
            .Where(enrollment => enrollment.Status == EnrollmentStatus.Completed)
            .Select(enrollment => enrollment.CourseId)
            .ToHashSet(StringComparer.Ordinal);

    private async Task<LearningPathPart> LoadPathAsync(string pathId)
    {
        if (string.IsNullOrEmpty(pathId))
        {
            return null;
        }

        var item = await _contentManager.GetAsync(pathId, VersionOptions.Published);

        if (item is null || !item.TryGet<LearningPathPart>(out var part))
        {
            return null;
        }

        // A hidden (unpublished) or deleted course cannot be taken, so it never holds the path back: the path is read
        // without it, as Moodle skips hidden courses of a learning plan.
        var courses = _services.GetRequiredService<ICourseStructureService>();
        var available = new HashSet<string>(StringComparer.Ordinal);

        foreach (var courseId in LearningPathRules.CourseIds(part))
        {
            if (await courses.GetCourseAsync(courseId) is not null)
            {
                available.Add(courseId);
            }
        }

        return LearningPathRules.WithAvailableCourses(part, available);
    }

    private async Task<LearningPathProgress> FindFreshAsync(string pathId, string userId, CancellationToken cancellationToken)
    {
        var progress = await _progress.FindAsync(pathId, userId, cancellationToken);

        if (progress is null)
        {
            return null;
        }

        // Another scope may have changed it since this session read it: read what was committed.
        _session.Detach(progress, ProgramsConstants.CollectionName);

        return await _progress.FindAsync(pathId, userId, cancellationToken);
    }

    private static string LockKey(string pathId, string userId)
        => $"LearningPath:{pathId}:{userId}";
}
