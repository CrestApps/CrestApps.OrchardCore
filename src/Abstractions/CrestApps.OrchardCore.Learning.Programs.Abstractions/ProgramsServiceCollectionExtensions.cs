using CrestApps.OrchardCore.Learning.Programs.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the extension points of Learning programs.
/// </summary>
public static class ProgramsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a kind of target required learning can require.
    /// </summary>
    public static IServiceCollection AddRequiredLearningTarget<TTarget>(this IServiceCollection services)
        where TTarget : class, IRequiredLearningTarget
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRequiredLearningTarget, TTarget>());

        return services;
    }

    /// <summary>
    /// Registers an observer of required learning.
    /// </summary>
    public static IServiceCollection AddRequiredLearningObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IRequiredLearningObserver
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRequiredLearningObserver, TObserver>());

        return services;
    }

    /// <summary>
    /// Registers an observer of completed learning paths.
    /// </summary>
    public static IServiceCollection AddLearningPathObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, ILearningPathObserver
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILearningPathObserver, TObserver>());

        return services;
    }
}
