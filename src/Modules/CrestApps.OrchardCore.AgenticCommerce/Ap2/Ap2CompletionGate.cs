using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce.Ap2;

/// <summary>
/// Binds an AP2 checkout to the buyer's checkout mandate inside the agent checkout's completion, so REST, MCP and A2A
/// all pass it. A checkout made while AP2 was agreed keeps needing a mandate (<see cref="RequiredProperty"/>), whatever
/// a later request negotiates; a complete request may not change it; and the mandate must be for the checkout as it
/// will be paid: the same id, items, total and currency, for the same buyer, shipped to the same place the same way.
/// </summary>
public sealed class Ap2CompletionGate : IAgentCheckoutCompletionGate
{
    /// <summary>
    /// The storage key that marks a checkout made while AP2 was agreed, and a request that agrees it.
    /// </summary>
    public const string RequiredProperty = "ap2.required";

    private readonly IMerchantSigningKeyService _keys;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="Ap2CompletionGate"/> class.
    /// </summary>
    /// <param name="keys">The store's signing keys.</param>
    /// <param name="clock">The clock.</param>
    public Ap2CompletionGate(IMerchantSigningKeyService keys, IClock clock)
    {
        _keys = keys;
        _clock = clock;
    }

    /// <summary>
    /// Gets the mandate this request's completion was verified with, for the receipt, or <see langword="null"/>.
    /// </summary>
    internal Ap2MandateVerificationResult Verified { get; private set; }

    /// <summary>
    /// Gets the verified mandate as the agent sent it.
    /// </summary>
    internal string VerifiedMandate { get; private set; }

    /// <summary>
    /// Returns whether AP2 applies to a completion: the checkout was made under AP2, or the request agrees it.
    /// </summary>
    /// <param name="context">The completion.</param>
    public static bool Applies(AgentCheckoutCompletionContext context)
        => context is not null &&
            (string.Equals(context.Checkout?.GetProperty(RequiredProperty), "true", StringComparison.Ordinal) ||
                (context.Context?.Properties is { } properties && properties.TryGetValue(RequiredProperty, out var value) && value == "true"));

    /// <inheritdoc />
    public Task<AgentCheckoutMessage> CheckChangesAsync(AgentCheckoutCompletionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The buyer approved the checkout as it stood; changing it in the same request that pays it would pay for
        // something they never saw.
        return Task.FromResult(Applies(context) && context.Update is not null && Changes(context.Checkout, context.Update)
            ? Refuse(Ap2ErrorCodes.MandateScopeMismatch, "An AP2 checkout can't be changed when it's completed. Update it, have the buyer approve it, then complete it.")
            : null);
    }

    /// <inheritdoc />
    public async Task<AgentCheckoutMessage> CheckAsync(AgentCheckoutCompletionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!Applies(context))
        {
            return null;
        }

        var mandate = context.Payment?.Properties is { } properties && properties.TryGetValue(Ap2CheckoutExtension.MandateProperty, out var value) ? value : null;

        if (string.IsNullOrWhiteSpace(mandate))
        {
            return Refuse(Ap2ErrorCodes.MandateRequired, "Complete an AP2 checkout with the buyer's checkout mandate.");
        }

        var platformKeys = JsonWebKeySet.Parse(context.Context?.PlatformProfile?["keys"]);

        if (platformKeys.Count == 0)
        {
            return Refuse(Ap2ErrorCodes.AgentMissingKey, "The platform's profile publishes no key to verify its mandates with.");
        }

        var merchantKeys = await _keys.GetPublishedKeysAsync(cancellationToken);
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

        if (!Matches(result.Checkout, context.View, context.Context?.ProtocolVersion))
        {
            return Refuse(Ap2ErrorCodes.MandateScopeMismatch, "The checkout mandate is for another checkout, or the checkout changed since the buyer approved it.");
        }

        Verified = result;
        VerifiedMandate = mandate;

        return null;
    }

    // The mandate is for this checkout as it stands: the same id, items, total and currency, the same buyer's e-mail
    // address, and the same fulfillment.
    internal static bool Matches(JsonObject approved, AgentCheckoutView view, string version)
    {
        if (approved is null || view is null || !string.Equals(AgentJson.String(approved, "id"), view.Id, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(AgentJson.String(approved, "currency"), view.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Who it's for: what has nothing to ship is delivered to the buyer's e-mail address.
        if (!string.Equals(AgentJson.String(approved["buyer"], "email")?.Trim() ?? string.Empty, view.Buyer?.Email?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase))
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

        if (!approvedLines.SequenceEqual(currentLines, StringComparer.Ordinal))
        {
            return false;
        }

        // Where it ships and how: the selected destination (its address) and the selected option of each method.
        var current = view.Fulfillment?.Required == true ? UcpCheckoutMapper.WriteFulfillment(view, version) : null;

        return string.Equals(Fingerprint(approved["fulfillment"] as JsonObject), Fingerprint(current), StringComparison.Ordinal);
    }

    private static string Fingerprint(JsonObject fulfillment)
    {
        if (fulfillment?["methods"] is not JsonArray methods)
        {
            return string.Empty;
        }

        var parts = new List<string>();

        foreach (var method in methods.OfType<JsonObject>())
        {
            var builder = new StringBuilder(AgentJson.String(method, "type"));
            var selected = AgentJson.String(method, "selected_destination_id");

            if (selected is not null && method["destinations"] is JsonArray destinations &&
                destinations.OfType<JsonObject>().FirstOrDefault(destination => AgentJson.String(destination, "id") == selected) is { } destination)
            {
                foreach (var (key, field) in destination.Where(pair => pair.Key is not "id" and not "name").OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    builder.Append('|').Append(key).Append('=').Append(field?.ToJsonString());
                }
            }
            else if (selected is not null)
            {
                builder.Append("|destination=").Append(selected);
            }

            foreach (var group in (method["groups"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                builder.Append("|option=").Append(AgentJson.String(group, "selected_option_id"));
            }

            parts.Add(builder.ToString());
        }

        return string.Join(';', parts.Order(StringComparer.Ordinal));
    }

    private static bool Changes(AgentCheckout checkout, AgentCheckoutUpdate update)
    {
        if (update.Lines is { Count: > 0 } || (update.ClearFulfillment && checkout.Fulfillment is not null))
        {
            return true;
        }

        if (update.Buyer is not null && !SameJson(update.Buyer, checkout.Buyer))
        {
            return true;
        }

        if (update.Fulfillment is not null && !SameJson(update.Fulfillment, checkout.Fulfillment))
        {
            return true;
        }

        return update.DiscountCodes is not null &&
            !new HashSet<string>(update.DiscountCodes.Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code.Trim()), StringComparer.OrdinalIgnoreCase)
                .SetEquals(checkout.DiscountCodes ?? []);
    }

    private static bool SameJson<T>(T left, T right)
        => string.Equals(JsonSerializer.Serialize(left), JsonSerializer.Serialize(right), StringComparison.Ordinal);

    private static EcJsonWebKey FindKey(IReadOnlyList<EcJsonWebKey> keys, string kid)
        => string.IsNullOrEmpty(kid) && keys.Count == 1
            ? keys[0]
            : keys.FirstOrDefault(key => string.Equals(key.Kid, kid, StringComparison.Ordinal));

    private static AgentCheckoutMessage Refuse(string code, string content)
        => AgentCheckoutMessage.Error(code, AgentCheckoutFields.Payment, content, AgentMessageSeverity.Unrecoverable);
}
