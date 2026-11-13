using System.Net;
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

            if (string.IsNullOrEmpty(vendor.StripeAccountRequestKey))
            {
                vendor.StripeAccountRequestKey = "vendor-account-" + vendor.ItemId + "-" + global::OrchardCore.IdGenerator.GenerateId();
                await _vendors.UpdateAsync(vendor, cancellationToken);
            }

            Account account;

            try
            {
                account = await new AccountService(_stripeClient).CreateAsync(new AccountCreateOptions
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
                }, new RequestOptions { IdempotencyKey = vendor.StripeAccountRequestKey }, cancellationToken);
            }
            catch (StripeException)
            {
                // Stripe keeps a refused request's answer for its key a day; a new key lets the vendor try again.
                vendor.StripeAccountRequestKey = null;
                await _vendors.UpdateAsync(vendor, cancellationToken);

                throw;
            }

            vendor.StripeAccountId = account.Id;
            vendor.StripeAccountRequestKey = null;
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
    // Ties a transfer to the payout that asked for it, so a pending payout is found at Stripe after its key has expired.
    internal const string PayoutKeyMetadata = "payoutKey";

    private readonly StripeClient _stripeClient;
    private readonly IPaymentAttemptStore _attempts;
    private readonly ILogger _logger;

    public StripeConnectPayoutProvider(StripeClient stripeClient, IPaymentAttemptStore attempts, ILogger<StripeConnectPayoutProvider> logger)
    {
        _stripeClient = stripeClient;
        _attempts = attempts;
        _logger = logger;
    }

    public string Name
        => MarketplaceConstants.StripeConnectPayoutProvider;

    public async Task<VendorPayoutResult> PayAsync(Vendor vendor, Order order, decimal amount, string currency, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(vendor?.StripeAccountId))
        {
            return VendorPayoutResult.Refused("The vendor has no Stripe account.");
        }

        var (chargeId, chargeUnknown) = await GetChargeIdAsync(order, cancellationToken);

        if (chargeUnknown)
        {
            // Sending without the charge would be a different request under the same key.
            return VendorPayoutResult.Unsettled($"The payment of order {order?.OrderNumber} couldn't be read from Stripe, so its payout waits for the next run.");
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
                    [PayoutKeyMetadata] = idempotencyKey,
                },
            };

            // Funded by the order's own charge, it can go before that charge's money is available to the store.
            if (chargeId is not null)
            {
                options.SourceTransaction = chargeId;
            }

            var transfer = await new TransferService(_stripeClient).CreateAsync(options, new RequestOptions { IdempotencyKey = "vendor-payout-" + idempotencyKey }, cancellationToken);

            return VendorPayoutResult.Paid(transfer.Id);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe didn't make the transfer to vendor '{VendorId}'.", vendor.ItemId);

            var message = ex.StripeError?.Message ?? ex.Message;

            return IsRefusal(ex) ? VendorPayoutResult.Refused(message) : VendorPayoutResult.Unsettled(message);
        }
    }

    public async Task<VendorPayoutResult> FindTransferAsync(Vendor vendor, Order order, VendorPayout payout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payout);

        if (string.IsNullOrEmpty(vendor?.StripeAccountId))
        {
            return null;
        }

        var options = new TransferListOptions
        {
            Destination = vendor.StripeAccountId,
            TransferGroup = string.IsNullOrEmpty(order?.OrderNumber) ? null : order.OrderNumber,
            Created = new DateRangeOptions { GreaterThanOrEqual = payout.CreatedUtc.AddDays(-1) },
            Limit = 100,
        };

        try
        {
            await foreach (var transfer in new TransferService(_stripeClient).ListAutoPagingAsync(options, cancellationToken: cancellationToken))
            {
                if (IsTransferOf(transfer, vendor, payout))
                {
                    return VendorPayoutResult.Paid(transfer.Id);
                }
            }

            return null;
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe couldn't be asked whether the payout of order '{OrderNumber}' was made.", payout.OrderNumber);

            return VendorPayoutResult.Unsettled($"Stripe couldn't be asked whether the payout of order {payout.OrderNumber} was made, so it waits for the next run.");
        }
    }

    // A transfer names the payout's key; one made before transfers carried it is matched on what it was for.
    private static bool IsTransferOf(Transfer transfer, Vendor vendor, VendorPayout payout)
    {
        var metadata = transfer.Metadata ?? [];

        if (metadata.TryGetValue(PayoutKeyMetadata, out var key))
        {
            return string.Equals(key, payout.IdempotencyKey, StringComparison.Ordinal);
        }

        return metadata.TryGetValue("vendorId", out var vendorId) && string.Equals(vendorId, vendor.ItemId, StringComparison.Ordinal) &&
            metadata.TryGetValue("orderId", out var orderId) && string.Equals(orderId, payout.OrderId, StringComparison.Ordinal) &&
            transfer.Amount == StripeCurrency.ToMinorUnits(payout.Amount, payout.Currency) &&
            string.Equals(transfer.Currency, payout.Currency, StringComparison.OrdinalIgnoreCase);
    }

    // Only Stripe saying no to the request (its parameters, the account, the balance) proves no transfer was made. A reused
    // key, a conflict, a rate limit, a server error or no answer at all prove nothing.
    private static bool IsRefusal(StripeException exception)
        => !string.Equals(exception.StripeError?.Type, "idempotency_error", StringComparison.Ordinal) &&
            exception.HttpStatusCode is HttpStatusCode.BadRequest or HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden or HttpStatusCode.NotFound;

    // The charge of the order's Stripe payment, from its succeeded payment attempt. Unknown when Stripe couldn't be asked.
    private async Task<(string ChargeId, bool Unknown)> GetChargeIdAsync(Order order, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(order?.CheckoutSessionId))
        {
            return (null, false);
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
            return (null, false);
        }

        try
        {
            var intent = await new PaymentIntentService(_stripeClient).GetAsync(paymentIntentId, cancellationToken: cancellationToken);

            return (intent.LatestChargeId, false);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "The payment '{PaymentIntentId}' of order '{OrderNumber}' couldn't be read.", paymentIntentId, order.OrderNumber);

            return (null, true);
        }
    }
}
