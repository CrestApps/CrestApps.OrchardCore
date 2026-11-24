using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Marketplace.AI;

/// <summary>
/// Returns one vendor as its page under Commerce › Vendors shows it: with Stripe Connect on, the payout account's state
/// is read from Stripe first (<see cref="StripeConnectVendorService.RefreshAsync"/>), then the totals per currency
/// (<see cref="MarketplaceService.GetTotals"/>) over the same 500 latest earnings, the latest earnings per order and the
/// latest 100 payouts with their failure messages. The payout account's id is never returned.
/// </summary>
internal sealed class GetVendorTool : MarketplaceAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getVendor";

    private const int EarningsRead = 500;
    private const int PayoutsRead = 100;
    private const int Shown = 20;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "vendorId": {
          "type": "string",
          "description": "The vendor's id, from listRecords with kind vendor."
        }
      },
      "required": ["vendorId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one vendor with its totals per currency (held, available, paid out), its recent earnings per order and its payouts with any failure message. Use it to answer what the store owes a vendor.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => MarketplacePermissions.ManageMarketplace;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "vendorId") is not { } vendorId)
        {
            return "Give the vendor's id. Use listRecords with kind vendor to find it.";
        }

        var vendor = await services.GetRequiredService<IVendorStore>().FindByIdAsync(vendorId, cancellationToken);

        if (vendor is null)
        {
            return $"No vendor has the id '{vendorId}'. Use listRecords with kind vendor to find it.";
        }

        // As the vendor's page: the payout account's state is read from Stripe before it is shown.
        if (services.GetService<StripeConnectVendorService>() is { } stripe)
        {
            await stripe.RefreshAsync(vendor, cancellationToken);

            if (await CommitAsync(services, cancellationToken) is { } error)
            {
                return error;
            }
        }

        var earnings = await services.GetRequiredService<IVendorEarningStore>().ListByVendorAsync(vendor.ItemId, EarningsRead, cancellationToken);
        var payouts = await services.GetRequiredService<IVendorPayoutStore>().ListByVendorAsync(vendor.ItemId, PayoutsRead, cancellationToken);

        var earningItems = new List<object>();

        foreach (var earning in earnings.OrderByDescending(earning => earning.CreatedUtc).Take(Shown))
        {
            earningItems.Add(new
            {
                id = earning.ItemId,
                orderId = earning.OrderId,
                orderNumber = earning.OrderNumber,
                title = earning.Title,
                kind = ToCamel(earning.Kind.ToString()),
                status = ToCamel(earning.Status.ToString()),
                currency = earning.Currency,
                gross = earning.Gross,
                commissionPercent = earning.CommissionPercent,
                commission = earning.Commission,
                net = earning.Net,
                holdUntil = await LocalTimeAsync(services, earning.HoldUntilUtc),
                awaitsFulfillment = earning.AwaitsFulfillment,
                releasedAt = await LocalTimeAsync(services, earning.ReleasedUtc),
                paidOut = !string.IsNullOrEmpty(earning.PayoutId),
                createdAt = await LocalTimeAsync(services, earning.CreatedUtc),
            });
        }

        var payoutItems = new List<object>();

        foreach (var payout in payouts.OrderByDescending(payout => payout.CreatedUtc).Take(Shown))
        {
            payoutItems.Add(new
            {
                id = payout.ItemId,
                orderId = payout.OrderId,
                orderNumber = payout.OrderNumber,
                amount = payout.Amount,
                currency = payout.Currency,
                provider = payout.Provider,
                status = ToCamel(payout.Status.ToString()),
                failure = payout.Failure,
                createdAt = await LocalTimeAsync(services, payout.CreatedUtc),
            });
        }

        return Json(new
        {
            vendor = Describe(vendor),
            totals = MarketplaceService.GetTotals(earnings).Select(total => new
            {
                currency = total.Currency,
                held = total.Held,
                available = total.Available,
                paidOut = total.PaidOut,
            }),
            totalsCoverLatestEarningsOnly = earnings.Count >= EarningsRead,
            earnings = earningItems,
            earningsShown = earningItems.Count,
            earningsRead = earnings.Count,
            payouts = payoutItems,
            payoutsShown = payoutItems.Count,
            payoutsRead = payouts.Count,
            adminPath = GetAdminPath(services, "VendorsDetail", new { area = MarketplaceConstants.Feature, itemId = vendor.ItemId }),
        });
    }

    /// <summary>
    /// Describes a vendor from an explicit list of fields: never its payout account's id or its account request key.
    /// </summary>
    /// <param name="vendor">The vendor.</param>
    /// <returns>The vendor's fields.</returns>
    internal static object Describe(Vendor vendor)
        => new
        {
            id = vendor.ItemId,
            name = vendor.Name,
            email = vendor.Email,
            status = ToCamel(vendor.Status.ToString()),
            commissionPercent = vendor.CommissionPercent,
            usesStoreCommission = vendor.CommissionPercent is null,
            country = vendor.Country,
            userNames = vendor.UserNames ?? [],
            hasPayoutAccount = !string.IsNullOrEmpty(vendor.StripeAccountId),
            payoutsEnabled = vendor.PayoutsEnabled,
        };
}
