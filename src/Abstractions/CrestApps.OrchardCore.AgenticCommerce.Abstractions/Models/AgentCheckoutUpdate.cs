using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// What an agent changes on a checkout. A <see langword="null"/> member is left as it is.
/// </summary>
public sealed class AgentCheckoutUpdate
{
    /// <summary>
    /// Gets or sets the items, which replace the earlier ones.
    /// </summary>
    public IList<AgentCheckoutLineInput> Lines { get; set; }

    /// <summary>
    /// Gets or sets the buyer.
    /// </summary>
    public AgentBuyer Buyer { get; set; }

    /// <summary>
    /// Gets or sets how the order reaches the buyer.
    /// </summary>
    public AgentFulfillmentInput Fulfillment { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to clear how the order reaches the buyer.
    /// </summary>
    public bool ClearFulfillment { get; set; }

    /// <summary>
    /// Gets or sets the discount codes, which replace the earlier ones; empty clears them.
    /// </summary>
    public IList<string> DiscountCodes { get; set; }

    /// <summary>
    /// Gets or sets the billing address.
    /// </summary>
    public Address BillingAddress { get; set; }

    /// <summary>
    /// Gets or sets where the buyer came from.
    /// </summary>
    public JsonObject Attribution { get; set; }

    /// <summary>
    /// Gets or sets what the agent observed about the buyer.
    /// </summary>
    public IDictionary<string, string> Signals { get; set; }

    /// <summary>
    /// Gets or sets the buyer's consent by purpose.
    /// </summary>
    public IDictionary<string, bool> Consents { get; set; }

    /// <summary>
    /// Gets or sets the buyer's notes for the store.
    /// </summary>
    public string OrderNotes { get; set; }
}
