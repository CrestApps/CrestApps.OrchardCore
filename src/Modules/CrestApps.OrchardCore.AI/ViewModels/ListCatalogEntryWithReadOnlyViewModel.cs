using CrestApps.OrchardCore.Core.Models;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class ListCatalogEntryWithReadOnlyViewModel<T> : ListSourceCatalogEntryViewModel<T>
{
    public IList<CatalogEntryViewModel<T>> ReadOnlyModels { get; set; } = [];
}
