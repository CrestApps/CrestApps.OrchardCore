using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Subscriptions.AI;

/// <summary>
/// Charges one scheduled payment of a plan to the saved card now, through the service the plan's Charge action uses
/// (<see cref="IInstallmentPlanService.ChargeAsync"/>), which refuses a plan that is not active or past due, a payment
/// already paid or canceled, and a plan without a saved card, and commits the plan itself.
/// </summary>
/// <remarks>
/// Stricter than the page, under the owner's money rules: the amount and currency must be given and equal the payment's,
/// as a confirmation of what is charged; and every call carries an idempotency key, kept for a day per site and caller
/// (<see cref="IdempotentOperation"/>), so a repeated call with the same key is refused instead of charging twice. A
/// charge the provider has not confirmed yet is reported as pending, never as received. Every call that ran keeps its
/// key, even one refused or one that failed with an error, since the card may already have been charged.
/// </remarks>
internal sealed class ChargeInstallmentPaymentTool : InstallmentPlanToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "chargeInstallmentPayment";

    private const int MaxKeyLength = 128;

    private static readonly TimeSpan _keyLifetime = TimeSpan.FromHours(24);

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "planId": {
          "type": "string",
          "description": "The plan's id, from listInstallmentPlans."
        },
        "paymentNumber": {
          "type": "integer",
          "description": "The scheduled payment's number, from getInstallmentPlan (1 or more; the down payment is not charged here)."
        },
        "amount": {
          "type": "number",
          "description": "The payment's amount, exactly as getInstallmentPlan shows it, to confirm what is charged."
        },
        "currency": {
          "type": "string",
          "description": "The plan's currency, such as USD."
        },
        "idempotencyKey": {
          "type": "string",
          "description": "A new unique key for this charge, up to 128 characters, such as charge-plan-3-thompson. A second call with the same key is refused, so a retry never charges twice."
        }
      },
      "required": ["planId", "paymentNumber", "amount", "currency", "idempotencyKey"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Charges the customer's saved card for one scheduled payment of a plan now, by payment number, with the payment's amount and currency to confirm it and a new idempotency key. Use it for a due or failed payment; it fails when the plan has no saved card. A charge cannot be undone.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var key = ReadString(arguments, "idempotencyKey");

        if (key is null || key.Length > MaxKeyLength)
        {
            return $"Nothing was charged: give a new idempotencyKey of up to {MaxKeyLength} characters for this charge.";
        }

        if (!TryReadInt(arguments, "paymentNumber", out var number) || number is not > 0)
        {
            return "Nothing was charged: give the paymentNumber of a scheduled payment, 1 or more, from getInstallmentPlan.";
        }

        if (!TryReadDecimal(arguments, "amount", out var amount) || amount is not > 0m)
        {
            return "Nothing was charged: give the payment's amount, more than zero, as getInstallmentPlan shows it.";
        }

        var (plan, error) = await FindPlanAsync(services, ReadString(arguments, "planId"), cancellationToken);

        if (plan is null)
        {
            return error;
        }

        var currency = ReadString(arguments, "currency");

        if (!string.Equals(currency, plan.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return $"Nothing was charged: plan '{plan.Title}' is in {plan.Currency}; give the amount in {plan.Currency}.";
        }

        var payment = plan.Payments.FirstOrDefault(candidate => candidate.Number == number.Value);

        if (payment is null)
        {
            return $"Nothing was charged: plan '{plan.Title}' has no payment {number.Value}. Read its schedule with getInstallmentPlan.";
        }

        if (CurrencyScale.Round(amount.Value, plan.Currency) != amount.Value || amount.Value != payment.Amount)
        {
            return $"Nothing was charged: payment {payment.Number} of plan '{plan.Title}' is {Format(payment.Amount, plan.Currency)}. Give exactly that amount to confirm the charge.";
        }

        if (payment.Status is InstallmentPaymentStatus.Paid or InstallmentPaymentStatus.Canceled)
        {
            return $"Nothing was charged: payment {payment.Number} of plan '{plan.Title}' is {ToCamel(payment.Status.ToString())} and does not need collecting.";
        }

        var cache = services.GetService<IDistributedCache>();
        var locks = services.GetService<IDistributedLock>();

        if (cache is null || locks is null)
        {
            return "Nothing was charged: the site can't keep idempotency keys right now, so a charge from a chat can't be guarded against running twice. Charge it from the plan's page.";
        }

        var scope = $"{services.GetService<ShellSettings>()?.Name}|ai-tool:{TheName}|{GetUserId(caller)}";
        var fingerprint = string.Join('|', plan.ItemId, payment.Number.ToString(CultureInfo.InvariantCulture), amount.Value.ToString(CultureInfo.InvariantCulture), plan.Currency);
        var planId = plan.ItemId;
        var paymentNumber = payment.Number;

        var outcome = await IdempotentOperation.RunAsync(cache, locks, scope, key, fingerprint, _keyLifetime, async token =>
        {
            InstallmentPlanResult result;

            try
            {
                result = await services.GetRequiredService<IInstallmentPlanService>().ChargeAsync(planId, paymentNumber, token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The card may have been charged before the error; the key is used up so a retry can't charge twice.
                return new IdempotentOperationAnswer(
                    $"The charge of payment {paymentNumber} of plan '{plan.Title}' stopped with an error, and whether the card was charged is unknown. Do not retry with this or another key; read the plan with getInstallmentPlan and check its page.",
                    Keep: true);
            }

            var charged = result.Plan?.Payments.FirstOrDefault(candidate => candidate.Number == paymentNumber);
            var state = !result.Succeeded
                ? "notCharged"
                : charged?.Status == InstallmentPaymentStatus.Paid
                    ? "received"
                    : "pending";

            var answer = Json(new
            {
                planId,
                paymentNumber,
                amount = payment.Amount,
                currency = plan.Currency,
                state,
                paymentStatus = charged is null ? null : ToCamel(charged.Status.ToString()),
                errors = result.Errors.Select(entry => entry.Value).ToList(),
                message = state switch
                {
                    "received" => $"Payment {paymentNumber} of plan '{plan.Title}' was charged to the saved card and received: {Format(payment.Amount, plan.Currency)}.",
                    "pending" => $"Payment {paymentNumber} of plan '{plan.Title}' was sent to the saved card and is still being confirmed by the payment provider. It is not received yet; do not charge it again. Read the plan with getInstallmentPlan later.",
                    _ => $"Payment {paymentNumber} of plan '{plan.Title}' was not charged; read the errors.",
                },
            });

            // Every answer is kept, so the key is refused from then on, whatever the charge came to.
            return new IdempotentOperationAnswer(answer, Keep: true);
        }, cancellationToken);

        return outcome.Outcome switch
        {
            IdempotentOperationOutcome.Ran => outcome.Value,
            IdempotentOperationOutcome.Replayed => $"Nothing was charged again: the idempotency key '{key}' was already used for this charge. The first call answered: {outcome.Value}",
            IdempotentOperationOutcome.Conflict => $"Nothing was charged: the idempotency key '{key}' was already used for a different charge. Use a new key for a new charge.",
            _ => $"Nothing was charged: a charge with the idempotency key '{key}' is still running. Read the plan with getInstallmentPlan in your next message.",
        };
    }
}
