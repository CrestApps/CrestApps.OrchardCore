using CrestApps.OrchardCore.Learning.Coursework.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers submission observers.
/// </summary>
public static class CourseworkServiceCollectionExtensions
{
    /// <summary>
    /// Registers an observer of submissions.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSubmissionObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, ISubmissionObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISubmissionObserver, TObserver>());

        return services;
    }
}
