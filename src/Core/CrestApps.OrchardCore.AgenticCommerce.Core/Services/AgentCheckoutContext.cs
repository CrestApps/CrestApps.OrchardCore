namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Who asks the agent checkout for something, through which protocol.
/// </summary>
public sealed class AgentCheckoutContext
{
    /// <summary>
    /// Gets or sets the agent platform's id.
    /// </summary>
    public string PlatformId { get; set; }

    /// <summary>
    /// Gets or sets the protocol, one of <see cref="AgenticCommerceConstants.Protocols"/>.
    /// </summary>
    public string Protocol { get; set; }

    /// <summary>
    /// Gets or sets the protocol version agreed for this request.
    /// </summary>
    public string ProtocolVersion { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the platform is testing, where only test credentials pay.
    /// </summary>
    public bool TestMode { get; set; }

    /// <summary>
    /// Gets or sets the payment handlers the platform may pay through, by name; empty allows all.
    /// </summary>
    public IReadOnlyCollection<string> AllowedPaymentHandlers { get; set; } = [];

    /// <summary>
    /// Gets or sets the user a linked account belongs to, when the agent buys as a signed-in buyer.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the address of the request's client, for rate limits on discount codes.
    /// </summary>
    public string ClientAddress { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agent said, when making a checkout, that it can run 3-D Secure with
    /// the buyer itself and send back the result (ACP's <c>3ds</c> intervention). It is kept with the new checkout.
    /// </summary>
    public bool RunsThreeDSecure { get; set; }

    /// <summary>
    /// Gets or sets protocol data kept with a new checkout, such as where a UCP platform's order updates go.
    /// </summary>
    public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Returns a value of <see cref="Properties"/>, or <see langword="null"/>.
    /// </summary>
    /// <param name="name">The value's name.</param>
    public string GetProperty(string name)
        => Properties is not null && Properties.TryGetValue(name, out var value) ? value : null;

    /// <summary>
    /// Returns who the limits on discount codes and declined cards count for this request: the agent platform, whose
    /// servers every request of its buyers comes from (and over MCP or A2A with no address at all), so one platform can't
    /// use up another's tries; or the request's address when no platform is known.
    /// </summary>
    public string GetClientKey()
        => !string.IsNullOrEmpty(PlatformId)
            ? global::CrestApps.OrchardCore.Core.Throttling.ClientNetworkKey.ForAgentPlatform(PlatformId)
            : ClientAddress;

    /// <summary>
    /// Gets or sets the platform's published profile, when its protocol has one (UCP): the keys its signatures and
    /// payment mandates are checked with.
    /// </summary>
    public System.Text.Json.Nodes.JsonObject PlatformProfile { get; set; }
}
