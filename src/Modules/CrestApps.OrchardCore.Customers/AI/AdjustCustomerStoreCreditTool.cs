using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Customers.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Customers.AI;

/// <summary>
/// Gives a customer store credit, or takes it away, as the customer page's Store credit card does: the same checks
/// (<see cref="CustomerBalanceRules.CheckStoreCredit"/>: only a customer with an account, an amount that is not zero, a
/// three-letter currency and a reason), the same change under the checkout's lock
/// (<see cref="StoreCreditService.ChangeAsync"/>), and the same record: the reason and the caller's user name are kept
/// with the credit's activity. The balance never goes below zero, so the change made can be smaller than the one asked
/// for; the result says what changed. It needs <see cref="CustomerPermissions.ManageCustomers"/> and asks for approval.
/// </summary>
internal sealed class AdjustCustomerStoreCreditTool : CustomersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "adjustCustomerStoreCredit";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "customerId": {
          "type": "string",
          "description": "The customer's id, from listCustomers. Only a customer with an account holds store credit."
        },
        "amount": {
          "type": "number",
          "description": "The amount to give, or a negative amount to take away, such as 25 or -10. It cannot be zero."
        },
        "currency": {
          "type": "string",
          "description": "The three-letter currency code, such as USD. The store's currency when left out. Credit pays only toward checkouts in its currency."
        },
        "reason": {
          "type": "string",
          "description": "Why the credit changes, such as 'Late delivery'. It is kept with the credit's activity."
        }
      },
      "required": ["customerId", "amount", "reason"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds or removes store credit for a customer in a currency, with a reason shown in the credit history. A positive amount is money the store gives away. The balance never goes below zero.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => CustomerPermissions.ManageCustomers;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (customer, error) = await FindCustomerAsync(services, arguments, cancellationToken);

        if (customer is null)
        {
            return error;
        }

        var amount = ReadDecimal(arguments, "amount");
        var currency = (ReadString(arguments, "currency") ?? (await services.GetRequiredService<ISiteService>().GetSettingsAsync<CheckoutSettings>()).Currency)?.Trim().ToUpperInvariant();
        var reason = ReadString(arguments, "reason");

        switch (CustomerBalanceRules.CheckStoreCredit(customer, amount, currency, reason))
        {
            case CustomerBalanceProblem.NoAccount:
                return "Nothing was changed: only customers with an account can be given store credit.";
            case CustomerBalanceProblem.NoAmount:
                return "Nothing was changed: give an amount to give, or a negative amount to take away.";
            case CustomerBalanceProblem.InvalidCurrency:
                return "Nothing was changed: give the three-letter currency code, such as USD.";
            case CustomerBalanceProblem.NoReason:
                return "Nothing was changed: say why the credit changes. It's kept with its activity.";
        }

        var account = await services.GetRequiredService<StoreCreditService>().ChangeAsync(customer.ItemId, currency, amount.Value, reason, GetActor(caller), cancellationToken: cancellationToken);

        if (account is null)
        {
            return amount < 0m && await services.GetRequiredService<IStoreCreditStore>().FindAsync(customer.ItemId, currency, cancellationToken) is null
                ? $"Nothing was changed: the customer has no store credit in {currency} to take away."
                : "Nothing was changed: the customer's store credit is being used at a checkout right now. Try again in a moment.";
        }

        var change = account.Activity?.LastOrDefault();
        var changed = change?.Amount ?? 0m;

        return Json(new
        {
            customerId = customer.ItemId,
            customer = customer.GetName(),
            currency = account.Currency,
            asked = amount.Value,
            changed,
            balance = account.Balance,
            reason,
            recordedBy = change?.Actor,
            when = await LocalTimeAsync(services, change?.CreatedUtc),
            message = changed == amount.Value
                ? $"The customer's store credit is now {account.Balance} {account.Currency}."
                : $"The credit never goes below zero, so {Math.Abs(changed)} {account.Currency} was taken away instead of {Math.Abs(amount.Value)}. The customer's store credit is now {account.Balance} {account.Currency}.",
        });
    }
}
