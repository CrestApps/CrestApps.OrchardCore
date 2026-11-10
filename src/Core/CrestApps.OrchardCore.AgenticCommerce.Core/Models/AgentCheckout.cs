using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.AgenticCommerce.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Models;

/// <summary>
/// A checkout an AI agent drives. It keeps what the agent asked for and the store checkout that holds it: when the agent
/// changes the items, a new store checkout replaces the old one, and the agent keeps the same id.
/// </summary>
public sealed class AgentCheckout : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the agent platform that made it.
    /// </summary>
    public string PlatformId { get; set; }

    /// <summary>
    /// Gets or sets the protocol it was made through.
    /// </summary>
    public string Protocol { get; set; }

    /// <summary>
    /// Gets or sets the protocol version agreed when it was made.
    /// </summary>
    public string ProtocolVersion { get; set; }

    /// <summary>
    /// Gets or sets where it stands, once it is completed, canceled or expired; open ones are worked out each time.
    /// </summary>
    public AgentCheckoutStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the store checkout that holds it now.
    /// </summary>
    public string CheckoutSessionId { get; set; }

    /// <summary>
    /// Gets or sets the order of the store checkout.
    /// </summary>
    public string OrderId { get; set; }

    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the user a linked account belongs to, when the agent buys as a signed-in buyer.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the items.
    /// </summary>
    public List<AgentCheckoutLineInput> Lines { get; set; } = [];

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
    public List<string> DiscountCodes { get; set; } = [];

    /// <summary>
    /// Gets or sets the billing address.
    /// </summary>
    public Address BillingAddress { get; set; }

    /// <summary>
    /// Gets or sets a fingerprint of the items the store checkout was made for.
    /// </summary>
    public string AppliedLines { get; set; }

    /// <summary>
    /// Gets or sets a fingerprint of the buyer details saved on the store checkout.
    /// </summary>
    public string AppliedContact { get; set; }

    /// <summary>
    /// Gets or sets a fingerprint of the fulfillment saved on the store checkout.
    /// </summary>
    public string AppliedFulfillment { get; set; }

    /// <summary>
    /// Gets or sets where the buyer came from, kept with the order.
    /// </summary>
    public JsonObject Attribution { get; set; }

    /// <summary>
    /// Gets or sets what the agent observed about the buyer, kept until the payment settles.
    /// </summary>
    public Dictionary<string, string> Signals { get; set; } = [];

    /// <summary>
    /// Gets or sets the buyer's consent by purpose.
    /// </summary>
    public Dictionary<string, bool> Consents { get; set; } = [];

    /// <summary>
    /// Gets or sets the buyer's notes for the store.
    /// </summary>
    public string OrderNotes { get; set; }

    /// <summary>
    /// Gets or sets why the agent canceled it, as the agent gave it.
    /// </summary>
    public JsonObject IntentTrace { get; set; }

    /// <summary>
    /// Gets or sets what went wrong the last time the agent tried to pay.
    /// </summary>
    public AgentCheckoutMessage PaymentMessage { get; set; }

    /// <summary>
    /// Gets or sets where the buyer finishes a payment their bank asked them to approve.
    /// </summary>
    public string EscalationUrl { get; set; }

    /// <summary>
    /// Gets or sets the brand and last digits of the card the agent paid with, as the agent showed them.
    /// </summary>
    public string PaymentDisplay { get; set; }

    /// <summary>
    /// Gets or sets when it expires unless completed.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was completed.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Returns whether it can't change any more.
    /// </summary>
    public bool IsTerminal
        => Status is AgentCheckoutStatus.Completed or AgentCheckoutStatus.Canceled or AgentCheckoutStatus.Expired;

    /// <summary>
    /// Gets or sets protocol data, such as where a UCP platform's order updates go or an AP2 mandate.
    /// </summary>
    public Dictionary<string, string> ProtocolData { get; set; } = [];

    /// <summary>
    /// Returns a protocol value, or <see langword="null"/>.
    /// </summary>
    /// <param name="name">The value's name.</param>
    public string GetProperty(string name)
        => ProtocolData is not null && ProtocolData.TryGetValue(name, out var value) ? value : null;

    /// <summary>
    /// Sets a protocol value, or removes it when <see langword="null"/>.
    /// </summary>
    /// <param name="name">The value's name.</param>
    /// <param name="value">The value.</param>
    public void SetProperty(string name, string value)
    {
        ProtocolData ??= [];

        if (value is null)
        {
            ProtocolData.Remove(name);

            return;
        }

        ProtocolData[name] = value;
    }
}
