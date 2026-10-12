namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Classifies the nature of a taxable item so that the engine can apply the correct rules without
/// being coupled to any particular commerce implementation.
/// </summary>
public enum TaxableItemKind
{
    /// <summary>
    /// A physical good.
    /// </summary>
    Physical,

    /// <summary>
    /// A digital good or digital service.
    /// </summary>
    Digital,

    /// <summary>
    /// A service.
    /// </summary>
    Service,

    /// <summary>
    /// A booking or reservation.
    /// </summary>
    Booking,

    /// <summary>
    /// An event or ticket.
    /// </summary>
    Event,

    /// <summary>
    /// A shipping charge.
    /// </summary>
    Shipping,

    /// <summary>
    /// Any other charge that participates in taxation.
    /// </summary>
    OtherCharge,
}
