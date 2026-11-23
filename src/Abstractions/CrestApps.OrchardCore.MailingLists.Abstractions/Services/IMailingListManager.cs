using CrestApps.Core.Services;
using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.MailingLists.Services;

/// <summary>
/// Manages mailing lists, so their handlers run.
/// </summary>
public interface IMailingListManager : ICatalogManager<MailingList>
{
    /// <summary>
    /// Returns the list with the given technical name, matched without case sensitivity.
    /// </summary>
    /// <param name="name">The technical name.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The list, or <see langword="null"/>.</returns>
    Task<MailingList> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}
