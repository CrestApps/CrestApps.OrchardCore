using CrestApps.OrchardCore.Learning.Certifications.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers certification path extension points.
/// </summary>
public static class CertificationsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a requirement kind.
    /// </summary>
    /// <typeparam name="TRequirement">The requirement.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddCertificationRequirement<TRequirement>(this IServiceCollection services)
        where TRequirement : class, ICertificationRequirement
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICertificationRequirement, TRequirement>());

        return services;
    }

    /// <summary>
    /// Registers an observer of certification path progress.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddCertificationPathObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, ICertificationPathObserver
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICertificationPathObserver, TObserver>());

        return services;
    }

    /// <summary>
    /// Registers an observer of education credits.
    /// </summary>
    public static IServiceCollection AddEducationCreditObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IEducationCreditObserver
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEducationCreditObserver, TObserver>());

        return services;
    }
}
