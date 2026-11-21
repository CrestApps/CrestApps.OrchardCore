using CrestApps.OrchardCore.Ledgers;
using CrestApps.OrchardCore.YesSql.Core.Ledgers;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using YesSql.Indexes;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers quantity ledgers.
/// </summary>
public static class QuantityLedgerServiceCollectionExtensions
{
    /// <summary>
    /// Registers a quantity ledger kept in its own collection. The owner adds the collection to the store and creates
    /// its index table with <see cref="QuantityLedgerIndexSchema.CreateAsync"/>.
    /// </summary>
    /// <typeparam name="TLedger">The marker type that names the ledger.</typeparam>
    /// <param name="services">The services.</param>
    /// <param name="configure">Sets the collection.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddQuantityLedger<TLedger>(this IServiceCollection services, Action<QuantityLedgerOptions<TLedger>> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(IQuantityLedger<TLedger>)))
        {
            return services;
        }

        services.Configure(configure);
        services.TryAddScoped<IQuantityLedger<TLedger>, YesSqlQuantityLedger<TLedger>>();
        services.AddSingleton<IIndexProvider>(serviceProvider =>
            new QuantityLedgerEntryIndexProvider(serviceProvider.GetRequiredService<IOptions<QuantityLedgerOptions<TLedger>>>().Value.CollectionName));

        return services;
    }
}
