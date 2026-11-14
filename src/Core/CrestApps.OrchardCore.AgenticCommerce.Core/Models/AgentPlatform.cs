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
    /// The start of the id an unregistered UCP platform's checkouts are kept under. A registered platform's id never has
    /// it, and every unregistered platform shares one allowance of open checkouts.
    /// </summary>
    public const string UnregisteredIdPrefix = "ucp-";

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
    /// Gets or sets the origins (<c>https://host[:port]</c>) whose pages may show the store's checkout in a frame, for
    /// UCP embedded checkout. They aren't secret, so they travel with the platform in exports.
    /// </summary>
    public List<string> EmbeddedCheckoutOrigins { get; set; } = [];

    /// <summary>
    /// Gets or sets the name of the ACP event a new order sends; <c>order_create</c> when empty.
    /// </summary>
    public string AcpOrderCreatedEvent { get; set; }

    /// <summary>
    /// Gets or sets the name of the ACP event a changed order sends; <c>order_update</c> when empty.
    /// </summary>
    public string AcpOrderUpdatedEvent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether its ACP requests must be signed (<c>Signature</c> and <c>Timestamp</c>
    /// headers) with <see cref="AcpSignaturePublicKey"/>.
    /// </summary>
    public bool AcpRequireSignedRequests { get; set; }

    /// <summary>
    /// Gets or sets the algorithm its ACP requests are signed with, from
    /// <see cref="Security.AcpRequestSignature.Algorithms"/>; ES256 when empty.
    /// </summary>
    public string AcpSignatureAlgorithm { get; set; }

    /// <summary>
    /// Gets or sets the public key that verifies its signed ACP requests: an EC JWK or a PEM <c>PUBLIC KEY</c> on P-256.
    /// It isn't a secret, so it travels in deployments and recipes.
    /// </summary>
    public string AcpSignaturePublicKey { get; set; }

    /// <summary>
    /// Gets or sets how many seconds a signed ACP request's <c>Timestamp</c> may be from the store's clock; five minutes
    /// when empty.
    /// </summary>
    public int? AcpSignatureClockSkewSeconds { get; set; }

    /// <summary>
    /// Gets or sets what the platform's ACP signatures cover: 1 or empty, the body only (platforms set up before versions
    /// existed); 2, the timestamp, method and path with the body. New platforms use 2.
    /// </summary>
    public int? AcpSignatureVersion { get; set; }

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
