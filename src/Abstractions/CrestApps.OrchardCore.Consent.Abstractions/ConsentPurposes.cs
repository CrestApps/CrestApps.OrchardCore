namespace CrestApps.OrchardCore.Consent;

/// <summary>
/// Well-known reasons the site contacts a person. The list is open: a module may use its own purpose string. An
/// opt-out with a purpose blocks queries for that purpose; an opt-out with no purpose blocks every purpose; a query with
/// no purpose asks only whether every purpose is blocked.
/// </summary>
public static class ConsentPurposes
{
    /// <summary>
    /// Marketing: newsletters, campaigns, offers. A person who stops marketing can still be sent what they asked for
    /// (a confirmation, an order notice, a reply to their question).
    /// </summary>
    public const string Marketing = "Marketing";
}
