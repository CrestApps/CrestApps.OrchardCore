using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// How the last pushes of the products to agent platforms went, and the feeds the store created on them.
/// </summary>
public sealed class AgentFeedStatusDocument : Document
{
    /// <summary>
    /// Gets or sets the last SFTP push of the OpenAI feed, or <see langword="null"/> before the first.
    /// </summary>
    public AgentFeedPushResult LastSftpPush { get; set; }

    /// <summary>
    /// Gets or sets the last push to each ACP feed API, by the target's id.
    /// </summary>
    public Dictionary<string, AgentFeedPushResult> FeedApiPushes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the ids of the feeds the store created on ACP feed APIs, by the target's id.
    /// </summary>
    public Dictionary<string, string> FeedApiFeedIds { get; set; } = new(StringComparer.Ordinal);
}
