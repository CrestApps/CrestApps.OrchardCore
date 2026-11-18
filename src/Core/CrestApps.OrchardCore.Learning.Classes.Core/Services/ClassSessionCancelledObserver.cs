using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Notifications;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.Classes.Core.Services;

/// <summary>
/// Tells each learner whose class place was cancelled because staff cancelled the whole session, through the
/// Notifications module (its center and the delivery methods the person chose). A place a learner gave up, or one an
/// ending enrollment gave up, belongs to a session that is still on, so nobody is told then. Nothing is sent when the
/// Notifications feature is off.
/// </summary>
internal sealed class ClassSessionCancelledObserver : IAppointmentObserver
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public ClassSessionCancelledObserver(
        IServiceProvider services,
        ILogger<ClassSessionCancelledObserver> logger,
        IStringLocalizer<ClassSessionCancelledObserver> stringLocalizer)
    {
        _services = services;
        _logger = logger;
        S = stringLocalizer;
    }

    public async Task OnChangedAsync(AppointmentChange change, CancellationToken cancellationToken = default)
    {
        if (change?.Kind != AppointmentChangeKind.Cancelled ||
            change.Appointment is not { Source: ClassesConstants.RegistrationSource } appointment ||
            string.IsNullOrEmpty(appointment.UserId) ||
            string.IsNullOrEmpty(appointment.SessionId) ||
            _services.GetService<INotificationService>() is not { } notifications)
        {
            return;
        }

        try
        {
            var session = await _services.GetRequiredService<ISchedulingSessionService>().FindByIdAsync(appointment.SessionId, cancellationToken);

            if (session?.Status != SchedulingSessionStatus.Cancelled ||
                await _services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(appointment.UserId) is not { } user)
            {
                return;
            }

            var title = string.IsNullOrWhiteSpace(session.Title) ? S["your class"].Value : session.Title;
            var reason = string.IsNullOrWhiteSpace(appointment.CancelReason) ? S["No reason was given"].Value : appointment.CancelReason.Trim().TrimEnd('.');
            var text = S["The session of {0} you had a place in was cancelled. Reason: {1}. Open the course to choose another session.", title, reason].Value;

            await notifications.SendAsync(user, new LearningNotificationMessage
            {
                Subject = S["Your session of {0} was cancelled", title],
                Summary = text,
                TextBody = text,
                Type = ClassesConstants.SessionCancelledNotificationType,
                ReferenceId = appointment.ItemId,
                Url = "~/learning",
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The learner of cancelled class registration {AppointmentId} could not be told.", appointment.ItemId);
        }
    }
}
