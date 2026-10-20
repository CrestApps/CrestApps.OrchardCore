using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;
using Stripe;

namespace CrestApps.OrchardCore.Marketplace.Services;

/// <summary>
/// Vendors' Stripe Connect accounts: an Express account made for a vendor, Stripe's own onboarding to finish it, and
/// whether it can take transfers yet.
/// </summary>
public sealed class StripeConnectVendorService
{
    private readonly StripeClient _stripeClient;
    private readonly IVendorStore _vendors;
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;

    public StripeConnectVendorService(StripeClient stripeClient, IVendorStore vendors, ISiteService siteService, ILogger<StripeConnectVendorService> logger)
    {
        _stripeClient = stripeClient;
        _vendors = vendors;
        _siteService = siteService;
        _logger = logger;
    }

    /// <summary>
    /// Returns the address of Stripe's onboarding for the vendor, making its account first when it has none.
    /// </summary>
    public async Task<string> GetOnboardingUrlAsync(Vendor vendor, string returnUrl, string refreshUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vendor);

        if (string.IsNullOrEmpty(vendor.StripeAccountId))
        {
            var settings = await _siteService.GetSettingsAsync<MarketplaceSettings>();
            var account = await new AccountService(_stripeClient).CreateAsync(new AccountCreateOptions
            {
                Type = "express",
                Country = string.IsNullOrEmpty(vendor.Country) ? settings.DefaultCountry : vendor.Country,
                Email = vendor.Email,
                Capabilities = new AccountCapabilitiesOptions
                {
                    Transfers = new AccountCapabilitiesTransfersOptions { Requested = true },
                },
                BusinessProfile = new AccountBusinessProfileOptions { Name = vendor.Name },
                Metadata = new Dictionary<string, string> { ["vendorId"] = vendor.ItemId },
            }, new RequestOptions { IdempotencyKey = "vendor-account-" + vendor.ItemId }, cancellationToken);

            vendor.StripeAccountId = account.Id;
            await _vendors.UpdateAsync(vendor, cancellationToken);
        }

        var link = await new AccountLinkService(_stripeClient).CreateAsync(new AccountLinkCreateOptions
        {
            Account = vendor.StripeAccountId,
            ReturnUrl = returnUrl,
            RefreshUrl = refreshUrl,
            Type = "account_onboarding",
        }, cancellationToken: cancellationToken);

        return link.Url;
    }

    /// <summary>
    /// Reads whether the vendor's account can take transfers now, and keeps it on the vendor.
    /// </summary>
    public async Task<bool> RefreshAsync(Vendor vendor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vendor);

        if (string.IsNullOrEmpty(vendor.StripeAccountId))
        {
            return false;
        }

        try
        {
            var account = await new AccountService(_stripeClient).GetAsync(vendor.StripeAccountId, cancellationToken: cancellationToken);
            var enabled = account.DetailsSubmitted && string.Equals(account.Capabilities?.Transfers, "active", StringComparison.OrdinalIgnoreCase);

            if (vendor.PayoutsEnabled != enabled)
            {
                vendor.PayoutsEnabled = enabled;
                await _vendors.UpdateAsync(vendor, cancellationToken);
            }

            return enabled;
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "The Stripe account of vendor '{VendorId}' couldn't be read.", vendor.ItemId);

            return vendor.PayoutsEnabled;
        }
    }
}

/// <summary>
/// Pays a vendor's earnings on an order to its Stripe account: a transfer funded by that order's charge, so it doesn't
/// wait for the store's own balance.
/// </summary>
internal sealed class StripeConnectPayoutProvider : IVendorPayoutProvider
{
    private readonly StripeClient _stripeClient;
    private readonly IPaymentAttemptStore _attempts;
    private readonly ILogger _logger;

    public StripeConnectPayoutProvider(StripeClient stripeClient, IPaymentAttemptStore attempts, ILogger<StripeConnectPayoutProvider> logger)
    {
        _stripeClient = stripeClient;
        _attempts = attempts;
        _logger = logger;
    }

    public async Task<(string TransferId, string Failure)> PayAsync(Vendor vendor, Order order, decimal amount, string currency, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(vendor?.StripeAccountId))
        {
            return (null, "The vendor has no Stripe account.");
        }

        try
        {
            var options = new TransferCreateOptions
            {
                Amount = StripeCurrency.ToMinorUnits(amount, currency),
                Currency = currency.ToLowerInvariant(),
                Destination = vendor.StripeAccountId,
                TransferGroup = order?.OrderNumber,
                Description = order is null ? null : $"Order {order.OrderNumber}",
                Metadata = new Dictionary<string, string>
                {
                    ["vendorId"] = vendor.ItemId,
                    ["orderId"] = order?.ItemId ?? string.Empty,
                },
            };

            // Funded by the order's own charge, it can go before that charge's money is available to the store.
            if (await GetChargeIdAsync(order, cancellationToken) is { } chargeId)
            {
                options.SourceTransaction = chargeId;
            }

            var transfer = await new TransferService(_stripeClient).CreateAsync(options, new RequestOptions { IdempotencyKey = "vendor-payout-" + idempotencyKey }, cancellationToken);

            return (transfer.Id, null);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe refused the transfer to vendor '{VendorId}'.", vendor.ItemId);

            return (null, ex.StripeError?.Message ?? ex.Message);
        }
    }

    // The charge of the order's Stripe payment, from its succeeded payment attempt.
    private async Task<string> GetChargeIdAsync(Order order, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(order?.CheckoutSessionId))
        {
            return null;
        }

        var paymentIntentId = (await _attempts.GetBySessionAsync(order.CheckoutSessionId, cancellationToken))
            .Where(attempt => attempt.State == PaymentAttemptState.Succeeded &&
                !attempt.IsSuperseded &&
                string.Equals(attempt.ProviderKey, StripeConstants.ProcessorKey, StringComparison.OrdinalIgnoreCase) &&
                attempt.ProviderReference?.StartsWith("pi_", StringComparison.Ordinal) == true)
            .Select(attempt => attempt.ProviderReference)
            .FirstOrDefault();

        if (paymentIntentId is null)
        {
            return null;
        }

        try
        {
            var intent = await new PaymentIntentService(_stripeClient).GetAsync(paymentIntentId, cancellationToken: cancellationToken);

            return intent.LatestChargeId;
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "The payment '{PaymentIntentId}' of order '{OrderNumber}' couldn't be read.", paymentIntentId, order.OrderNumber);

            return null;
        }
    }
}
