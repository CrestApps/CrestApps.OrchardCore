using CrestApps.OrchardCore.MailingLists.Core.Indexes;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Stores mailing lists in the mailing lists collection.
/// </summary>
public sealed class MailingListStore : DocumentCatalog<MailingList, MailingListIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public MailingListStore(ISession session)
        : base(session)
    {
        CollectionName = MailingListsConstants.CollectionName;
    }

    /// <summary>
    /// Returns the list with the given technical name, matched without case sensitivity.
    /// </summary>
    /// <param name="name">The technical name.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The list, or <see langword="null"/>.</returns>
    public async Task<MailingList> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();

        // Names are short and few, and the column's collation may or may not ignore case, so the match is made here.
        var candidates = await Session.Query<MailingList, MailingListIndex>(index => index.Name == trimmed, collection: CollectionName).ListAsync(cancellationToken);
        var found = candidates.FirstOrDefault(list => string.Equals(list.Name, trimmed, StringComparison.OrdinalIgnoreCase));

        if (found is not null)
        {
            return found;
        }

        return (await GetAllAsync(cancellationToken)).FirstOrDefault(list => string.Equals(list.Name, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
