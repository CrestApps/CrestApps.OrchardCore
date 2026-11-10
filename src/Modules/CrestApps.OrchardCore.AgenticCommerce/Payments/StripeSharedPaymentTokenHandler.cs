using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Stripe.Core;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Payments;

/// <summary>
/// Agents pay with a Stripe shared payment token (<c>spt_…</c>) their platform issued to the store's Stripe account:
/// UCP's <c>com.stripe.payments</c> handler and ACP's tokenized card with Stripe as the payment service provider.
/// </summary>
public sealed class StripeSharedPaymentTokenHandler : IAgentPaymentHandler
{
    /// <summary>
    /// The handler's name in UCP.
    /// </summary>
    public const string UcpName = "com.stripe.payments";

    /// <summary>
    /// The handler's name in ACP.
    /// </summary>
    public const string AcpName = "dev.acp.tokenized.card";

    private readonly IOptionsMonitor<StripeOptions> _stripeOptions;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public StripeSharedPaymentTokenHandler(
        IOptionsMonitor<StripeOptions> stripeOptions,
        ISiteService siteService,
        IStringLocalizer<StripeSharedPaymentTokenHandler> stringLocalizer)
    {
        _stripeOptions = stripeOptions;
        _siteService = siteService;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string GetName(string protocol)
        => protocol == AgenticCommerceConstants.Protocols.Acp ? AcpName : UcpName;

    /// <inheritdoc />
    public async Task<AgentPaymentHandlerDescriptor> DescribeAsync(AgentPaymentHandlerContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var stripe = _stripeOptions.CurrentValue;

        // A platform in test mode only pays with test credentials, and a store without Stripe takes no tokens.
        if (context.TestMode || !stripe.IsConfigured)
        {
            return null;
        }

        var settings = await _siteService.GetSettingsAsync<AgenticCommerceSettings>();
        var environment = stripe.IsLive ? "livemode" : "testmode";

        if (context.Protocol == AgenticCommerceConstants.Protocols.Acp)
        {
            var config = new JsonObject
            {
                ["environment"] = environment,
            };

            if (!string.IsNullOrEmpty(settings.StripeBusinessProfileId))
            {
                config["merchant_id"] = settings.StripeBusinessProfileId;
            }

            return new AgentPaymentHandlerDescriptor
            {
                Id = "stripe_card",
                Name = AcpName,
                Version = AgenticCommerceConstants.Versions.Acp20260417,
                Spec = "https://agenticcommerce.dev/specs/payment-handlers/tokenized-card",
                Schema = "https://agenticcommerce.dev/schemas/payment-handlers/tokenized-card/config.json",
                Psp = "stripe",
                RequiresDelegatePayment = true,
                DisplayName = S["Credit or debit card"],
                InstrumentTypes = ["card"],
                Config = config,
            };
        }

        // UCP agents issue the token to the store's Stripe business profile, so a store that hasn't given it can't be paid.
        if (string.IsNullOrEmpty(settings.StripeBusinessProfileId))
        {
            return null;
        }

        return new AgentPaymentHandlerDescriptor
        {
            Id = "stripe_payments",
            Name = UcpName,
            Version = "2026-06-25",
            Spec = "https://docs.stripe.com/agentic-commerce/protocols/ucp/stripe-payments-handler",
            Schema = "https://ucp.stripe.com/payments/2026-06-25/schema.json",
            Psp = "stripe",
            DisplayName = S["Credit or debit card"],
            InstrumentTypes = ["card"],
            Config = new JsonObject
            {
                ["environment"] = environment,
                ["business_profile"] = settings.StripeBusinessProfileId,
            },
        };
    }

    /// <inheritdoc />
    public Task<AgentPaymentPreparation> PrepareAsync(AgentPaymentHandlerContext context, AgentPayment payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);

        var token = payment.Credential?.Trim();

        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(AgentPaymentPreparation.Fail(S["The payment has no shared payment token."]));
        }

        return Task.FromResult(new AgentPaymentPreparation
        {
            Succeeded = true,
            ProviderKey = StripeConstants.ProcessorKey,
            ProviderData = new Dictionary<string, string>
            {
                [CheckoutPaymentDataKeys.DelegatedCredential] = token,
                [CheckoutPaymentDataKeys.DelegatedCredentialType] = DelegatedCredentialTypes.StripeSharedPaymentToken,
            },
        });
    }
}
