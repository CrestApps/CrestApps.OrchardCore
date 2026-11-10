using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Models;

/// <summary>
/// A business whose AI agents may buy from the store, such as Google or OpenAI: how it signs in, where its order updates
/// go, and how its agents may pay.
/// </summary>
public sealed class AgentPlatform : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the platform's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets notes about the platform.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether its agents may buy.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the platform is testing: its agents may only pay with test credentials.
    /// </summary>
    public bool TestMode { get; set; }

    /// <summary>
    /// Gets or sets the protocols its agents use, from <see cref="AgenticCommerceConstants.Protocols"/>.
    /// </summary>
    public List<string> Protocols { get; set; } = [];

    /// <summary>
    /// Gets or sets the client ID of its OAuth client on the store's OpenID server, which signs in with a bearer token.
    /// </summary>
    public string OAuthClientId { get; set; }

    /// <summary>
    /// Gets or sets the hash of its API key, which it sends as a bearer token.
    /// </summary>
    public string ApiKeyHash { get; set; }

    /// <summary>
    /// Gets or sets the last characters of its API key, to tell keys apart.
    /// </summary>
    public string ApiKeyHint { get; set; }

    /// <summary>
    /// Gets or sets the address of its UCP profile, whose keys verify its signed requests.
    /// </summary>
    public string ProfileUrl { get; set; }

    /// <summary>
    /// Gets or sets where its order updates go, when its protocol doesn't say.
    /// </summary>
    public string WebhookUrl { get; set; }

    /// <summary>
    /// Gets or sets the protected secret its order updates are signed with.
    /// </summary>
    public string ProtectedWebhookSecret { get; set; }

    /// <summary>
    /// Gets or sets the payment handlers its agents may pay through, by name; empty allows all.
    /// </summary>
    public List<string> AllowedPaymentHandlers { get; set; } = [];

    /// <summary>
    /// Gets or sets the IP addresses and ranges (CIDR) its requests come from; empty allows any.
    /// </summary>
    public List<string> AllowedIpRanges { get; set; } = [];

    /// <summary>
    /// Gets or sets the name of the ACP event a new order sends; <c>order_create</c> when empty.
    /// </summary>
    public string AcpOrderCreatedEvent { get; set; }

    /// <summary>
    /// Gets or sets the name of the ACP event a changed order sends; <c>order_update</c> when empty.
    /// </summary>
    public string AcpOrderUpdatedEvent { get; set; }

    /// <summary>
    /// Gets or sets when it was added.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Returns whether the platform uses a protocol.
    /// </summary>
    /// <param name="protocol">The protocol.</param>
    public bool Uses(string protocol)
        => Protocols?.Contains(protocol, StringComparer.OrdinalIgnoreCase) == true;
}
