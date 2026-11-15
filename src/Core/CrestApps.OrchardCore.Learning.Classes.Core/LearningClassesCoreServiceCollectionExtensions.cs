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
    /// Registers the class course item kind, class registration, and the observers that complete a class item on
    /// attendance and give up an enrollment's places when it ends.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLearningClassesCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IClassRegistrationService, DefaultClassRegistrationService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAppointmentObserver, ClassAttendanceObserver>());
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
