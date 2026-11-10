using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// An agent checkout as it stands: what each protocol renders in its own shape.
/// </summary>
public sealed class AgentCheckoutView
{
    /// <summary>
    /// Gets or sets the checkout's id, which the agent uses.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the protocol it was made through.
    /// </summary>
    public string Protocol { get; set; }

    /// <summary>
    /// Gets or sets where it stands.
    /// </summary>
    public AgentCheckoutStatus Status { get; set; }

    /// <summary>
    /// Gets or sets its currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets its items.
    /// </summary>
    public IList<AgentCheckoutLineView> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets its totals.
    /// </summary>
    public AgentCheckoutTotals Totals { get; set; } = new();

    /// <summary>
    /// Gets or sets the buyer.
    /// </summary>
    public AgentBuyer Buyer { get; set; }

    /// <summary>
    /// Gets or sets the billing address.
    /// </summary>
    public Address BillingAddress { get; set; }

    /// <summary>
    /// Gets or sets how the order reaches the buyer.
    /// </summary>
    public AgentFulfillmentView Fulfillment { get; set; } = new();

    /// <summary>
    /// Gets or sets the discount codes the agent gave.
    /// </summary>
    public IList<string> DiscountCodes { get; set; } = [];

    /// <summary>
    /// Gets or sets the discounts that apply.
    /// </summary>
    public IList<AgentAppliedDiscount> Discounts { get; set; } = [];

    /// <summary>
    /// Gets or sets what the checkout tells the agent.
    /// </summary>
    public IList<AgentCheckoutMessage> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets the links to the store's policies.
    /// </summary>
    public IList<AgentLink> Links { get; set; } = [];

    /// <summary>
    /// Gets or sets the address where the buyer finishes in the store's own checkout.
    /// </summary>
    public string ContinueUrl { get; set; }

    /// <summary>
    /// Gets or sets when it expires.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the order it placed, once completed.
    /// </summary>
    public AgentOrderReference Order { get; set; }
}
