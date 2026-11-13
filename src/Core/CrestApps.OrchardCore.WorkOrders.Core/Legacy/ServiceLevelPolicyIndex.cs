using YesSql.Indexes;

namespace CrestApps.OrchardCore.WorkOrders.Core.Legacy;

/// <summary>
/// The index table Work Orders kept its own service level policies in. Its name matches the table, so the upgrade can
/// find the stored policies; nothing writes to it any more.
/// </summary>
public sealed class ServiceLevelPolicyIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the id of the policy's document.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the policy id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the policy name.
    /// </summary>
    public string Name { get; set; }
}
