using CrestApps.OrchardCore.Learning.Commerce.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers observers of licence pools.
/// </summary>
public static class LearningCommerceServiceCollectionExtensions
{
    /// <summary>
    /// Registers an observer of licence pools.
    /// </summary>
    public static IServiceCollection AddLicensePoolObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, ILicensePoolObserver
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILicensePoolObserver, TObserver>());

        return services;
    }
}
