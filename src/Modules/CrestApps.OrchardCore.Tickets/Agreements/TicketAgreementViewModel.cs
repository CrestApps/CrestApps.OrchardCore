using CrestApps.OrchardCore.Agreements.Models;

namespace CrestApps.OrchardCore.Tickets.Agreements;

/// <summary>
/// The agreement a ticket works under, on the ticket's page, with how its hours stand.
/// </summary>
public class TicketAgreementViewModel
{
    /// <summary>
    /// Gets or sets the agreement.
    /// </summary>
    public Agreement Agreement { get; set; }

    /// <summary>
    /// Gets or sets how its hours stand, when it covers hours.
    /// </summary>
    public AgreementBalance Balance { get; set; }
}
