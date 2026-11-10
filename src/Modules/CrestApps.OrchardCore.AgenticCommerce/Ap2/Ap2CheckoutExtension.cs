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
/// (<c>ap2.checkout_mandate</c>) for exactly that checkout, which is verified before the agent pays.
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

    private const string VerificationItem = "ap2.verification";

    private readonly IMerchantSigningKeyService _keys;
    private readonly IAgentCheckoutStore _checkouts;
    private readonly AgentSiteUrls _urls;
    private readonly IClock _clock;

    public Ap2CheckoutExtension(
        IMerchantSigningKeyService keys,
        IAgentCheckoutStore checkouts,
        AgentSiteUrls urls,
        IClock clock)
    {
        _keys = keys;
        _checkouts = checkouts;
        _urls = urls;
        _clock = clock;
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
    public async Task<AgentCheckoutMessage> CompletingAsync(UcpCheckoutContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Request.Has(Ap2ProfileContributor.GetName(context.Request.Version)))
        {
            return null;
        }

        // Once AP2 is agreed the checkout is locked to mandates: there is no completing without one.
        var mandate = AgentJson.String(context.Json?["ap2"], "checkout_mandate");

        if (string.IsNullOrWhiteSpace(mandate))
        {
            return Refuse(Ap2ErrorCodes.MandateRequired, "Complete an AP2 checkout with the buyer's checkout mandate.");
        }

        var platformKeys = JsonWebKeySet.Parse(context.Request.PlatformProfile?["keys"]);

        if (platformKeys.Count == 0)
        {
            return Refuse(Ap2ErrorCodes.AgentMissingKey, "The platform's profile publishes no key to verify its mandates with.");
        }

        var merchantKeys = await _keys.GetPublishedKeysAsync();
        var result = new Ap2MandateVerifier().VerifyCheckoutMandate(mandate, new Ap2VerificationOptions
        {
            RootKeyResolver = root => FindKey(platformKeys, AgentJson.String(root?["header"], "kid")),
            MerchantKeyResolver = kid => FindKey(merchantKeys, kid),
            Now = new DateTimeOffset(_clock.UtcNow, TimeSpan.Zero),
        });

        if (!result.Succeeded)
        {
            return Refuse(result.ErrorCode ?? Ap2ErrorCodes.MandateInvalid, result.ErrorMessage ?? "The checkout mandate isn't valid.");
        }

        // The mandate is for this checkout as it stands: the same id, items and total the buyer approved.
        if (!Matches(result.Checkout, context.View))
        {
            return Refuse(Ap2ErrorCodes.MandateScopeMismatch, "The checkout mandate is for another checkout, or the checkout changed since the buyer approved it.");
        }

        context.Items[VerificationItem] = result;

        return null;
    }

    /// <inheritdoc />
    public async Task CompletedAsync(UcpCheckoutContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Items.TryGetValue(VerificationItem, out var value) || value is not Ap2MandateVerificationResult result || context.Checkout is null)
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

        context.Checkout.SetProperty(MandateProperty, AgentJson.String(context.Json?["ap2"], "checkout_mandate"));
        context.Checkout.SetProperty(ReceiptProperty, receipt);

        await _checkouts.UpdateAsync(context.Checkout);
    }

    private static bool Matches(JsonObject approved, AgentCheckoutView view)
    {
        if (approved is null || view is null || !string.Equals(AgentJson.String(approved, "id"), view.Id, StringComparison.Ordinal))
        {
            return false;
        }

        var total = (approved["totals"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(entry => AgentJson.String(entry, "type") == "total");

        if (total?["amount"] is not JsonValue amount || !amount.TryGetValue<long>(out var minor) || minor != AgentJson.ToMinor(view.Totals.Total, view.Currency))
        {
            return false;
        }

        var approvedLines = (approved["line_items"] as JsonArray)?
            .OfType<JsonObject>()
            .Select(line => $"{AgentJson.String(line["item"], "id")}x{AgentJson.Int(line, "quantity")}")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList() ?? [];
        var currentLines = view.Lines
            .Select(line => $"{line.ItemId}x{line.Quantity}")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        return approvedLines.SequenceEqual(currentLines, StringComparer.Ordinal);
    }

    private static EcJsonWebKey FindKey(IReadOnlyList<EcJsonWebKey> keys, string kid)
        => string.IsNullOrEmpty(kid) && keys.Count == 1
            ? keys[0]
            : keys.FirstOrDefault(key => string.Equals(key.Kid, kid, StringComparison.Ordinal));

    private static AgentCheckoutMessage Refuse(string code, string content)
        => AgentCheckoutMessage.Error(code, AgentCheckoutFields.Payment, content, AgentMessageSeverity.Unrecoverable);
}
