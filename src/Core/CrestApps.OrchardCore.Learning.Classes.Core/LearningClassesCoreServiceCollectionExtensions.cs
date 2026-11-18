using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Classes;
using CrestApps.OrchardCore.Learning.Classes.Core.Services;
using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Learning.Classes.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Learning classes core.
/// </summary>
public static class LearningClassesCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the class course item kind, class registration, the observers that complete a class item on
    /// attendance, give up an enrollment's places when it ends and tell learners of a cancelled session, and the queue
    /// that retries their unfinished work.
    /// The feature's migration creates the queue's table.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLearningClassesCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IClassRegistrationService, DefaultClassRegistrationService>();
        services.TryAddScoped<ClassFollowUps>();
        services.TryAddScoped<ClassAttendanceRecorder>();
        services.AddLeasedWorkQueue<ClassFollowUpWork>(options =>
        {
            options.CollectionName = ClassFollowUpWork.CollectionName;
            options.RemoveCompletedItems = true;
        });
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAppointmentObserver, ClassAttendanceObserver>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAppointmentObserver, ClassSessionCancelledObserver>());
        services.AddCalendarEventSource<ClassSessionCalendarEventSource>();
        services.AddEnrollmentObserver<ClassEnrollmentObserver>();
        services.AddCourseItemKind<ClassPart>(ClassesConstants.ClassKind, kind =>
        {
            kind.DisplayName = LocalizationSource.Create("Class", typeof(LearningClassesCoreServiceCollectionExtensions));
            kind.LearnerName = LocalizationSource.Create("Class", typeof(LearningClassesCoreServiceCollectionExtensions));
            kind.ContentType = ClassesConstants.ContentTypes.Class;
            kind.CompletionModes.Add(CompletionModes.Attended);
            kind.CompletionModes.Add(CompletionModes.MarkedDone);
            kind.Icon = "fa-solid fa-chalkboard-user";
        });

        return services;
    }
}
