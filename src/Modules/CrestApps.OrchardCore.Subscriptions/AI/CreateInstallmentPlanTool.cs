using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.AI;

/// <summary>
/// Creates a draft installment plan through the service the Create page uses
/// (<see cref="IInstallmentPlanService.CreateAsync"/>), and returns the page where staff take the first payment by card.
/// Nothing is charged: the card is entered in the payment provider's own form on that page, so a tool cannot take it.
/// </summary>
/// <remarks>
/// Narrower than the page: the customer must already have an account (<c>customerUserId</c>); the page can also create
/// a new, passwordless customer account, which a chat does not do. The service's validation runs unchanged and its
/// messages are returned as they are.
/// </remarks>
internal sealed class CreateInstallmentPlanTool : InstallmentPlanToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "createInstallmentPlan";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "title": {
          "type": "string",
          "description": "What the plan is for, such as Season fee 2027."
        },
        "customerUserId": {
          "type": "string",
          "description": "The customer's user id, from searchForUsers. The customer must already have an account."
        },
        "currency": {
          "type": "string",
          "description": "The currency of every amount, such as USD."
        },
        "totalAmount": {
          "type": "number",
          "description": "The whole balance, more than zero."
        },
        "downPaymentAmount": {
          "type": "number",
          "description": "What is paid first, by card on the plan's payment page; zero for none. Less than the total."
        },
        "installmentCount": {
          "type": "integer",
          "description": "How many scheduled payments follow the down payment."
        },
        "frequency": {
          "type": "string",
          "enum": ["weekly", "biWeekly", "monthly", "quarterly"],
          "description": "How often the scheduled payments fall due."
        },
        "firstDueDate": {
          "type": "string",
          "description": "The date the first scheduled payment falls due, as yyyy-MM-dd. After today when there is a down payment; today when there is none."
        },
        "collectionMethod": {
          "type": "string",
          "enum": ["autoCharge", "invoice"],
          "description": "autoCharge charges the saved card on each due date; invoice asks the customer to pay each one. Leave it out for the site's default."
        },
        "notes": {
          "type": "string",
          "description": "Notes kept with the plan."
        }
      },
      "required": ["title", "customerUserId", "currency", "totalAmount", "downPaymentAmount", "installmentCount", "frequency", "firstDueDate"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a draft installment plan for an existing customer (total, down payment, number of payments, frequency, first due date, collection method) and returns the link where staff take the first payment by card. Nothing is charged by this tool; the plan stays a draft until that payment is taken.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var customerUserId = ReadString(arguments, "customerUserId");

        if (customerUserId is null)
        {
            return "Nothing was created: give the customer's customerUserId (use searchForUsers). A plan for someone without an account is created on the Installment Plans page.";
        }

        if (!TryReadDecimal(arguments, "totalAmount", out var total) || total is null)
        {
            return "Nothing was created: give the totalAmount as a number.";
        }

        if (!TryReadDecimal(arguments, "downPaymentAmount", out var downPayment) || downPayment is null)
        {
            return "Nothing was created: give the downPaymentAmount as a number, zero for none.";
        }

        if (!TryReadInt(arguments, "installmentCount", out var count) || count is null)
        {
            return "Nothing was created: give the installmentCount as a whole number.";
        }

        if (!TryReadEnum<InstallmentFrequency>(arguments, "frequency", out var frequency) || frequency is null)
        {
            return "Nothing was created: frequency must be weekly, biWeekly, monthly or quarterly.";
        }

        if (!TryReadEnum<InstallmentCollectionMethod>(arguments, "collectionMethod", out var collectionMethod))
        {
            return "Nothing was created: collectionMethod must be autoCharge or invoice.";
        }

        if (!DateTime.TryParseExact(ReadString(arguments, "firstDueDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var firstDue))
        {
            return "Nothing was created: give the firstDueDate as yyyy-MM-dd.";
        }

        var currency = ReadString(arguments, "currency");

        if (currency is null)
        {
            return "Nothing was created: give the currency of the amounts, such as USD.";
        }

        if (collectionMethod is null && services.GetService<ISiteService>() is { } siteService)
        {
            collectionMethod = (await siteService.GetSettingsAsync<InstallmentPlanSettings>()).DefaultCollectionMethod;
        }

        var result = await services.GetRequiredService<IInstallmentPlanService>().CreateAsync(new CreateInstallmentPlanRequest
        {
            Title = ReadString(arguments, "title"),
            CustomerUserId = customerUserId,
            Currency = currency.ToUpperInvariant(),
            TotalAmount = total.Value,
            DownPaymentAmount = downPayment.Value,
            InstallmentCount = count.Value,
            Frequency = frequency.Value,

            // As the page does: the date entered is kept as that date at midnight UTC.
            FirstDueUtc = DateTime.SpecifyKind(firstDue.Date, DateTimeKind.Utc),
            CollectionMethod = collectionMethod ?? InstallmentCollectionMethod.AutoCharge,
            Notes = ReadString(arguments, "notes"),
        }, cancellationToken);

        if (!result.Succeeded)
        {
            return Json(new
            {
                saved = false,
                message = "Nothing was created. Fix what the errors say and try again.",
                errors = result.Errors.Select(error => new
                {
                    field = string.IsNullOrEmpty(error.Key) ? null : ToCamel(error.Key),
                    message = error.Value,
                }).ToList(),
            });
        }

        if (await CommitAsync(services, cancellationToken) is { } saveError)
        {
            return saveError;
        }

        var plan = result.Plan;
        var summary = await SummarizeAsync(services, plan);
        var payPath = GetPayPath(services, plan);

        summary["saved"] = true;
        summary["charged"] = false;
        summary["adminPath"] = GetPlanPath(services, plan);
        summary["takeDownPaymentPath"] = payPath;
        summary["message"] = $"Created the draft plan '{plan.Title}' for {plan.CustomerName}. Nothing was charged: the plan stays a draft until staff take the first payment by card on the plan's payment page{(payPath is null ? string.Empty : $" ({payPath})")}.";

        return Json(summary);
    }
}
