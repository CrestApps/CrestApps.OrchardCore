using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Parties.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Banking.Core.Services;

/// <summary>
/// Manages bank accounts, so their handlers run.
/// </summary>
public sealed class BankAccountManager : CatalogManager<BankAccount>, IBankAccountManager
{
    private readonly IBankAccountStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="BankAccountManager"/> class.
    /// </summary>
    /// <param name="store">The account store.</param>
    /// <param name="handlers">The account handlers.</param>
    /// <param name="logger">The logger.</param>
    public BankAccountManager(
        IBankAccountStore store,
        IEnumerable<ICatalogEntryHandler<BankAccount>> handlers,
        ILogger<CatalogManager<BankAccount>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BankAccount>> ListByOwnersAsync(IEnumerable<PartyRef> owners, CancellationToken cancellationToken = default)
        => _store.ListByOwnersAsync(owners, cancellationToken);
}
