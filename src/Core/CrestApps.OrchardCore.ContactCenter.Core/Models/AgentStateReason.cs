namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// A reason given for an agent state, resolved against the configured reason codes.
/// </summary>
/// <param name="ReasonCodeId">The identifier of the matching reason code, or <see langword="null"/> for free text.</param>
/// <param name="Name">The reason as it reads now: the reason code's name, or the free text given.</param>
public sealed record AgentStateReason(string ReasonCodeId, string Name);
