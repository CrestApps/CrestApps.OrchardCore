namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A protected item of a ticket, without its value.
/// </summary>
/// <param name="ItemId">The item.</param>
/// <param name="TicketId">The ticket.</param>
/// <param name="Label">What it is, such as "VPN login".</param>
/// <param name="CreatedUtc">When it was added.</param>
/// <param name="CreatedByName">Who added it.</param>
/// <param name="FromRequester">Whether the requester added it.</param>
public sealed record TicketProtectedItem(string ItemId, string TicketId, string Label, DateTime CreatedUtc, string CreatedByName, bool FromRequester);

/// <summary>
/// A protected item of a ticket with its value, read only for someone allowed to see it.
/// </summary>
/// <param name="ItemId">The item.</param>
/// <param name="Label">What it is.</param>
/// <param name="Value">The secret.</param>
public sealed record TicketProtectedValue(string ItemId, string Label, string Value);
