using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Services;

/// <summary>
/// Certification paths and progress. Every change to a person's progress runs under one lock per path and person and
/// is committed inside it; the Skills issuer is idempotent by progress and cycle, so a completion delivered twice, or
/// the upkeep catching up after it, issues one certificate.
/// </summary>
public sealed class DefaultCertificationPathService : ICertificationPathService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private static readonly CertificationPathState[] _openStates = [CertificationPathState.InProgress, CertificationPathState.RenewalOpen, CertificationPathState.Lapsed];

    private readonly CertificationPathStore _paths;
    private readonly CertificationPathProgressStore _progress;
    private readonly IEnumerable<ICertificationRequirement> _requirements;
    private readonly ICertificateIssuer _issuer;
    private readonly ICertificationManager _certifications;
    private readonly IEnrollmentService _enrollmentService;
    private readonly EnrollmentStore _enrollments;
    private readonly CertificateHolderResolver _holders;
    private readonly ILeasedWorkQueue<CertificationRenewalWork> _renewals;
    private readonly IEnumerable<ICertificationPathObserver> _observers;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCertificationPathService"/> class.
    /// </summary>
    public DefaultCertificationPathService(
        CertificationPathStore paths,
        CertificationPathProgressStore progress,
        IEnumerable<ICertificationRequirement> requirements,
        ICertificateIssuer issuer,
        ICertificationManager certifications,
        IEnrollmentService enrollmentService,
        EnrollmentStore enrollments,
        CertificateHolderResolver holders,
        ILeasedWorkQueue<CertificationRenewalWork> renewals,
        IEnumerable<ICertificationPathObserver> observers,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        ILocalClock localClock,
        ILogger<DefaultCertificationPathService> logger,
        IStringLocalizer<DefaultCertificationPathService> stringLocalizer)
    {
        _paths = paths;
        _progress = progress;
        _requirements = requirements;
        _issuer = issuer;
        _certifications = certifications;
        _enrollmentService = enrollmentService;
        _enrollments = enrollments;
        _holders = holders;
        _renewals = renewals;
        _observers = observers;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
        _localClock = localClock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task<CertificationPath> FindByCertificationAsync(string certificationId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(certificationId) ? Task.FromResult<CertificationPath>(null) : _paths.FindByCertificationAsync(certificationId, cancellationToken);

    /// <inheritdoc/>
    public async Task<CertificationPath> FindByIdAsync(string pathId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(pathId) ? null : await _paths.FindByIdAsync(pathId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<CertificationPath>> ListByTargetAsync(string targetId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(targetId) ? Task.FromResult<IReadOnlyList<CertificationPath>>([]) : _paths.ListByTargetAsync(targetId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<CertificationPath>> ListAsync(CancellationToken cancellationToken = default)
        => _paths.ListAllAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<CertificationPathResult> SaveAsync(CertificationPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var result = new CertificationPathResult();
        var kinds = _requirements.Select(requirement => requirement.Kind).ToHashSet(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(path.CertificationId) || await _certifications.FindByIdAsync(path.CertificationId, cancellationToken) is null)
        {
            result.Fail(S["Choose the certification the path earns."]);
        }

        foreach (var requirement in path.InitialRequirements.Concat(path.RenewalRequirements))
        {
            requirement.Id = string.IsNullOrEmpty(requirement.Id) ? Guid.NewGuid().ToString("n")[..12] : requirement.Id;

            if (!kinds.Contains(requirement.Kind ?? string.Empty))
            {
                result.Fail(S["The requirement kind '{0}' is not available.", requirement.Kind ?? string.Empty]);
            }

            if (string.IsNullOrEmpty(requirement.TargetId))
            {
                result.Fail(S["Every requirement needs a course or a quiz."]);
            }
        }

        if (path.Enabled && path.InitialRequirements.Count == 0)
        {
            result.Fail(S["A path in use needs at least one requirement."]);
        }

        path.RenewalWindowDays = Math.Max(0, path.RenewalWindowDays);
        path.GracePeriodDays = Math.Max(0, path.GracePeriodDays);
        path.MinimumIntervalMonths = Math.Max(0, path.MinimumIntervalMonths);
        path.FixedMonth = Math.Clamp(path.FixedMonth, 1, 12);
        path.FixedDay = Math.Clamp(path.FixedDay, 1, 31);

        if (!result.Succeeded)
        {
            return result;
        }

        var other = await _paths.FindByCertificationAsync(path.CertificationId, cancellationToken);

        if (other is not null && other.ItemId != path.ItemId)
        {
            result.Fail(S["The certification already has a path."]);

            return result;
        }

        if (string.IsNullOrEmpty(path.ItemId) || await _paths.FindByIdAsync(path.ItemId, cancellationToken) is null)
        {
            path.CreatedUtc = _clock.UtcNow;
            await _paths.CreateAsync(path, cancellationToken);
        }
        else
        {
            path.ModifiedUtc = _clock.UtcNow;
            await _paths.UpdateAsync(path, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<CertificationPathResult> StartAsync(string pathId, string userId, string actor, CancellationToken cancellationToken = default)
    {
        var result = new CertificationPathResult();
        var path = await FindEnabledAsync(pathId, result, cancellationToken);

        if (path is null || string.IsNullOrEmpty(userId))
        {
            if (string.IsNullOrEmpty(userId))
            {
                result.Fail(S["Choose who starts the path."]);
            }

            return result;
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey(path.ItemId, userId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            result.Fail(S["Someone else is changing this person's certification. Try again in a moment."]);

            return result;
        }

        await using (locker)
        {
            var progress = await _progress.FindFreshAsync(path.ItemId, userId, cancellationToken);
            var today = await TodayAsync();

            if (progress is null)
            {
                progress = NewProgress(path, userId, actor);
                await _progress.CreateAsync(progress, cancellationToken);
            }
            else if (progress.State == CertificationPathState.Revoked || IsPastGrace(path, progress, today))
            {
                StartCycle(progress, renewal: false);
                progress.State = CertificationPathState.InProgress;
                progress.StartedBy = actor;
            }

            result.Progress = progress;
            await EvaluateCoreAsync(path, progress, today, result, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<CertificationPathResult> EvaluateAsync(string pathId, string userId, CancellationToken cancellationToken = default)
    {
        var result = new CertificationPathResult();
        var path = await FindEnabledAsync(pathId, result, cancellationToken);

        if (path is null || string.IsNullOrEmpty(userId))
        {
            return result;
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey(path.ItemId, userId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            result.Fail(S["Someone else is changing this person's certification. Try again in a moment."]);

            return result;
        }

        await using (locker)
        {
            var progress = await _progress.FindFreshAsync(path.ItemId, userId, cancellationToken);
            var today = await TodayAsync();

            if (progress is null)
            {
                // A person joins a path on their own only by meeting one of its first requirements.
                if (!path.StartsOnFirstRequirement || !await MeetsAnyAsync(path, userId, cancellationToken))
                {
                    return result;
                }

                progress = NewProgress(path, userId, CertificationsConstants.Source);
                await _progress.CreateAsync(progress, cancellationToken);
            }

            result.Progress = progress;
            await EvaluateCoreAsync(path, progress, today, result, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<CertificationPathResult> OpenRenewalAsync(string progressId, CancellationToken cancellationToken = default)
    {
        var result = new CertificationPathResult();
        var found = await _progress.FindFreshByIdAsync(progressId, cancellationToken);

        if (found is null)
        {
            return result;
        }

        var path = await FindEnabledAsync(found.PathId, result, cancellationToken);

        if (path is null)
        {
            return result;
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey(path.ItemId, found.UserId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            result.Fail(S["Someone else is changing this person's certification. Try again in a moment."]);

            return result;
        }

        CertificationPathProgress progress;

        await using (locker)
        {
            progress = await _progress.FindFreshByIdAsync(progressId, cancellationToken);
            result.Progress = progress;

            var today = await TodayAsync();

            if (progress is null || progress.State != CertificationPathState.Certified || progress.RenewalOpensOn is not { } opensOn || opensOn > today)
            {
                return result;
            }

            var enrollmentIds = new List<string>();
            var cycleNumber = Math.Max(progress.Cycle, progress.Cycles.Count) + 1;

            if (path.EnrollOnRenewal)
            {
                foreach (var requirement in path.RequirementsFor(renewal: true).Where(requirement => requirement.Kind == CertificationsConstants.RequirementKinds.CourseCompleted))
                {
                    var enrollmentId = await EnrollForRenewalAsync(progress, requirement.TargetId, cycleNumber, cancellationToken);

                    if (enrollmentId is not null)
                    {
                        enrollmentIds.Add(enrollmentId);
                    }
                }

                // Each enrollment commits, and a commit makes the session forget the progress it read.
                progress = await _progress.FindFreshByIdAsync(progressId, cancellationToken);
                result.Progress = progress;
            }

            var cycle = StartCycle(progress, renewal: true);

            cycle.EnrollmentIds.AddRange(enrollmentIds);
            progress.State = CertificationPathState.RenewalOpen;
            progress.ModifiedUtc = _clock.UtcNow;
            await _progress.UpdateAsync(progress, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }

        await NotifyAsync(observer => observer.RenewalOpenedAsync(progress, path, cancellationToken), progress);

        return result;
    }

    /// <inheritdoc/>
    public async Task<CertificationPathProgress> FindProgressAsync(string pathId, string userId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(pathId) || string.IsNullOrEmpty(userId) ? null : await _progress.FindFreshAsync(pathId, userId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<CertificationPathProgress>> ListProgressForUserAsync(string userId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(userId) ? Task.FromResult<IReadOnlyList<CertificationPathProgress>>([]) : _progress.ListForUserAsync(userId, cancellationToken);

    /// <summary>
    /// Moves the progress of an expired certificate to lapsed.
    /// </summary>
    /// <param name="recordId">The certificate that expired.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    internal async Task LapseAsync(string recordId, CancellationToken cancellationToken = default)
    {
        foreach (var id in (await _progress.ListByRecordAsync(recordId, cancellationToken)).Select(item => item.ItemId).ToArray())
        {
            // Each change commits, and a commit makes the session forget what it read: read each one again.
            var progress = await _progress.FindFreshByIdAsync(id, cancellationToken);

            if (progress?.State is not (CertificationPathState.Certified or CertificationPathState.RenewalOpen))
            {
                continue;
            }

            var path = await _paths.FindByIdAsync(progress.PathId, cancellationToken);

            progress.State = CertificationPathState.Lapsed;
            progress.ModifiedUtc = _clock.UtcNow;
            await _progress.UpdateAsync(progress, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            if (path is not null)
            {
                await NotifyAsync(observer => observer.LapsedAsync(progress, path, cancellationToken), progress);
            }
        }
    }

    /// <summary>
    /// Moves the progress of a revoked certificate to revoked.
    /// </summary>
    /// <param name="recordId">The certificate that was revoked.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    internal async Task RevokeAsync(string recordId, CancellationToken cancellationToken = default)
    {
        foreach (var progress in await _progress.ListByRecordAsync(recordId, cancellationToken))
        {
            progress.State = CertificationPathState.Revoked;
            progress.ModifiedUtc = _clock.UtcNow;
            await _progress.UpdateAsync(progress, cancellationToken);
        }

        await _session.SaveChangesAsync(cancellationToken);
    }

    private async Task EvaluateCoreAsync(CertificationPath path, CertificationPathProgress progress, DateOnly today, CertificationPathResult result, CancellationToken cancellationToken)
    {
        if (progress.State is CertificationPathState.Revoked or CertificationPathState.Certified)
        {
            return;
        }

        // Past the grace period a renewal is no longer possible: the person starts over with the first requirements.
        if (IsPastGrace(path, progress, today))
        {
            StartCycle(progress, renewal: false);
            progress.State = CertificationPathState.InProgress;
        }

        var cycle = progress.CurrentCycle() ?? StartCycle(progress, renewal: false);

        // A certificate that lapsed before its renewal opened (no renewal window) is renewed in a cycle of its own.
        if (progress.State == CertificationPathState.Lapsed && !cycle.Renewal)
        {
            cycle = StartCycle(progress, renewal: true);
        }

        var requirements = path.RequirementsFor(cycle.Renewal);

        if (requirements.Count == 0)
        {
            return;
        }

        var since = cycle.Number == 1 ? (DateTime?)null : cycle.StartedUtc;
        var evidence = new List<CertificationEvidence>();

        foreach (var requirement in requirements)
        {
            var kind = _requirements.FirstOrDefault(candidate => candidate.Kind == requirement.Kind);
            var found = kind is null
                ? null
                : await kind.FindEvidenceAsync(new CertificationRequirementContext { UserId = progress.UserId, Requirement = requirement, SinceUtc = since }, cancellationToken);

            if (found is not null)
            {
                found.RequirementId = requirement.Id;
                evidence.Add(found);
            }
        }

        cycle.Evidence = evidence;
        progress.ModifiedUtc = _clock.UtcNow;

        var met = path.Join == RequirementJoin.Any ? evidence.Count > 0 : evidence.Count == requirements.Count;

        if (!met)
        {
            await _progress.UpdateAsync(progress, cancellationToken);

            return;
        }

        var certification = await _certifications.FindByIdAsync(path.CertificationId, cancellationToken);

        if (certification is null)
        {
            result.Fail(S["The certification of the path no longer exists."]);

            return;
        }

        var (subject, name) = await _holders.ResolveAsync(progress.UserId, cancellationToken);
        var renewing = cycle.Renewal && !string.IsNullOrEmpty(progress.CurrentRecordId);
        var request = new CertificateIssueRequest
        {
            CertificationId = certification.ItemId,
            Subject = subject,
            SubjectName = name,
            Source = CertificationsConstants.Source,
            SourceId = $"{progress.ItemId}:{cycle.Number}",
            Evidence = string.Join("; ", evidence.Select(item => item.Title).Where(title => !string.IsNullOrWhiteSpace(title))),
            IssuedOn = today,
            ExpiresOn = CertificationDates.Expiry(path, certification.ValidityMonths, today, renewing ? progress.ExpiresOn : null),
            Actor = CertificationsConstants.Source,
        };

        // The issuer commits inside its own lock, and a commit makes the session forget the documents it read: the
        // progress is saved first, so it is committed with the certificate, and read again afterwards.
        await _progress.UpdateAsync(progress, cancellationToken);

        var issued = renewing
            ? await _issuer.RenewAsync(progress.CurrentRecordId, request, cancellationToken)
            : await _issuer.IssueAsync(request, cancellationToken);

        progress = await _progress.FindFreshByIdAsync(progress.ItemId, cancellationToken);
        result.Progress = progress;

        if (!issued.Succeeded)
        {
            foreach (var error in issued.Errors)
            {
                result.Fail(error);
            }

            return;
        }

        cycle = progress.Cycles.Single(item => item.Number == cycle.Number);
        cycle.CompletedUtc = _clock.UtcNow;
        cycle.RecordId = issued.Record.ItemId;
        progress.CurrentRecordId = issued.Record.ItemId;
        progress.ExpiresOn = issued.Record.ExpiresOn;
        progress.RenewalOpensOn = issued.Record.ExpiresOn?.AddDays(-path.RenewalWindowDays);
        progress.State = CertificationPathState.Certified;
        progress.ModifiedUtc = _clock.UtcNow;
        result.IssuedRecordId = issued.AlreadyIssued ? null : issued.Record.ItemId;

        await _progress.UpdateAsync(progress, cancellationToken);

        if (progress.RenewalOpensOn is { } opensOn)
        {
            await _renewals.EnqueueAsync(new CertificationRenewalWork
            {
                ProgressId = progress.ItemId,
                NextAttemptUtc = await StartOfDayUtcAsync(opensOn),
            }, cancellationToken);
        }
    }

    private async Task<bool> MeetsAnyAsync(CertificationPath path, string userId, CancellationToken cancellationToken)
    {
        foreach (var requirement in path.RequirementsFor(renewal: false))
        {
            var kind = _requirements.FirstOrDefault(candidate => candidate.Kind == requirement.Kind);

            if (kind is not null && await kind.FindEvidenceAsync(new CertificationRequirementContext { UserId = userId, Requirement = requirement }, cancellationToken) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<string> EnrollForRenewalAsync(CertificationPathProgress progress, string courseId, int cycle, CancellationToken cancellationToken)
    {
        var existing = await _enrollments.ListByCourseForUserAsync(progress.UserId, courseId, cancellationToken);
        var open = existing.FirstOrDefault(enrollment => enrollment.IsOpen);

        if (open is not null)
        {
            return open.ItemId;
        }

        var previous = existing.OrderByDescending(enrollment => enrollment.CreatedUtc).FirstOrDefault();
        var enrolled = await _enrollmentService.EnrollAsync(new EnrollmentRequest
        {
            UserId = progress.UserId,
            CourseId = courseId,
            Source = CertificationsConstants.Source,
            SourceId = $"{progress.ItemId}:{cycle}",
            PreviousEnrollmentId = previous?.ItemId,
            IgnorePrerequisites = true,
            Actor = CertificationsConstants.Source,
        }, cancellationToken);

        if (!enrolled.Succeeded)
        {
            _logger.LogWarning("Renewal of certification progress '{ProgressId}' could not enroll the holder in the course '{CourseId}': {ErrorCode}", progress.ItemId, courseId, enrolled.ErrorCode);

            return null;
        }

        return enrolled.Enrollment?.ItemId;
    }

    private async Task<CertificationPath> FindEnabledAsync(string pathId, CertificationPathResult result, CancellationToken cancellationToken)
    {
        var path = string.IsNullOrEmpty(pathId) ? null : await _paths.FindByIdAsync(pathId, cancellationToken);

        if (path is null || !path.Enabled)
        {
            result.Fail(S["The certification path doesn't exist or is not in use."]);

            return null;
        }

        return path;
    }

    private CertificationPathProgress NewProgress(CertificationPath path, string userId, string startedBy)
    {
        var progress = new CertificationPathProgress
        {
            PathId = path.ItemId,
            CertificationId = path.CertificationId,
            UserId = userId,
            State = CertificationPathState.InProgress,
            Cycle = 0,
            StartedBy = startedBy,
            CreatedUtc = _clock.UtcNow,
        };

        StartCycle(progress, renewal: false);

        return progress;
    }

    private CertificationCycle StartCycle(CertificationPathProgress progress, bool renewal)
    {
        progress.Cycle = Math.Max(progress.Cycle, progress.Cycles.Count) + 1;

        var cycle = new CertificationCycle
        {
            Number = progress.Cycle,
            Renewal = renewal,
            StartedUtc = _clock.UtcNow,
        };

        progress.Cycles.Add(cycle);

        return cycle;
    }

    private static bool IsPastGrace(CertificationPath path, CertificationPathProgress progress, DateOnly today)
        => progress.State == CertificationPathState.Lapsed && progress.ExpiresOn is { } expiresOn && today > expiresOn.AddDays(path.GracePeriodDays);

    private async Task<DateOnly> TodayAsync()
        => DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

    private Task<DateTime> StartOfDayUtcAsync(DateOnly day)
        => _localClock.ConvertToUtcAsync(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified));

    private async Task NotifyAsync(Func<ICertificationPathObserver, Task> notify, CertificationPathProgress progress)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await notify(observer);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "The certification path observer '{Observer}' failed for the progress '{ProgressId}'.", observer.GetType().Name, progress.ItemId);
            }
        }
    }

    private static string LockKey(string pathId, string userId) => $"Learning:CertificationPath:{pathId}:{userId}";
}
