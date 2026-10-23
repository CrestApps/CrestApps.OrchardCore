using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// A persisted checkout session. It is the provider-agnostic unit of work for any purchase flow
/// (subscriptions, one-time goods, ...). Its property bag carries the <see cref="CheckoutInvoice"/>,
/// provider metadata, and <see cref="PaymentsMetadata"/>.
/// </summary>
public sealed class CheckoutSession : Entity, ICheckoutFlowSession
{
    /// <summary>
    /// The YesSql document identity.
    /// </summary>
    /// <remarks>
    /// It matters more than it looks. YesSql recognises a document it has already stored by this property;
    /// without one, saving a session again after a commit inserts a second document rather than updating the
    /// first, and the checkout then exists twice — once pending and once completed.
    /// </remarks>
    public long Id { get; set; }

    /// <summary>
    /// Whether this checkout pays off a balance that already exists, such as an outstanding transaction, rather
    /// than buying something new. Only a provider that collects money can complete it: a provider that
    /// <see cref="Services.PaymentProviderCapabilities.DefersPayment"/> would mark the balance paid with nothing
    /// collected.
    /// </summary>
    public bool SettlesExistingBalance { get; set; }

    /// <summary>
    /// Whether the payment method the customer pays with is kept so it can be charged again later without them
    /// present, for example for the remaining payments of an installment plan they chose at checkout. Only a provider
    /// that <see cref="Services.PaymentProviderCapabilities.SupportsSavedPaymentMethods"/> is offered or accepted,
    /// and the engine asks it to keep the method (<see cref="Services.CheckoutPaymentDataKeys.SavePaymentMethod"/>).
    /// </summary>
    /// <remarks>
    /// Only server code sets this. Whether a card is kept is never decided by what the customer's browser sends.
    /// </remarks>
    public bool SavePaymentMethod { get; set; }

    /// <summary>
    /// Whether the module that settles the existing balance (<see cref="SettlesExistingBalance"/>) takes what the
    /// invoice's discounts take off off that balance, so a promotion code can be used on this checkout. That module
    /// also redeems the codes, once the balance is settled; the checkout does not redeem them on completion. When it
    /// is <see langword="false"/>, a checkout that settles an existing balance offers and applies no discounts.
    /// </summary>
    public bool AcceptsDiscountsOnBalance { get; set; }

    /// <summary>
    /// The unique identifier of the checkout session.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// The kind of thing being purchased (for example a content type or an application-defined reference type).
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// The identifier of the thing being purchased.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// An optional secondary identifier of the thing being purchased (for example a content item version id).
    /// </summary>
    public string ReferenceVersionId { get; set; }

    /// <summary>
    /// The things the checkout pays for when it pays for several at once, such as several outstanding transactions
    /// settled with one payment. They all share <see cref="ReferenceType"/>, and <see cref="ReferenceId"/> is then
    /// empty. Empty for a checkout that pays for one thing, which <see cref="ReferenceId"/> names.
    /// </summary>
    public IList<CheckoutReference> References { get; init; } = [];

    /// <summary>
    /// The ISO-4217 currency code for the checkout.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// The lifecycle state of the checkout.
    /// </summary>
    public CheckoutSessionStatus Status { get; set; }

    /// <summary>
    /// The UTC time the session was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The UTC time the session was last modified.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// The UTC time the session completed, when applicable.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// The identifier of the authenticated user that owns the session, when applicable.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// The data collected for each completed step, keyed by step key.
    /// </summary>
    public JsonObject SavedSteps { get; init; } = [];

    /// <summary>
    /// The ordered steps that make up this checkout.
    /// </summary>
    public IList<CheckoutFlowStep> Steps { get; init; } = [];

    /// <summary>
    /// The key of the step the customer is currently on.
    /// </summary>
    public string CurrentStep { get; set; }

    /// <summary>
    /// The client IP address captured for anonymous sessions. It is recorded for audit only and must never
    /// be used to decide who may resume a session: everyone behind one router shares it.
    /// </summary>
    public string IPAddress { get; set; }

    /// <summary>
    /// The user agent captured for anonymous sessions. It is recorded for audit only and must never be used
    /// to decide who may resume a session: it is neither secret nor unique.
    /// </summary>
    public string AgentInfo { get; set; }

    /// <summary>
    /// The SHA-256 hash of the ownership token issued to the guest who started this session. Only that
    /// browser holds the token, so it is what actually proves ownership. Only the hash is stored, so a
    /// leaked database does not hand an attacker the ability to resume live checkouts.
    /// </summary>
    public string GuestTokenHash { get; set; }

    /// <summary>
    /// The UTC time the buyer's personal data was removed from the session, at their request or after a retention
    /// window, or <see langword="null"/> while it is still there.
    /// </summary>
    public DateTime? AnonymizedUtc { get; set; }

    /// <summary>
    /// Removes who the session was for: the owning account, the buyer's contact (their billing address keeps only its
    /// country and region, which tax records need), the IP address, the user agent and what was typed into each step.
    /// The amounts, invoice, status and dates stay for the store's accounts.
    /// </summary>
    /// <remarks>
    /// A session that is still pending can no longer be resumed afterwards: it has no owner, and the guest token only
    /// ever proved who started it. The modification time is left alone, so the abandoned-checkout sweep still expires
    /// it on time.
    /// </remarks>
    /// <param name="now">The current UTC time, recorded as <see cref="AnonymizedUtc"/> the first time.</param>
    /// <param name="replacementName">The text the buyer's name is replaced with, or <see langword="null"/> to clear it.</param>
    public void Anonymize(DateTime now, string replacementName = null)
    {
        if (this.TryGet<CheckoutContactInfo>(out var contact) && contact is not null)
        {
            this.Put(new CheckoutContactInfo
            {
                DisplayName = string.IsNullOrEmpty(contact.DisplayName) ? null : replacementName,
                BillingAddress = contact.BillingAddress is null
                    ? null
                    : new Address
                    {
                        Country = contact.BillingAddress.Country,
                        Region = contact.BillingAddress.Region,
                    },
            });
        }

        OwnerId = null;
        IPAddress = null;
        AgentInfo = null;
        SavedSteps?.Clear();
        AnonymizedUtc ??= now;
    }
}
