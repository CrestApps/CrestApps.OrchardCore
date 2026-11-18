namespace CrestApps.OrchardCore.WorkOrders.Models;

/// <summary>
/// How the visits of an agreement are paid for.
/// </summary>
public enum AgreementPricing
{
    /// <summary>
    /// The agreement covers them: their work orders aren't invoiced.
    /// </summary>
    Included,

    /// <summary>
    /// Each visit is invoiced at the agreement's price.
    /// </summary>
    PerVisit,
}
