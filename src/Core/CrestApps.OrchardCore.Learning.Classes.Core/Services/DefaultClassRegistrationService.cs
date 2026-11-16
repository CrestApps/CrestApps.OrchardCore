using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Learning.Classes.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.AspNetCore.Identity;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.Classes.Core.Services;

/// <summary>
/// Registers enrolled learners in the dated sessions of a class item through <see cref="ISchedulingSessionService"/>,
/// which keeps the places, the holds, the waitlist and its offers under its own lock per session.
/// </summary>
internal sealed class DefaultClassRegistrationService : IClassRegistrationService
{
    private static readonly AppointmentStatus[] _upcoming = [AppointmentStatus.Held, AppointmentStatus.Requested, AppointmentStatus.Confirmed];

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(60);

    private readonly ISchedulingSessionService _sessions;
    private readonly IEnrollmentStore _enrollments;
    private readonly ICourseStructureService _courses;
    private readonly UserManager<IUser> _users;
    private readonly IDistributedLock _distributedLock;
    private readonly CourseReleaseEvaluator _release;
    private readonly ILearnerProgressService _progress;
    private readonly IClock _clock;

    public DefaultClassRegistrationService(
        ISchedulingSessionService sessions,
        IEnrollmentStore enrollments,
        ICourseStructureService courses,
        UserManager<IUser> users,
        IDistributedLock distributedLock,
        CourseReleaseEvaluator release,
        ILearnerProgressService progress,
        IClock clock)
    {
        _sessions = sessions;
        _enrollments = enrollments;
        _courses = courses;
        _users = users;
        _distributedLock = distributedLock;
        _release = release;
        _progress = progress;
        _clock = clock;
    }

    public async Task<IReadOnlyList<SchedulingSession>> ListSessionsAsync(string classItemId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(classItemId)
            ? []
            : (await _sessions.ListBySourceAsync(ClassesConstants.SessionSource, classItemId, cancellationToken))
                .Where(session => session.Status == SchedulingSessionStatus.Scheduled)
                .OrderBy(session => session.StartUtc)
                .ToArray();

    public async Task<ClassRegistration> FindAsync(string enrollmentId, string classItemId, CancellationToken cancellationToken = default)
    {
        var enrollment = string.IsNullOrEmpty(enrollmentId) ? null : await _enrollments.FindByIdAsync(enrollmentId, cancellationToken);

        return enrollment is null ? null : await FindAsync(enrollment, classItemId, cancellationToken);
    }

    public async Task<ClassRegistrationResult> RegisterAsync(string enrollmentId, string classItemId, string sessionId, string actor = null, CancellationToken cancellationToken = default)
    {
        var enrollment = string.IsNullOrEmpty(enrollmentId) ? null : await _enrollments.FindByIdAsync(enrollmentId, cancellationToken);

        if (enrollment is null)
        {
            return ClassRegistrationResult.Fail(ClassRegistrationErrors.EnrollmentNotFound);
        }

        if (enrollment.Status != EnrollmentStatus.Active)
        {
            return ClassRegistrationResult.Fail(ClassRegistrationErrors.EnrollmentNotActive);
        }

        var entry = await FindClassEntryAsync(enrollment.CourseId, classItemId);

        if (entry is null)
        {
            return ClassRegistrationResult.Fail(ClassRegistrationErrors.NotAClassOfTheCourse);
        }

        // As for any other item of the course: within the learner's access dates, once the release rules open it.
        if (!EnrollmentAccess.HasAccess(enrollment, _clock.UtcNow))
        {
            return ClassRegistrationResult.Fail(ClassRegistrationErrors.NoAccess);
        }

        if (!(await _release.EvaluateAsync(entry, enrollment, await _progress.GetAsync(enrollment.ItemId, cancellationToken))).IsReleased)
        {
            return ClassRegistrationResult.Fail(ClassRegistrationErrors.NotAvailableYet);
        }

        var session = string.IsNullOrEmpty(sessionId) ? null : await _sessions.FindByIdAsync(sessionId, cancellationToken);

        if (session is null ||
            session.Status != SchedulingSessionStatus.Scheduled ||
            session.Source != ClassesConstants.SessionSource ||
            session.SourceId != classItemId)
        {
            return ClassRegistrationResult.Fail(ClassRegistrationErrors.SessionNotFound);
        }

        // The Scheduling lock is per session, so two requests for different sessions of the class would both pass the
        // one-place check. The check and the registration run under one lock per enrollment and class item instead.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(RegistrationLockKey(enrollment.ItemId, classItemId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return ClassRegistrationResult.Fail(ClassRegistrationErrors.Busy);
        }

        try
        {
            // One place per class: the same session again returns it, another session needs the place given up first.
            // Read inside the lock, so a registration the other request just committed is seen.
            var existing = await FindAsync(enrollment, classItemId, cancellationToken);

            if (existing is not null && !existing.CanRegisterAgain)
            {
                return existing.Session.ItemId == session.ItemId
                    ? new ClassRegistrationResult { Registration = existing }
                    : ClassRegistrationResult.Fail(ClassRegistrationErrors.AlreadyRegistered);
            }

            var user = await _users.FindByIdAsync(enrollment.UserId);
            var result = await _sessions.RegisterAsync(new SessionRegistrationRequest
            {
                SessionId = session.ItemId,
                UserId = enrollment.UserId,
                CustomerName = user is null ? enrollment.UserId : await _users.GetUserNameAsync(user),
                CustomerEmail = user is null ? null : await _users.GetEmailAsync(user),
                Source = ClassesConstants.RegistrationSource,
                SourceId = ClassesConstants.RegistrationSourceId(enrollment.ItemId, classItemId),
                Actor = actor ?? enrollment.UserId,
                JoinWaitlistWhenFull = session.WaitlistMode != SchedulingWaitlistMode.None,
            }, cancellationToken);

            return result.Succeeded
                ? new ClassRegistrationResult { Registration = new ClassRegistration(session, result.Appointment, result.Appointment is null ? result.WaitlistEntry : null) }
                : ClassRegistrationResult.Fail(result.Error);
        }
        finally
        {
            await locker.DisposeAsync();
        }
    }

