using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AgenticCommerce.Endpoints;

/// <summary>
/// A request an agent platform made, once it is signed in: who it is, what it sent, and the agent checkout context its
/// protocol agreed.
/// </summary>
public sealed class AgentRequest
{
    private const string ItemKey = "CrestApps.AgenticCommerce.AgentRequest";

    /// <summary>
    /// Gets or sets the platform that made the request.
    /// </summary>
    public AgentPlatform Platform { get; set; }

    /// <summary>
    /// Gets or sets the raw body, exactly as it was sent.
    /// </summary>
    public byte[] Body { get; set; } = [];

    /// <summary>
    /// Gets or sets the body as JSON, or <see langword="null"/> when it is empty or isn't JSON.
    /// </summary>
    public JsonNode Json { get; set; }

    /// <summary>
    /// Gets or sets the context the agent checkout is driven with.
    /// </summary>
    public AgentCheckoutContext Context { get; set; }

    /// <summary>
    /// Gets or sets the protocol version agreed for the request.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the capabilities agreed for the request, by name, with their versions.
    /// </summary>
    public IDictionary<string, string> Capabilities { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the platform's UCP profile, when it has one.
    /// </summary>
    public JsonObject PlatformProfile { get; set; }

    /// <summary>
    /// Gets or sets the OAuth scopes the platform's token grants.
    /// </summary>
    public IReadOnlyCollection<string> Scopes { get; set; } = [];

    /// <summary>
    /// Gets or sets the linked buyer the token was issued for, even when its scopes don't let the agent check out as
    /// them.
    /// </summary>
    public string LinkedUserId { get; set; }

    /// <summary>
    /// Gets or sets the address of the platform's UCP profile.
    /// </summary>
    public string ProfileUrl { get; set; }

    /// <summary>
    /// Returns whether a capability was agreed for the request.
    /// </summary>
    /// <param name="name">The capability's name.</param>
    public bool Has(string name)
        => Capabilities.ContainsKey(name);

    /// <summary>
    /// Returns the request a filter signed in, or <see langword="null"/>.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    public static AgentRequest Get(HttpContext httpContext)
        => httpContext?.Items.TryGetValue(ItemKey, out var value) == true ? value as AgentRequest : null;

    /// <summary>
    /// Keeps the request for the endpoint.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    public void Set(HttpContext httpContext)
        => httpContext.Items[ItemKey] = this;
}
