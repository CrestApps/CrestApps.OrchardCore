using CrestApps.OrchardCore.Banking.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Banking;

/// <summary>
/// Registers what other modules add to bank accounts.
/// </summary>
public static class BankingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the routing and account number checks of one country.
    /// </summary>
    /// <typeparam name="TValidator">The validator.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddBankRoutingValidator<TValidator>(this IServiceCollection services)
        where TValidator : class, IBankRoutingValidator
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBankRoutingValidator, TValidator>());

        return services;
    }

    /// <summary>
    /// Registers a way to check bank accounts.
    /// </summary>
    /// <typeparam name="TVerifier">The verifier.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddBankAccountVerifier<TVerifier>(this IServiceCollection services)
        where TVerifier : class, IBankAccountVerifier
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBankAccountVerifier, TVerifier>());

        return services;
    }

    /// <summary>
    /// Registers an observer of bank account changes.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddBankAccountObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IBankAccountObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBankAccountObserver, TObserver>());

        return services;
    }
}
