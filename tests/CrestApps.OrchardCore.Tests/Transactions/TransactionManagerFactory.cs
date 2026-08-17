using CrestApps.Core.Services;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// Builds a real <see cref="TransactionManager"/> over the supplied <see cref="FakeTransactionStore"/> so
/// tests exercise the production manager without a database.
/// </summary>
internal static class TransactionManagerFactory
{
    public static TransactionManager Create(FakeTransactionStore store)
        => new(store, [], NullLogger<CatalogManager<Transaction>>.Instance);
}
