using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Transactions.Core;

/// <summary>
/// Registration helpers for the provider-agnostic Transactions core services.
/// </summary>
public static class TransactionsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the transaction ledger store and manager.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddTransactionsCore(this IServiceCollection services)
    {
        services.AddScoped<ITransactionStore, TransactionStore>();
        services.AddScoped<ITransactionManager, TransactionManager>();

        return services;
    }
}
