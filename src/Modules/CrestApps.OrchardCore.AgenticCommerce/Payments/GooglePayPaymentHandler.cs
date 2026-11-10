using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce.Stores;
using CrestApps.OrchardCore.Stripe.Core;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Payments;

/// <summary>
/// UCP agents pay with Google Pay (<c>com.google.pay</c>). The token's gateway is Stripe, which charges it.
/// </summary>
public sealed class GooglePayPaymentHandler : IAgentPaymentHandler
{
    /// <summary>
    /// The handler's name in UCP.
    /// </summary>
    public const string UcpName = "com.google.pay";

    private readonly IOptionsMonitor<StripeOptions> _stripeOptions;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public GooglePayPaymentHandler(
        IOptionsMonitor<StripeOptions> stripeOptions,
        ISiteService siteService,
        IStringLocalizer<GooglePayPaymentHandler> stringLocalizer)
    {
        _stripeOptions = stripeOptions;
        _siteService = siteService;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string GetName(string protocol)
        => UcpName;

    /// <inheritdoc />
    public async Task<AgentPaymentHandlerDescriptor> DescribeAsync(AgentPaymentHandlerContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var stripe = _stripeOptions.CurrentValue;

        if (context.TestMode || context.Protocol != AgenticCommerceConstants.Protocols.Ucp || !stripe.IsConfigured || string.IsNullOrEmpty(stripe.PublishableKey))
        {
            return null;
        }

        var settings = await _siteService.GetSettingsAsync<AgenticCommerceSettings>();

        if (!settings.EnableGooglePay)
        {
            return null;
        }

        var profile = await _siteService.GetStoreProfileAsync();
        var site = await _siteService.GetSiteSettingsAsync();
        var merchantInfo = new JsonObject
        {
            ["merchant_name"] = settings.GooglePayMerchantName ?? profile.Name ?? site.SiteName,
        };

        if (!string.IsNullOrEmpty(settings.GooglePayMerchantId))
        {
            merchantInfo["merchant_id"] = settings.GooglePayMerchantId;
        }

        return new AgentPaymentHandlerDescriptor
        {
            Id = "google_pay",
            Name = UcpName,
            Version = "2026-01-23",
            Spec = "https://developers.google.com/pay/api/universal-commerce-protocol/google-pay-payment-handler",
            Schema = "https://pay.google.com/gp/p/ucp/2026-01-11/schemas/config.json",
            Psp = "stripe",
            DisplayName = S["Google Pay"],
            InstrumentTypes = ["card"],
            Config = new JsonObject
            {
                ["api_version"] = 2,
                ["api_version_minor"] = 0,
                ["environment"] = settings.GooglePayTestEnvironment || !stripe.IsLive ? "TEST" : "PRODUCTION",
                ["merchant_info"] = merchantInfo,
                ["allowed_payment_methods"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "CARD",
                        ["parameters"] = new JsonObject
                        {
                            ["allowed_auth_methods"] = new JsonArray { "PAN_ONLY", "CRYPTOGRAM_3DS" },
                            ["allowed_card_networks"] = new JsonArray { "AMEX", "DISCOVER", "MASTERCARD", "VISA" },
                        },
                        ["tokenization_specification"] = new JsonObject
                        {
                            ["type"] = "PAYMENT_GATEWAY",
                            ["parameters"] = new JsonObject
                            {
                                ["gateway"] = "stripe",
                                ["stripe:version"] = "2018-10-31",
                                ["stripe:publishableKey"] = stripe.PublishableKey,
                            },
                        },
                    },
                },
            },
        };
    }

    /// <inheritdoc />
    public Task<AgentPaymentPreparation> PrepareAsync(AgentPaymentHandlerContext context, AgentPayment payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (string.IsNullOrWhiteSpace(payment.Credential))
        {
            return Task.FromResult(AgentPaymentPreparation.Fail(S["The payment has no Google Pay token."]));
        }

        return Task.FromResult(new AgentPaymentPreparation
        {
            Succeeded = true,
            ProviderKey = StripeConstants.ProcessorKey,
            ProviderData = new Dictionary<string, string>
            {
                [CheckoutPaymentDataKeys.DelegatedCredential] = payment.Credential.Trim(),
                [CheckoutPaymentDataKeys.DelegatedCredentialType] = DelegatedCredentialTypes.GooglePayToken,
            },
        });
    }
}
