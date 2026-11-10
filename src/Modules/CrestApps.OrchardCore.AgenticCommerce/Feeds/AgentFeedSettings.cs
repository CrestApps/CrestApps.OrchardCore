namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Settings › Commerce › Agent feeds: the countries the OpenAI feed targets, the daily SFTP push, and the agent
/// platforms that host an ACP feed API.
/// </summary>
public sealed class AgentFeedSettings
{
    /// <summary>
    /// The purpose the settings' secrets are protected for.
    /// </summary>
    public const string ProtectionPurpose = "CrestApps.OrchardCore.AgenticCommerce.Feeds";

    /// <summary>
    /// Gets or sets the two-letter codes of the countries the store sells to through agents, such as <c>US</c>; the
    /// store's own country when empty.
    /// </summary>
    public List<string> TargetCountries { get; set; } = [];

    /// <summary>
    /// Gets or sets the daily push of the OpenAI feed over SFTP.
    /// </summary>
    public AgentFeedSftpSettings Sftp { get; set; } = new();

    /// <summary>
    /// Gets or sets the agent platforms the store pushes its products to through their ACP feed API.
    /// </summary>
    public List<AcpFeedTarget> FeedApiTargets { get; set; } = [];
}
