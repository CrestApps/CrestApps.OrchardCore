using CrestApps.OrchardCore.Checks.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Checks;

/// <summary>
/// Registers what other modules add to checks.
/// </summary>
public static class ChecksServiceCollectionExtensions
{
    /// <summary>
    /// Registers the voucher of the checks of one source kind.
    /// </summary>
    /// <typeparam name="TSource">The voucher source.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddCheckVoucherSource<TSource>(this IServiceCollection services)
        where TSource : class, ICheckVoucherSource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICheckVoucherSource, TSource>());

        return services;
    }

    /// <summary>
    /// Registers an observer of check changes.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddCheckObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, ICheckObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICheckObserver, TObserver>());

        return services;
    }

    /// <summary>
    /// Registers the check line format of one country.
    /// </summary>
    /// <typeparam name="TFormat">The format.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddCheckMicrFormat<TFormat>(this IServiceCollection services)
        where TFormat : class, ICheckMicrFormat
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICheckMicrFormat, TFormat>());

        return services;
    }

    /// <summary>
    /// Registers the amount in words of one language.
    /// </summary>
    /// <typeparam name="TWriter">The writer.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddAmountInWords<TWriter>(this IServiceCollection services)
        where TWriter : class, IAmountInWords
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAmountInWords, TWriter>());

        return services;
    }
}
