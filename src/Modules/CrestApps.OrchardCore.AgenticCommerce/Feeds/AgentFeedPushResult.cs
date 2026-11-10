namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// How a push of the products to an agent platform went.
/// </summary>
public sealed class AgentFeedPushResult
{
    /// <summary>
    /// Gets or sets when the push started.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the push ended.
    /// </summary>
    public DateTime FinishedUtc { get; set; }

    /// <summary>
    /// Gets or sets whether every product was pushed.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets how many items were pushed.
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// Gets or sets the files written, for a file push.
    /// </summary>
    public List<string> Files { get; set; } = [];

    /// <summary>
    /// Gets or sets what went wrong, or a note on what was done.
    /// </summary>
    public string Message { get; set; }
}
