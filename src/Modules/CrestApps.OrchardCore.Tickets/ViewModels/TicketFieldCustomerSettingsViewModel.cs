namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// Whether customers fill a field of a ticket type in.
/// </summary>
public class TicketFieldCustomerSettingsViewModel
{
    /// <summary>
    /// Gets or sets whether customers fill the field in when they open a request in the portal.
    /// </summary>
    public bool CustomerEditable { get; set; }
}
