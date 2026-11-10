using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// A call of one of ACP's MCP tools, read from its arguments <c>{ meta, id?, payload? }</c>.
/// </summary>
public sealed class AcpMcpCall
{
    /// <summary>
    /// Gets the tool's name, such as <c>create_checkout_session</c>.
    /// </summary>
    public string Tool { get; init; }

    /// <summary>
    /// Gets the checkout session's id, or <see langword="null"/> for <c>create_checkout_session</c>.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets the payload, in the REST binding's request schema, or <see langword="null"/>.
    /// </summary>
    public JsonObject Payload { get; init; }

    /// <summary>
    /// Gets <c>meta.api_version</c>.
    /// </summary>
    public string ApiVersion { get; init; }

    /// <summary>
    /// Gets <c>meta.idempotency_key</c>, or <see langword="null"/>.
    /// </summary>
    public string IdempotencyKey { get; init; }

    /// <summary>
    /// Gets <c>meta.request_id</c>, or <see langword="null"/>.
    /// </summary>
    public string RequestId { get; init; }

    /// <summary>
    /// Gets <c>meta.signature</c>, or <see langword="null"/>.
    /// </summary>
    public string Signature { get; init; }

    /// <summary>
    /// Gets <c>meta.timestamp</c>, or <see langword="null"/>.
    /// </summary>
    public string Timestamp { get; init; }

    /// <summary>
    /// Gets a value indicating whether the tool changes a checkout session, so it needs an idempotency key, like a POST
    /// in the REST binding.
    /// </summary>
    public bool IsMutating
        => Tool != AcpMcpEndpoints.Tools.Get;
}
