namespace CrestApps.OrchardCore.AgenticCommerce.Core.Models;

/// <summary>
/// A payment the buyer's bank asked to authenticate with 3-D Secure, which the agent runs itself: the checkout waits for
/// the agent to complete it again with the result.
/// </summary>
public sealed class AgentPendingAuthentication
{
    /// <summary>
    /// Gets or sets the payment provider the payment was begun with; the result is only taken for the same one.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the card network's directory server the agent authenticates with.
    /// </summary>
    public string DirectoryServer { get; set; }

    /// <summary>
    /// Gets or sets when the checkout asked for the authentication.
    /// </summary>
    public DateTime RequestedUtc { get; set; }
}