    private static string RegistrationLockKey(string enrollmentId, string classItemId)
        => "LEARNING_CLASS_REGISTRATION_" + enrollmentId + "_" + classItemId;

    public async Task<ClassRegistrationResult> AcceptOfferAsync(string enrollmentId, string classItemId, string actor = null, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(enrollmentId, classItemId, cancellationToken);

        if (existing is not { IsOffered: true })
        {
            return ClassRegistrationResult.Fail(ClassRegistrationErrors.NoOffer);
        }

        var result = await _sessions.AcceptOfferAsync(existing.Appointment.ItemId, actor, cancellationToken);

        return result.Succeeded
            ? new ClassRegistrationResult { Registration = new ClassRegistration(existing.Session, result.Appointment, null) }
            : ClassRegistrationResult.Fail(result.Error);
    }

    public async Task<ClassRegistrationResult> CancelAsync(string enrollmentId, string classItemId, string reason, string actor = null, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(enrollmentId, classItemId, cancellationToken);

        if (existing is null || (existing.Appointment is not null && !_upcoming.Contains(existing.Appointment.Status)))
        {
            return ClassRegistrationResult.Fail(ClassRegistrationErrors.NotRegistered);
        }

        // Once the first meeting started the place stays on the roster, so its attendance can be marked.
        if (HasStarted(existing))
        {
            return ClassRegistrationResult.Fail(ClassRegistrationErrors.SessionStarted);
        }

        return await CancelAsync(existing, reason, actor, cancellationToken);
    }

    private bool HasStarted(ClassRegistration registration)
        => registration.Appointment is not null && registration.Session.StartUtc <= _clock.UtcNow;

