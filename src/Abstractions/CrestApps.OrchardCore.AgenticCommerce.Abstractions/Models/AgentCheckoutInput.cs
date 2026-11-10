using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// Everything an agent gives a new checkout.
/// </summary>
public sealed class AgentCheckoutInput
{
    /// <summary>
    /// Gets or sets the items.
    /// </summary>
    public IList<AgentCheckoutLineInput> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets the buyer.
    /// </summary>
    public AgentBuyer Buyer { get; set; }

    /// <summary>
    /// Gets or sets how the order reaches the buyer.
    /// </summary>
    public AgentFulfillmentInput Fulfillment { get; set; }

    /// <summary>
    /// Gets or sets the discount codes.
    /// </summary>
    public IList<string> DiscountCodes { get; set; } = [];

    /// <summary>
    /// Gets or sets the billing address.
    /// </summary>
    public Address BillingAddress { get; set; }

    /// <summary>
    /// Gets or sets where the buyer came from, such as a campaign, kept with the order.
    /// </summary>
    public JsonObject Attribution { get; set; }

    /// <summary>
    /// Gets or sets what the agent observed about the buyer, such as their IP address, kept for the risk check.
    /// </summary>
    public IDictionary<string, string> Signals { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the buyer's consent by purpose, such as marketing e-mails.
    /// </summary>
    public IDictionary<string, bool> Consents { get; set; } = new Dictionary<string, bool>();

    /// <summary>
    /// Gets or sets the buyer's notes for the store.
    /// </summary>
    public string OrderNotes { get; set; }
}
