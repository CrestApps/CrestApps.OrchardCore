namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// Represents the data needed to originate an Asterisk ARI channel into a Stasis application.
/// </summary>
internal sealed class AsteriskAriOriginateRequest
{
    /// <summary>
    /// Gets or sets the Asterisk endpoint to originate.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the caller id to present to the endpoint.
    /// </summary>
    public string CallerId { get; set; }

    /// <summary>
    /// Gets or sets the deterministic ARI channel identifier.
    /// </summary>
    public string ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the Stasis application name.
    /// </summary>
    public string App { get; set; }

    /// <summary>
    /// Gets or sets the Stasis application arguments.
    /// </summary>
    public IReadOnlyList<string> AppArgs { get; set; } = [];

    /// <summary>
    /// Gets or sets the channel variables to stamp on the originated channel.
    /// </summary>
    public IReadOnlyDictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the originate timeout, in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; }
}
