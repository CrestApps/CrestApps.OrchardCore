namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// The stable names of what can change on a ticket, kept in <c>TimelineFieldChange.Key</c> and given to workflows and
/// webhook subscribers. They are the same in every language.
/// </summary>
public static class TicketChangeKeys
{
    public const string Subject = "Subject";

    public const string Desk = "Desk";

    public const string Impact = "Impact";

    public const string Urgency = "Urgency";

    public const string Estimate = "Estimate";

    public const string BillingType = "BillingType";

    public const string Status = "Status";

    public const string Priority = "Priority";

    public const string Team = "Team";

    public const string Assignee = "Assignee";

    public const string EscalationLevel = "EscalationLevel";

    public const string Link = "Link";

    public const string Fields = "Fields";
}
