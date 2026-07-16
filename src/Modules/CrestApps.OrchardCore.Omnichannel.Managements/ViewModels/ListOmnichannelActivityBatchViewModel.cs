using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the list view model for omnichannel activity batches.
/// </summary>
public sealed class ListOmnichannelActivityBatchViewModel : ListCatalogEntryViewModel<CatalogEntryViewModel<OmnichannelActivityBatch>>
{
    /// <summary>
    /// Gets or sets the available activity batch sources.
    /// </summary>
    public IEnumerable<ActivityBatchSourceEntry> Sources { get; set; }
}
