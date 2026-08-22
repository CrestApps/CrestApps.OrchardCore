namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Provides the well-known, extensible tax sourcing strategy identifiers.
/// </summary>
/// <remarks>
/// Sourcing determines which address is used to resolve the applicable jurisdictions for a taxable item.
/// Strategies are resolved by name so additional sourcing rules can be introduced by third parties.
/// </remarks>
public static class TaxSourcingNames
{
    /// <summary>
    /// Source tax from the origin (ship-from) address.
    /// </summary>
    public const string Origin = "Origin";

    /// <summary>
    /// Source tax from the destination (ship-to) address.
    /// </summary>
    public const string Destination = "Destination";

    /// <summary>
    /// Source tax from the customer residence address.
    /// </summary>
    public const string CustomerResidence = "CustomerResidence";

    /// <summary>
    /// Source tax from the customer business address.
    /// </summary>
    public const string CustomerBusiness = "CustomerBusiness";

    /// <summary>
    /// Source tax from the location where a service is performed.
    /// </summary>
    public const string ServiceLocation = "ServiceLocation";

    /// <summary>
    /// Source tax from the location where an event takes place.
    /// </summary>
    public const string EventLocation = "EventLocation";
}
