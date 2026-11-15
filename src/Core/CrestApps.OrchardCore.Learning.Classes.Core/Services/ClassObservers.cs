using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Learning.Classes.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Classes.Core.Services;

/// <summary>
/// Completes a class item when its learner attended: attended, or partly attended for at least the class's minimum
/// minutes. Recording the same attendance again changes nothing, because a completed item stays completed.
/// </summary>
internal sealed class ClassAttendanceObserver : IAppointmentObserver
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public ClassAttendanceObserver(IServiceProvider services, ILogger<ClassAttendanceObserver> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task OnChangedAsync(AppointmentChange change, CancellationToken cancellationToken = default)
    {
        if (change?.Kind != AppointmentChangeKind.Completed ||
            change.Appointment is not { Source: ClassesConstants.RegistrationSource } appointment ||
            !ClassesConstants.TryReadRegistrationSourceId(appointment.SourceId, out var enrollmentId, out var classItemId))
        {
            return;
        }

        var enrollment = await _services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollmentId, cancellationToken);

        if (enrollment is null)
        {
            return;
        }

        var courses = _services.GetRequiredService<ICourseStructureService>();
        var course = await courses.GetCourseAsync(enrollment.CourseId);
        var item = course is null ? null : (await courses.GetOutlineAsync(course)).Find(classItemId)?.Item;
        var part = item is not null && item.TryGet<ClassPart>(out var found) ? found : null;
        var attended = appointment.Attendance == AppointmentAttendance.Attended ||
            (appointment.Attendance == AppointmentAttendance.Partial && part?.MinimumMinutes is int minimum && (appointment.MinutesAttended ?? 0) >= minimum);

        if (!attended)
        {
            return;
        }

        var update = await _services.GetRequiredService<ILearnerProgressService>().RecordResultAsync(enrollment, new CourseItemResult { ItemId = classItemId, Attended = true }, cancellationToken);

        if (update.RefusedReason is not null)
        {
            _logger.LogWarning("The attendance of enrollment {EnrollmentId} in class {ClassItemId} did not complete the class: {Reason}.", enrollment.ItemId, classItemId, update.RefusedReason);
        }
    }
}

/// <summary>
/// Gives up an enrollment's class places when it ends without being completed, so the next person waiting is offered
/// the place.
/// </summary>
internal sealed class ClassEnrollmentObserver : IEnrollmentObserver
{
    private readonly IServiceProvider _services;

    public ClassEnrollmentObserver(IServiceProvider services)
    {
        _services = services;
    }

    public async Task StatusChangedAsync(Enrollment enrollment, EnrollmentStatus previous, CancellationToken cancellationToken = default)
    {
        if (enrollment is null || enrollment.IsOpen || enrollment.Status == EnrollmentStatus.Completed)
        {
            return;
        }

        await _services.GetRequiredService<IClassRegistrationService>().CancelAllAsync(enrollment.ItemId, "The enrollment ended.", "Learning", cancellationToken);
    }
}
