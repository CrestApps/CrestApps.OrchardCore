using CrestApps.Core.Services;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// Builds a real <see cref="TransactionManager"/> over the supplied <see cref="FakeTransactionStore"/> so
/// tests exercise the production manager without a database.
/// </summary>
internal static class TransactionManagerFactory
{
    public static TransactionManager Create(FakeTransactionStore store, params ITransactionEventHandler[] eventHandlers)
    {
        var services = new ServiceCollection();

        foreach (var eventHandler in eventHandlers)
        {
            services.AddSingleton(eventHandler);
        }

        return new(store, [], services.BuildServiceProvider(), NullLogger<CatalogManager<Transaction>>.Instance);
    }

    // The manager as the application gets it, through the service container, so a test calls it the way every caller
    // does: through ITransactionManager.
    public static ITransactionManager Resolve(FakeTransactionStore store, params ITransactionEventHandler[] eventHandlers)
        => new ServiceCollection()
            .AddSingleton<ITransactionManager>(Create(store, eventHandlers))
            .BuildServiceProvider()
            .GetRequiredService<ITransactionManager>();
}
