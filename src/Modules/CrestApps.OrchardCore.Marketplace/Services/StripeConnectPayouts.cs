using System.Net;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
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
/// Sends vendor payouts as Stripe Connect transfers to the vendors' own accounts, each from its order's payment, as a
/// Disbursements provider. A transfer goes through (the disbursement is settled), is refused (failed with Stripe's
/// reason), or has an outcome Stripe didn't say (submitted, and looked up at Stripe before the payout is given up on);
/// the disbursement's key is the transfer's idempotency key, and is written on the transfer so it is found again.
/// </summary>
internal sealed class StripeConnectPayoutProvider : IVendorPayoutProvider
{
    // Ties a transfer to the payout that asked for it, so it is found at Stripe after its idempotency key has expired.
    internal const string PayoutKeyMetadata = "payoutKey";

    private const string DisbursementIdMetadata = "disbursementId";

    private readonly StripeClient _stripeClient;
    private readonly IPaymentAttemptStore _attempts;
    private readonly IVendorStore _vendors;
    private readonly IOrderStore _orders;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public StripeConnectPayoutProvider(
        StripeClient stripeClient,
        IPaymentAttemptStore attempts,
        IVendorStore vendors,
        IOrderStore orders,
        ILogger<StripeConnectPayoutProvider> logger,
        IStringLocalizer<StripeConnectPayoutProvider> stringLocalizer)
    {
        _stripeClient = stripeClient;
        _attempts = attempts;
        _vendors = vendors;
        _orders = orders;
        _logger = logger;
        S = stringLocalizer;
    }

    public string Name
        => MarketplaceConstants.StripeConnectPayoutProvider;

    public LocalizedString DisplayName
        => S["Stripe Connect transfer"];

    public string Method
        => MarketplaceConstants.StripeConnectPayoutProvider;

    public async Task<DisbursementSubmitResult> SubmitAsync(DisbursementBatch batch, IReadOnlyList<Disbursement> disbursements, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(disbursements);

        var transfers = new List<(Disbursement Disbursement, Vendor Vendor, Order Order, string ChargeId)>();

        // Every order's charge is read before anything is transferred. Sending without a charge that couldn't be read
        // would be another request under the same key (Stripe refuses it, or makes a second transfer once it forgot the
        // key), so the whole batch is answered as not sent: it stays open and is sent again by the next payout run.
        foreach (var disbursement in disbursements)
        {
            var (vendor, order) = await GetPartiesAsync(disbursement, cancellationToken);
            var (chargeId, chargeUnknown) = await GetChargeIdAsync(order, cancellationToken);

            if (chargeUnknown)
            {
                return DisbursementSubmitResult.Failed(S["The payment of order {0} couldn't be read from Stripe, so its payout waits for the next run.", order?.OrderNumber].Value);
            }

            transfers.Add((disbursement, vendor, order, chargeId));
        }

        var result = new DisbursementSubmitResult { Succeeded = true };

        foreach (var (disbursement, vendor, order, chargeId) in transfers)
        {
            var answer = await TransferAsync(vendor, order, chargeId, disbursement, cancellationToken);

            if (answer.TransferId is not null)
            {
                result.Statuses[disbursement.ItemId] = DisbursementStatus.Settled;
                result.TraceNumbers[disbursement.ItemId] = answer.TransferId;
            }
            else if (answer.RetryLater)
            {
                // Whether Stripe made the transfer isn't known: submitted, so it is never sent again on its own, and the
                // payout looks it up at Stripe before giving up on it.
                result.Statuses[disbursement.ItemId] = DisbursementStatus.Submitted;
            }
            else
            {
                result.Statuses[disbursement.ItemId] = DisbursementStatus.Failed;
                result.FailureReasons[disbursement.ItemId] = answer.Failure;
            }
        }

        return result;
    }

    public async Task<VendorPayoutResult> FindTransferAsync(Disbursement disbursement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(disbursement);

        var (vendor, order) = await GetPartiesAsync(disbursement, cancellationToken);

        return await FindTransferAsync(vendor, order, disbursement, cancellationToken);
    }

    private async Task<(Vendor Vendor, Order Order)> GetPartiesAsync(Disbursement disbursement, CancellationToken cancellationToken)
    {
        var vendor = disbursement.Payee.Kind == MarketplaceConstants.VendorPartyKind ? await _vendors.FindByIdAsync(disbursement.Payee.Id, cancellationToken) : null;
        var order = disbursement.SourceKind == MarketplaceConstants.PayoutSourceKind && !string.IsNullOrEmpty(disbursement.SourceId)
            ? await _orders.FindByIdAsync(disbursement.SourceId, cancellationToken)
            : null;

        return (vendor, order);
    }

