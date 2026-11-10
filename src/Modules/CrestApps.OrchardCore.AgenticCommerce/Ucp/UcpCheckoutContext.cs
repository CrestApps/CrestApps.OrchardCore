using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// A UCP checkout being answered or completed, which <see cref="IUcpCheckoutExtension"/>s add to or check.
/// </summary>
public sealed class UcpCheckoutContext
{
    /// <summary>
    /// Gets or sets the request.
    /// </summary>
    public AgentRequest Request { get; set; }

    /// <summary>
    /// Gets or sets the agent checkout.
    /// </summary>
    public AgentCheckout Checkout { get; set; }

    /// <summary>
    /// Gets or sets where it stands.
    /// </summary>
    public AgentCheckoutView View { get; set; }

    /// <summary>
    /// Gets or sets the checkout as UCP JSON: the answer being written, or the body of a complete request.
    /// </summary>
    public JsonObject Json { get; set; }
}
