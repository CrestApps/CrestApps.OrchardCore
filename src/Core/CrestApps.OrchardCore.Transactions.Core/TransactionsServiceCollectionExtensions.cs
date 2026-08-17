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

    /// <summary>
    /// Registers a transaction source so the administration report can present and filter transactions by a
    /// friendly, localizable name instead of the raw source key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The technical source key stored on the transaction.</param>
    /// <param name="configure">A delegate that configures the source (for example its display name).</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddTransactionSource(
        this IServiceCollection services,
        string name,
        Action<TransactionSource> configure)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure<TransactionSourceOptions>(options =>
        {
            var source = new TransactionSource(name);
            configure(source);
            options.AddSource(source);
        });

        return services;
    }
}
