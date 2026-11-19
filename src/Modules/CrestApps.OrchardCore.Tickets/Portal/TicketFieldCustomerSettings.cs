namespace CrestApps.OrchardCore.Tickets.Portal;

/// <summary>
/// Whether customers fill in a field of a ticket type in the portal, in the field's settings. A field is hidden from
/// customers unless it says so, so an internal field on a type customers submit (a root cause, an approved discount)
/// is never theirs to set.
/// </summary>
public sealed class TicketFieldCustomerSettings
{
    /// <summary>
    /// Gets or sets whether customers fill the field in when they open a request.
    /// </summary>
    public bool CustomerEditable { get; set; }
}
