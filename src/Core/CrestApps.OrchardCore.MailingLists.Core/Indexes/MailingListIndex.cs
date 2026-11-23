using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.MailingLists.Core.Indexes;

/// <summary>
/// Indexes mailing lists by name and provider.
/// </summary>
public sealed class MailingListIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name of the list's provider.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets whether the list is offered to people.
    /// </summary>
    public bool IsEnabled { get; set; }
}