    private async Task<VendorPayoutResult> TransferAsync(Vendor vendor, Order order, string chargeId, Disbursement disbursement, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(vendor?.StripeAccountId))
        {
            return VendorPayoutResult.Refused(S["The vendor has no Stripe account."].Value);
        }

        // A disbursement sent again (its batch was checked and reopened) may have been transferred already, and Stripe
        // keeps a key for 24 hours only: what Stripe has is asked first, and nothing is sent when it can't be asked.
        if (await FindTransferAsync(vendor, order, disbursement, cancellationToken) is { } existing)
        {
            return existing;
        }

        try
        {
            var options = new TransferCreateOptions
            {
                Amount = StripeCurrency.ToMinorUnits(disbursement.Amount, disbursement.Currency),
                Currency = disbursement.Currency.ToLowerInvariant(),
                Destination = vendor.StripeAccountId,
                TransferGroup = order?.OrderNumber,
                Description = order is null ? null : $"Order {order.OrderNumber}",
                Metadata = new Dictionary<string, string>
                {
                    ["vendorId"] = vendor.ItemId,
                    ["orderId"] = order?.ItemId ?? string.Empty,
                    [DisbursementIdMetadata] = disbursement.ItemId,
                    [PayoutKeyMetadata] = disbursement.IdempotencyKey,
                },
            };

            // Funded by the order's own charge, it can go before that charge's money is available to the store.
            if (chargeId is not null)
            {
                options.SourceTransaction = chargeId;
            }

            var transfer = await new TransferService(_stripeClient).CreateAsync(options, new RequestOptions { IdempotencyKey = "vendor-payout-" + disbursement.IdempotencyKey }, cancellationToken);

            return VendorPayoutResult.Paid(transfer.Id);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe didn't make the transfer to vendor '{VendorId}'.", vendor.ItemId);

            var message = ex.StripeError?.Message ?? ex.Message;

            return IsRefusal(ex) ? VendorPayoutResult.Refused(message) : VendorPayoutResult.Unsettled(message);
        }
    }

    // The transfer an earlier request of the disbursement made, Unsettled when Stripe can't be asked, or null when none.
    private async Task<VendorPayoutResult> FindTransferAsync(Vendor vendor, Order order, Disbursement disbursement, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(vendor?.StripeAccountId))
        {
            return null;
        }

        // Not filtered by transfer group: a transfer funded by the order's charge (source_transaction) takes the charge's
        // group, which Stripe names after the PaymentIntent, not the order number it asked for. Missing it here would
        // answer "no transfer" and pay the vendor again; the transfer is matched on its metadata instead.
        var options = new TransferListOptions
        {
            Destination = vendor.StripeAccountId,
            Created = new DateRangeOptions { GreaterThanOrEqual = disbursement.CreatedUtc.AddDays(-1) },
            Limit = 100,
        };

        try
        {
            await foreach (var transfer in new TransferService(_stripeClient).ListAutoPagingAsync(options, cancellationToken: cancellationToken))
            {
                if (IsTransferOf(transfer, vendor, disbursement))
                {
                    return VendorPayoutResult.Paid(transfer.Id);
                }
            }

            return null;
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe couldn't be asked whether the payout of order '{OrderNumber}' was made.", order?.OrderNumber);

            return VendorPayoutResult.Unsettled(S["Stripe couldn't be asked whether the payout of order {0} was made, so it waits for the next run.", order?.OrderNumber].Value);
        }
    }

    // A transfer names the payout's key, or its disbursement; one made before transfers carried either is matched on what
    // it was for.
    private static bool IsTransferOf(Transfer transfer, Vendor vendor, Disbursement disbursement)
    {
        var metadata = transfer.Metadata ?? [];

        if (metadata.TryGetValue(PayoutKeyMetadata, out var key))
        {
            return string.Equals(key, disbursement.IdempotencyKey, StringComparison.Ordinal);
        }

        if (metadata.TryGetValue(DisbursementIdMetadata, out var disbursementId))
        {
            return string.Equals(disbursementId, disbursement.ItemId, StringComparison.Ordinal);
        }

        return metadata.TryGetValue("vendorId", out var vendorId) && string.Equals(vendorId, vendor.ItemId, StringComparison.Ordinal) &&
            metadata.TryGetValue("orderId", out var orderId) && string.Equals(orderId, disbursement.SourceId, StringComparison.Ordinal) &&
            transfer.Amount == StripeCurrency.ToMinorUnits(disbursement.Amount, disbursement.Currency) &&
            string.Equals(transfer.Currency, disbursement.Currency, StringComparison.OrdinalIgnoreCase);
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
