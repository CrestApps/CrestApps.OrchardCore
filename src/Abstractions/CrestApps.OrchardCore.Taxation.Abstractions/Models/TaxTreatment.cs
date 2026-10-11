namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Describes how a tax was treated for a taxable item, so that compliant documents can show
/// zero-rated, exempt, and reverse-charge lines rather than silently omitting them.
/// </summary>
public enum TaxTreatment
{
    /// <summary>
    /// The tax was applied and produced a positive amount.
    /// </summary>
    Taxable,

    /// <summary>
    /// The supply is exempt from the tax; no tax is charged and none can be reclaimed.
    /// </summary>
    Exempt,

    /// <summary>
    /// The supply is taxable at a zero rate; no tax is charged but the supply remains within the tax system.
    /// </summary>
    ZeroRated,

    /// <summary>
    /// The liability to account for the tax shifts to the recipient (for example EU B2B reverse charge).
    /// </summary>
    ReverseCharge,

    /// <summary>
    /// The supply is outside the scope of the tax.
    /// </summary>
    OutOfScope,
}
