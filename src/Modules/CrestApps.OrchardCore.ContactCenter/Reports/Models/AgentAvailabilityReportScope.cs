namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// Which agents the agent availability report lists.
/// </summary>
internal enum AgentAvailabilityReportScope
{
    /// <summary>
    /// Every agent: the person running the report oversees no queue or campaign, so there is nothing to scope by.
    /// </summary>
    Everyone,

    /// <summary>
    /// Every agent entitled to or signed in to a queue or campaign the person running the report oversees.
    /// </summary>
    Overseen,

    /// <summary>
    /// The one agent chosen in the report's filter.
    /// </summary>
    ChosenAgent,
}
