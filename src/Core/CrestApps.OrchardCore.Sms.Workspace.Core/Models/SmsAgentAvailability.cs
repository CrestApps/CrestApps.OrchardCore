namespace CrestApps.OrchardCore.Sms.Workspace.Core.Models;

/// <summary>
/// Per-agent availability for <b>routed</b> (push) SMS assignment. Stored in the agent profile's property bag so
/// it is fully independent of the agent's voice presence — an agent can accept routed SMS while signed out of
/// voice, and vice versa. When absent it resolves to the defaults below (available, with the default cap), which
/// keeps routed distribution working out of the box: an agent is eligible unless they explicitly pause, mirroring
/// the membership-based reception of the shared-pool inbox.
/// </summary>
public sealed class SmsAgentAvailability
{
    /// <summary>
    /// The default maximum number of concurrent open, routed SMS conversations an agent is assigned.
    /// </summary>
    public const int DefaultMaxConcurrent = 10;

    /// <summary>
    /// Gets or sets a value indicating whether the agent is accepting routed SMS assignments.
    /// </summary>
    public bool Available { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of concurrent open, routed SMS conversations to assign to the agent.
    /// A value of zero or less means the <see cref="DefaultMaxConcurrent"/> is used.
    /// </summary>
    public int MaxConcurrent { get; set; } = DefaultMaxConcurrent;

    /// <summary>
    /// Gets or sets the UTC time the availability was last changed.
    /// </summary>
    public DateTime? UpdatedUtc { get; set; }

    /// <summary>
    /// Gets the effective concurrency cap, applying the default when unset.
    /// </summary>
    public int EffectiveMaxConcurrent => MaxConcurrent > 0 ? MaxConcurrent : DefaultMaxConcurrent;
}
