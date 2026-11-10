using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// A payment handler as an agent sees it.
/// </summary>
public sealed class AgentPaymentHandlerDescriptor
{
    /// <summary>
    /// Gets or sets the handler instance's id, which the agent names when paying.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the handler's name, such as <c>com.stripe.payments</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the handler specification's version.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the address of the handler's specification.
    /// </summary>
    public string Spec { get; set; }

    /// <summary>
    /// Gets or sets the address of the handler's configuration schema.
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// Gets or sets the handler's configuration for the agent.
    /// </summary>
    public JsonObject Config { get; set; } = [];

    /// <summary>
    /// Gets or sets the instrument types it takes, such as <c>card</c>.
    /// </summary>
    public IList<string> InstrumentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the payment service provider, such as <c>stripe</c>.
    /// </summary>
    public string Psp { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agent gets its credential through delegated payment.
    /// </summary>
    public bool RequiresDelegatePayment { get; set; }

    /// <summary>
    /// Gets or sets the name the agent shows the buyer.
    /// </summary>
    public string DisplayName { get; set; }
}
