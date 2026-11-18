using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// What an account is entitled to on its tickets: the service-level policy its tickets run on, before the ticket type's
/// and the desk's.
/// </summary>
public sealed class TicketAccountPart : ContentPart
{
    /// <summary>
    /// Gets or sets the policy of the account's tickets, or <see langword="null"/> for the type's or desk's.
    /// </summary>
    public string ServiceLevelPolicyId { get; set; }
}
