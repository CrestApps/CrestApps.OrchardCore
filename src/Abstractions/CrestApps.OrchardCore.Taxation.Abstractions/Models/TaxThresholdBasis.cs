namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// What a tax rule's minimum and maximum amounts are compared with.
/// </summary>
public enum TaxThresholdBasis
{
    /// <summary>
    /// Each item's taxable amount.
    /// </summary>
    Item,

    /// <summary>
    /// What the order's items come to, shipping left out, such as for an import duty charged only above a value.
    /// </summary>
    Order,
}
