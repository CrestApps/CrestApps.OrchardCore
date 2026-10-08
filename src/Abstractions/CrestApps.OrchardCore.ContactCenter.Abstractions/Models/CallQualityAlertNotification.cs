namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Tells supervisors that an agent's calls keep rating poor, and what is most likely causing it.
/// </summary>
public sealed class CallQualityAlertNotification
{
    /// <summary>
    /// Gets or sets the agent whose calls rated poor.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the user the agent signs in as.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets how many of the agent's recent calls rated poor.
    /// </summary>
    public int PoorCallCount { get; set; }

    /// <summary>
    /// Gets or sets how many of the agent's recent calls were looked at.
    /// </summary>
    public int RecentCallCount { get; set; }

    /// <summary>
    /// Gets or sets the most likely cause, as a <see cref="CallQualityCause"/> name.
    /// </summary>
    public string LikelyCause { get; set; }

    /// <summary>
    /// Gets or sets the interaction of the call that raised the alert, when it was a routed call.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets when the call that raised the alert ended.
    /// </summary>
    public DateTime ObservedUtc { get; set; }
}
