using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers what modules and packs add to Tax Returns and E-Filing: the figures of a return, return forms, observers,
/// e-file targets, formats and filing channels.
/// </summary>
public static class TaxReturnsServiceCollectionExtensions
{
    /// <summary>
    /// Adds a provider of return figures, such as a module's ledger or invoices.
    /// </summary>
    /// <typeparam name="TProvider">The provider.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddTaxReturnDataProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ITaxReturnDataProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITaxReturnDataProvider, TProvider>());

        return services;
    }

    /// <summary>
    /// Adds a return form, such as a state's sales and use tax return.
    /// </summary>
    /// <typeparam name="TForm">The form.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddTaxReturnForm<TForm>(this IServiceCollection services)
        where TForm : class, ITaxReturnForm
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITaxReturnForm, TForm>());

        return services;
    }

    /// <summary>
    /// Adds an observer of returns, such as one that posts a return's payment to the books.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddTaxReturnObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, ITaxReturnObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITaxReturnObserver, TObserver>());

        return services;
    }

    /// <summary>
    /// Adds a kind of document that can be filed electronically.
    /// </summary>
    /// <typeparam name="TProvider">The target provider.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddEFileTargetProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IEFileTargetProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEFileTargetProvider, TProvider>());

        return services;
    }

    /// <summary>
    /// Adds an e-file format, such as a state's XML schema.
    /// </summary>
    /// <typeparam name="TFormat">The format.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddEFileFormat<TFormat>(this IServiceCollection services)
        where TFormat : class, IEFileFormat
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEFileFormat, TFormat>());

        return services;
    }

    /// <summary>
    /// Adds a filing channel, such as a state's gateway.
    /// </summary>
    /// <typeparam name="TTransport">The channel.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddEFileTransport<TTransport>(this IServiceCollection services)
        where TTransport : class, IEFileTransport
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEFileTransport, TTransport>());

        return services;
    }
}
