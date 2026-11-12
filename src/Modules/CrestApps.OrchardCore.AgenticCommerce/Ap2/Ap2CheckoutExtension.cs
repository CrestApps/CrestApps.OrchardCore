using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce.Ap2;

/// <summary>
/// The AP2 mandates extension of UCP checkout. Every answer carries the store's signature over the checkout
/// (<c>ap2.merchant_authorization</c>), and a complete request needs the buyer's checkout mandate
/// (<c>ap2.checkout_mandate</c>) for exactly that checkout, which <see cref="Ap2CompletionGate"/> verifies before the
/// agent pays.
/// </summary>
public sealed class Ap2CheckoutExtension : IUcpCheckoutExtension
{
    /// <summary>
    /// The storage key of the mandate a completed checkout was paid with.
    /// </summary>
    public const string MandateProperty = "ap2.checkout_mandate";

    /// <summary>
    /// The storage key of the store's signed receipt.
    /// </summary>
    public const string ReceiptProperty = "ap2.checkout_receipt";

    private readonly IMerchantSigningKeyService _keys;
    private readonly IAgentCheckoutStore _checkouts;
    private readonly AgentSiteUrls _urls;
    private readonly IClock _clock;
    private readonly Ap2CompletionGate _gate;

    public Ap2CheckoutExtension(
        IMerchantSigningKeyService keys,
        IAgentCheckoutStore checkouts,
        AgentSiteUrls urls,
        IClock clock,
        Ap2CompletionGate gate)
    {
        _keys = keys;
        _checkouts = checkouts;
        _urls = urls;
        _clock = clock;
        _gate = gate;
    }

    /// <inheritdoc />
    public async Task WritingAsync(UcpCheckoutContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Request.Has(Ap2ProfileContributor.GetName(context.Request.Version)))
        {
            return;
        }

        var key = await _keys.GetSigningKeyAsync();

        context.Json.Remove("ap2");

        var ap2 = new JsonObject
        {
            ["merchant_authorization"] = Ap2CheckoutAuthorization.CreateMerchantAuthorization(context.Json, key),
        };

        if (context.Checkout?.GetProperty(ReceiptProperty) is { } receipt)
        {
            ap2["checkout_receipt"] = receipt;
        }

        context.Json["ap2"] = ap2;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The mandate is checked by <see cref="Ap2CompletionGate"/> inside the agent checkout's completion, after the
    /// complete request's own changes and the final sync, so every transport passes the same check.
    /// </remarks>
    public Task<AgentCheckoutMessage> CompletingAsync(UcpCheckoutContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult<AgentCheckoutMessage>(null);
    }

    /// <inheritdoc />
    public async Task CompletedAsync(UcpCheckoutContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_gate.Verified is not { } result || context.Checkout is null)
        {
            return;
        }

        // The mandate and the store's receipt are the evidence a dispute is settled with.
        var receipt = Ap2Receipts.CreateCheckoutReceipt(
            result.ClosedMandateHash,
            Ap2Receipts.StatusSuccess,
            await _urls.GetBaseUrlAsync(),
            new DateTimeOffset(_clock.UtcNow, TimeSpan.Zero),
            await _keys.GetSigningKeyAsync());

        // The checkout was committed when it completed, which detaches the copy in the context; saving that copy would
        // insert a second row. The evidence goes onto the stored checkout instead.
        var checkout = await _checkouts.FindByIdAsync(context.Checkout.ItemId) ?? context.Checkout;

        checkout.SetProperty(MandateProperty, _gate.VerifiedMandate);
        checkout.SetProperty(ReceiptProperty, receipt);

        await _checkouts.UpdateAsync(checkout);
    }
}
