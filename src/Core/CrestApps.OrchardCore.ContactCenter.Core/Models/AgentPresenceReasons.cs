namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The reasons the platform itself gives an agent's not-ready state, as they read on the agent's screens.
/// </summary>
public static class AgentPresenceReasons
{
    /// <summary>
    /// The agent accepted an offer, but the leg rung to their phone was refused or never answered, so routing set them
    /// Away rather than offer them the next caller too.
    /// </summary>
    public const string PhoneUnreachable = "Phone unreachable";
}
