namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// One stale-state flag raised on an agent.
/// </summary>
/// <param name="Kind">What is stale.</param>
/// <param name="Detail">The reason the detection gave, or the record the flag is about, when there is one.</param>
/// <param name="SinceUtc">When the stale state began, when that is known.</param>
internal sealed record AgentAvailabilityFlag(AgentAvailabilityFlagKind Kind, string Detail, DateTime? SinceUtc);
