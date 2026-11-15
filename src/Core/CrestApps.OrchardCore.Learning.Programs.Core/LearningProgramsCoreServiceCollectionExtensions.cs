using CrestApps.OrchardCore.Learning.Programs.Core.Services;
using CrestApps.OrchardCore.Learning.Programs.Core.Targets;
using CrestApps.OrchardCore.Learning.Programs.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Learning.Programs.Core;

/// <summary>
/// Registers the services of Learning programs.
/// </summary>
public static class LearningProgramsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers learning paths.
    /// </summary>
    public static IServiceCollection AddLearningPathsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<LearningPathProgressStore>();
        services.TryAddScoped<ILearningPathService, DefaultLearningPathService>();
        services.AddEnrollmentObserver<ProgramsEnrollmentObserver>();

        return services;
    }

    /// <summary>
    /// Registers required learning and its course and learning path targets.
    /// </summary>
    public static IServiceCollection AddRequiredLearningCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<LearningRequirementStore>();
        services.TryAddScoped<RequiredLearningAssignmentStore>();
        services.TryAddScoped<IRequiredLearningService, DefaultRequiredLearningService>();
        services.AddRequiredLearningTarget<CourseRequiredLearningTarget>();
        services.AddRequiredLearningTarget<LearningPathRequiredLearningTarget>();
        services.AddLearningPathObserver<RequiredLearningPathObserver>();

        return services;
    }

    /// <summary>
    /// Registers the certification path target, for sites with Learning certifications.
    /// </summary>
    public static IServiceCollection AddRequiredLearningCertificationTarget(this IServiceCollection services)
    {
        services.AddRequiredLearningTarget<CertificationPathRequiredLearningTarget>();

        return services;
    }
}
