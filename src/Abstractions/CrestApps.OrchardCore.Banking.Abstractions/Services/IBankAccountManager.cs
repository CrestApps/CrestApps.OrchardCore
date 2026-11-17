using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Banking.Services;

/// <summary>
/// Stores bank accounts. Writes run the catalog handlers, which check the record and tell the
/// <see cref="IBankAccountObserver"/>s what changed. Set numbers and status through <see cref="IBankAccountService"/>.
/// </summary>
public interface IBankAccountManager : ICatalogManager<BankAccount>
{
    /// <summary>
    /// Lists the accounts of some owners, oldest first.
    /// </summary>
    /// <param name="owners">The owners; an empty reference lists the business's own accounts.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The accounts.</returns>
    Task<IReadOnlyList<BankAccount>> ListByOwnersAsync(IEnumerable<PartyRef> owners, CancellationToken cancellationToken = default);
}
