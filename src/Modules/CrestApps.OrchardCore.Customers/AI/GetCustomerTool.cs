using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Customers.AI;

/// <summary>
/// Returns what a customer's admin page shows: the profile, the address book, recent orders, merge history and privacy
/// log (<see cref="CustomerEditorRules.GetHistoryAsync"/>), and, when those features are on, store credit balances and
/// loyalty points. It needs <see cref="CustomerPermissions.ViewCustomers"/>.
/// </summary>
internal sealed class GetCustomerTool : CustomersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getCustomer";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "customerId": {
          "type": "string",
          "description": "The customer's id, from listCustomers."
        }
      },
      "required": ["customerId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one customer's profile, addresses, recent orders, merge history, privacy log and, when those features are on, store credit balances and loyalty points.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => CustomerPermissions.ViewCustomers;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (customer, error) = await FindCustomerAsync(services, arguments, cancellationToken);

        if (customer is null)
        {
            return error;
        }

        var store = services.GetRequiredService<ICustomerStore>();
        var history = await CustomerEditorRules.GetHistoryAsync(customer, store, services.GetService<IOrderStore>(), cancellationToken);
        var summaries = await services.GetRequiredService<CustomerAddressBook>().SummarizeAsync(customer);

        var orders = new List<object>();

        foreach (var order in history.Orders)
        {
            orders.Add(new
            {
                id = order.ItemId,
                number = order.OrderNumber,
                status = order.Status.ToString(),
                total = order.Total ?? order.Subtotal,
                currency = order.Currency,
                created = await LocalTimeAsync(services, order.CreatedUtc),
            });
        }

        var privacyLog = new List<object>();

        foreach (var entry in customer.PrivacyLog ?? [])
        {
            privacyLog.Add(new
            {
                action = entry.Action.ToString(),
                by = entry.By,
                when = await LocalTimeAsync(services, entry.Utc),
            });
        }

        object storeCredit = null;

        if (services.GetService<IStoreCreditStore>() is { } creditStore)
        {
            var balances = new List<object>();

            foreach (var account in await creditStore.ListByCustomerAsync(customer.ItemId, cancellationToken))
            {
                balances.Add(new
                {
                    currency = account.Currency,
                    balance = account.Balance,
                    lastChanged = await LocalTimeAsync(services, account.Activity?.LastOrDefault()?.CreatedUtc),
                });
            }

            storeCredit = balances;
        }

        object loyalty = null;

        if (services.GetService<ILoyaltyStore>() is { } loyaltyStore && services.GetService<LoyaltyService>() is { } loyaltyService)
        {
            var account = await loyaltyStore.FindByCustomerAsync(customer.ItemId, cancellationToken);
            var settings = await loyaltyService.GetSettingsAsync();
            var currency = (await services.GetRequiredService<ISiteService>().GetSettingsAsync<CheckoutSettings>()).Currency;
            var points = account?.Points ?? 0;

            loyalty = new
            {
                program = settings.ProgramName,
                points,
                worth = LoyaltyService.GetValue(points, settings, currency),
                currency,
                lastChanged = await LocalTimeAsync(services, account?.Activity?.LastOrDefault()?.CreatedUtc),
            };
        }

        return Json(new
        {
            id = customer.ItemId,
            name = customer.GetName(),
            kind = customer.Kind.ToString(),
            status = customer.Status.ToString(),
            hasAccount = !string.IsNullOrEmpty(customer.UserId),
            displayName = customer.DisplayName,
            firstName = customer.FirstName,
            lastName = customer.LastName,
            companyName = customer.CompanyName,
            taxId = customer.TaxId,
            isTaxExempt = customer.IsTaxExempt,
            taxExemptionReason = customer.TaxExemptionReason,
            email = customer.Email,
            phone = customer.Phone,
            acceptsMarketing = customer.AcceptsMarketing,
            created = await LocalTimeAsync(services, customer.CreatedUtc),
            lastActivity = await LocalTimeAsync(services, customer.LastActivityUtc),
            anonymized = await LocalTimeAsync(services, customer.AnonymizedUtc),
            addresses = summaries.Select(summary => new
            {
                addressId = summary.AddressId,
                label = summary.Label,
                lines = summary.Lines,
                address = AddressArguments.Describe(customer.FindAddress(summary.AddressId)?.Address),
                isDefaultBilling = summary.IsDefaultBilling,
                isDefaultShipping = summary.IsDefaultShipping,
            }).ToList(),
            orders = history.HasOrders
                ? new { items = orders, total = history.OrderCount, truncated = history.OrderCount > orders.Count }
                : null,
            mergedInto = history.MergedInto is { } into
                ? new { id = into.ItemId, name = into.GetName(), when = await LocalTimeAsync(services, history.MergedUtc) }
                : null,
            mergedCustomers = history.MergedCustomers.Select(guest => new { id = guest.ItemId, name = guest.GetName() }).ToList(),
            privacyLog,
            storeCredit,
            loyalty,
            pagePath = GetAdminPath(services, "CustomersEdit", new { customerId = customer.ItemId }),
        });
    }
}
