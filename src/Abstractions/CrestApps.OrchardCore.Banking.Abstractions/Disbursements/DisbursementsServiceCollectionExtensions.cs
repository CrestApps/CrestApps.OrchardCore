using CrestApps.OrchardCore.Banking.Disbursements.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Banking.Disbursements;

/// <summary>
/// Registers what other modules add to disbursements.
/// </summary>
public static class DisbursementsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a way to send disbursements.
    /// </summary>
    /// <typeparam name="TProvider">The provider.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddDisbursementProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IDisbursementProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDisbursementProvider, TProvider>());

        return services;
    }

    /// <summary>
    /// Registers an observer of disbursement changes.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddDisbursementObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IDisbursementObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDisbursementObserver, TObserver>());

        return services;
    }

    /// <summary>
    /// Registers an observer of disbursement batch changes.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddDisbursementBatchObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IDisbursementBatchObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDisbursementBatchObserver, TObserver>());

        return services;
    }
}
