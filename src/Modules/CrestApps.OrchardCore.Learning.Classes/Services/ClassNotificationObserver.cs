using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Classes.Services;

/// <summary>
/// Tells a learner, through the Notifications module, when someone else gave them a place in a class session, and when a
/// place freed up in a full session they wait for. A learner who registers or accepts an offer themselves is not told.
/// Each learner with a place is told when staff move the session to other times. A cancelled session is told by its own
/// observer. Each notice links to the class in the course player.
/// </summary>
internal sealed class ClassNotificationObserver : IAppointmentObserver
{
    private readonly ISchedulingSessionService _sessions;
    private readonly IEnrollmentStore _enrollments;
    private readonly ILearningNotificationSender _sender;

    internal readonly IStringLocalizer S;

    public ClassNotificationObserver(
        ISchedulingSessionService sessions,
        IEnrollmentStore enrollments,
        ILearningNotificationSender sender,
        IStringLocalizer<ClassNotificationObserver> stringLocalizer)
    {
        _sessions = sessions;
        _enrollments = enrollments;
        _sender = sender;
        S = stringLocalizer;
    }

    public async Task OnChangedAsync(AppointmentChange change, CancellationToken cancellationToken = default)
    {
        if (change is null)
        {
            return;
        }

        if (change.Kind == AppointmentChangeKind.Confirmed &&
            change.Appointment is { Source: ClassesConstants.RegistrationSource } appointment &&
            !string.IsNullOrEmpty(appointment.UserId) &&
            !string.IsNullOrEmpty(change.Actor) &&
            !string.Equals(change.Actor, appointment.UserId, StringComparison.Ordinal))
        {
            var title = await SessionTitleAsync(appointment.SessionId, cancellationToken);

            await SendAsync(appointment.UserId, appointment.SourceId, appointment.ItemId, ClassesConstants.NotificationTypes.Registered,
                S["You have a place in {0}", title],
                S["You were given a place in the session of {0}. Open the class to see when and where it meets.", title],
                cancellationToken);

            return;
        }

        if (change.Kind == AppointmentChangeKind.Rescheduled &&
            change.Appointment is { Source: ClassesConstants.RegistrationSource } moved &&
            !string.IsNullOrEmpty(moved.UserId))
        {
            // The notice names no time: it may be read in another time zone than the staff member's who moved it, and
            // the class page shows the meetings in the reader's own.
            var title = await SessionTitleAsync(moved.SessionId, cancellationToken);

            await SendAsync(moved.UserId, moved.SourceId, moved.ItemId, ClassesConstants.NotificationTypes.SessionMoved,
                S["Your session of {0} has moved", title],
                S["The session of {0} you have a place in now meets at other times. Open the class to see when it meets.", title],
                cancellationToken);

            return;
        }

        if (change.Kind == AppointmentChangeKind.WaitlistSlotAvailable &&
            change.WaitlistEntry is { Source: ClassesConstants.RegistrationSource } entry &&
            !string.IsNullOrEmpty(entry.UserId))
        {
            var title = await SessionTitleAsync(entry.SessionId, cancellationToken);
            var body = change.Appointment is null
                ? S["A place is free in the session of {0} you wait for. Open the class to take it before someone else does.", title]
                : S["A place in the session of {0} you wait for is held for you for a short time. Open the class to accept it.", title];

            await SendAsync(entry.UserId, entry.SourceId, entry.ItemId, ClassesConstants.NotificationTypes.PlaceOffered,
                S["A place is free in {0}", title],
                body,
                cancellationToken);
        }
    }

    private async Task<string> SessionTitleAsync(string sessionId, CancellationToken cancellationToken)
    {
        var session = string.IsNullOrEmpty(sessionId) ? null : await _sessions.FindByIdAsync(sessionId, cancellationToken);

        return string.IsNullOrWhiteSpace(session?.Title) ? S["your class"].Value : session.Title;
    }

    private async Task SendAsync(string userId, string sourceId, string referenceId, string type, string subject, string body, CancellationToken cancellationToken)
    {
        var url = "~/learning";

        if (ClassesConstants.TryReadRegistrationSourceId(sourceId, out var enrollmentId, out var classItemId) &&
            await _enrollments.FindByIdAsync(enrollmentId, cancellationToken) is { } enrollment)
        {
            url = $"~/learning/courses/{enrollment.CourseId}/play/{classItemId}";
        }

        _sender.Send(userId, new LearningNotificationMessage
        {
            Subject = subject,
            Summary = body,
            TextBody = body,
            Type = type,
            ReferenceId = referenceId,
            Url = url,
        });
    }
}