    public async Task<int> CancelAllAsync(string enrollmentId, string reason, string actor = null, CancellationToken cancellationToken = default)
    {
        var enrollment = string.IsNullOrEmpty(enrollmentId) ? null : await _enrollments.FindByIdAsync(enrollmentId, cancellationToken);

        if (enrollment is null)
        {
            return 0;
        }

        // The classes of the course, and any other class this enrollment holds a place in (one taken off the outline).
        var classItemIds = new HashSet<string>(StringComparer.Ordinal);
        var course = await _courses.GetCourseAsync(enrollment.CourseId);

        if (course is not null)
        {
            foreach (var entry in (await _courses.GetOutlineAsync(course)).Entries.Where(entry => entry.Part?.Kind == ClassesConstants.ClassKind))
            {
                classItemIds.Add(entry.Item.ContentItemId);
            }
        }

        foreach (var appointment in await _sessions.ListRegistrationsForUserAsync(enrollment.UserId, cancellationToken))
        {
            if (appointment.Source == ClassesConstants.RegistrationSource &&
                ClassesConstants.TryReadRegistrationSourceId(appointment.SourceId, out var registrationEnrollmentId, out var classItemId) &&
                registrationEnrollmentId == enrollment.ItemId)
            {
                classItemIds.Add(classItemId);
            }
        }

        var cancelled = 0;

        foreach (var classItemId in classItemIds)
        {
            var existing = await FindAsync(enrollment, classItemId, cancellationToken);

            // A place in a session that already started stays on its roster; waits and upcoming places are given up.
            if (existing is null || (existing.Appointment is not null && !_upcoming.Contains(existing.Appointment.Status)) || HasStarted(existing))
            {
                continue;
            }

            if ((await CancelAsync(existing, reason, actor, cancellationToken)).Succeeded)
            {
                cancelled++;
            }
        }

        return cancelled;
    }

    private async Task<ClassRegistrationResult> CancelAsync(ClassRegistration existing, string reason, string actor, CancellationToken cancellationToken)
    {
        if (existing.Appointment is not null)
        {
            var result = await _sessions.CancelRegistrationAsync(existing.Appointment.ItemId, reason, actor, cancellationToken);

            return result.Succeeded ? new ClassRegistrationResult() : ClassRegistrationResult.Fail(result.Error);
        }

        return await _sessions.LeaveWaitlistAsync(existing.WaitlistEntry.ItemId, cancellationToken)
            ? new ClassRegistrationResult()
            : ClassRegistrationResult.Fail(ClassRegistrationErrors.NotRegistered);
    }

    // The place that holds the class for the learner: an upcoming or offered place, a wait, or attendance that counted.
    // Without one, the last session they were absent from (or attended too little of) is returned so the panel can say
    // so; it does not keep them from registering in another session.
    private async Task<ClassRegistration> FindAsync(Enrollment enrollment, string classItemId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(classItemId))
        {
            return null;
        }

        var sourceId = ClassesConstants.RegistrationSourceId(enrollment.ItemId, classItemId);
        var part = await FindPartAsync(enrollment.CourseId, classItemId);
        ClassRegistration ended = null;

        foreach (var session in await _sessions.ListBySourceAsync(ClassesConstants.SessionSource, classItemId, cancellationToken))
        {
            var registrations = (await _sessions.ListRegistrationsAsync(session.ItemId, cancellationToken))
                .Where(candidate => candidate.SourceId == sourceId)
                .ToList();
            var holding = registrations.FirstOrDefault(candidate => _upcoming.Contains(candidate.Status) ||
                (candidate.Status == AppointmentStatus.Completed && ClassAttendance.Counts(candidate, part)));

            if (holding is not null)
            {
                return new ClassRegistration(session, holding, null);
            }

            var waiting = (await _sessions.ListWaitlistAsync(session.ItemId, cancellationToken))
                .FirstOrDefault(candidate => candidate.SourceId == sourceId && candidate.Status == WaitlistStatus.Waiting);

            if (waiting is not null)
            {
                return new ClassRegistration(session, null, waiting);
            }

            if (registrations.LastOrDefault(candidate => candidate.Status is AppointmentStatus.NoShow or AppointmentStatus.Completed) is { } past)
            {
                ended = new ClassRegistration(session, past, null) { CanRegisterAgain = true };
            }
        }

        return ended;
    }

    private async Task<ClassPart> FindPartAsync(string courseId, string classItemId)
    {
        var course = await _courses.GetCourseAsync(courseId);
        var item = course is null ? null : (await _courses.GetOutlineAsync(course)).Find(classItemId)?.Item;

        return item is not null && item.TryGet<ClassPart>(out var part) ? part : null;
    }

    private async Task<CourseOutlineEntry> FindClassEntryAsync(string courseId, string classItemId)
    {
        var course = string.IsNullOrEmpty(classItemId) ? null : await _courses.GetCourseAsync(courseId);
        var entry = course is null ? null : (await _courses.GetOutlineAsync(course)).Find(classItemId);

        return entry?.Part?.Kind == ClassesConstants.ClassKind ? entry : null;
    }
}
