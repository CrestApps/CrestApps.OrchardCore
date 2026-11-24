using YesSql.Indexes;

namespace CrestApps.OrchardCore.WorkOrders.Core.Legacy;

/// <summary>
/// The index table Work Orders kept its service agreements in. Its name matches the table, so the upgrade can find the
/// stored agreements when they move to the Agreements module; nothing writes to it any more.
/// </summary>
internal sealed class ServiceAgreementIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the id of the agreement's document.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the agreement id.
    /// </summary>
    public string ItemId { get; set; }
}
