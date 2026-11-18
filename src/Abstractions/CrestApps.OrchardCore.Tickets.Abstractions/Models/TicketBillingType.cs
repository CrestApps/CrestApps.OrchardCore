namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// How the work on a ticket is charged.
/// </summary>
public enum TicketBillingType
{
    /// <summary>
    /// Billed to the customer.
    /// </summary>
    Billable = 1,

    /// <summary>
    /// Not billed, such as internal work.
    /// </summary>
    NotBillable = 2,

    /// <summary>
    /// Covered by the account's support agreement.
    /// </summary>
    Agreement = 3,

    /// <summary>
    /// Billable work the customer isn't charged for, such as goodwill.
    /// </summary>
    NoCharge = 4,
}
