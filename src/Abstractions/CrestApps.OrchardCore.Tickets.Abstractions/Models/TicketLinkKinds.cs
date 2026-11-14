namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// How a ticket relates to a linked record. The kind is an open string; these are the built-in ones.
/// </summary>
public static class TicketLinkKinds
{
    /// <summary>
    /// Related.
    /// </summary>
    public const string Related = "Related";

    /// <summary>
    /// The linked ticket is this one's parent.
    /// </summary>
    public const string Parent = "Parent";

    /// <summary>
    /// The linked ticket is this one's child.
    /// </summary>
    public const string Child = "Child";

    /// <summary>
    /// This ticket asks the same as the linked one.
    /// </summary>
    public const string Duplicate = "Duplicate";

    /// <summary>
    /// This ticket follows up the linked, closed one.
    /// </summary>
    public const string FollowUpOf = "FollowUpOf";
}
