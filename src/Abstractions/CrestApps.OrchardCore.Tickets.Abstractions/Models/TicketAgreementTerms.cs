namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// The ticket terms of an agreement, kept in the agreement's properties: an agreement with them covers tickets of its
/// account, on the desks and ticket types it names (all of them when it names none), and its service-level policy runs
/// those tickets.
/// </summary>
public sealed class TicketAgreementTerms
{
    /// <summary>
    /// Gets or sets the desks it covers; empty covers every desk.
    /// </summary>
    public List<string> DeskIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the ticket types it covers; empty covers every type.
    /// </summary>
    public List<string> TicketTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the service-level policy its tickets run on, before the account's own; <see langword="null"/> leaves
    /// the account's, the category's, the type's or the desk's.
    /// </summary>
    public string ServiceLevelPolicyId { get; set; }

    /// <summary>
    /// Gets whether it covers a ticket on a desk, of a type.
    /// </summary>
    /// <param name="deskId">The desk.</param>
    /// <param name="contentType">The ticket type.</param>
    /// <returns>Whether it covers it.</returns>
    public bool Covers(string deskId, string contentType)
        => (DeskIds is not { Count: > 0 } || DeskIds.Contains(deskId, StringComparer.Ordinal))
            && (TicketTypes is not { Count: > 0 } || TicketTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase));
}
