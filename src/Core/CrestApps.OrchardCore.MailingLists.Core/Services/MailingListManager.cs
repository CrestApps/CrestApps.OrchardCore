using CrestApps.Core.Handlers;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Manages mailing lists through their catalog handlers.
/// </summary>
public sealed class MailingListManager : CatalogManager<MailingList>, IMailingListManager
{
    private readonly MailingListStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public MailingListManager(
        MailingListStore store,
        IEnumerable<ICatalogEntryHandler<MailingList>> handlers,
        ILogger<CatalogManager<MailingList>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public Task<MailingList> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => _store.FindByNameAsync(name, cancellationToken);
}
