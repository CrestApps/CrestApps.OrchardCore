using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// A transport the store serves UCP's shopping service over.
/// </summary>
public sealed class UcpService
{
    /// <summary>
    /// Gets or sets the transport: <c>rest</c>, <c>mcp</c>, <c>a2a</c> or <c>embedded</c>.
    /// </summary>
    public string Transport { get; set; }

    /// <summary>
    /// Gets or sets the address of its schema.
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// Gets or sets its absolute endpoint.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets its configuration, such as the delegations the embedded transport accepts.
    /// </summary>
    public JsonObject Config { get; set; }
}
