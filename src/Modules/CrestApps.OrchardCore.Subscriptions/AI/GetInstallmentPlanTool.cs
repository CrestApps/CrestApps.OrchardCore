using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Subscriptions.AI;

/// <summary>
/// Returns one installment plan as its page shows it: the schedule with each payment's state, the history, the saved
/// card by brand and last four digits, and, while the plan waits for a down payment taken by staff, the link of the
/// page where it is taken by card.
/// </summary>
internal sealed class GetInstallmentPlanTool : InstallmentPlanToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getInstallmentPlan";

    private const int MaxEvents = 30;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "planId": {
          "type": "string",
          "description": "The plan's id, from listInstallmentPlans."
        }
      },
      "required": ["planId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one plan with its schedule (each payment's number, due date, amount, status), its history, and, while it waits for its down payment, the admin link where staff take that payment by card.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (plan, error) = await FindPlanAsync(services, ReadString(arguments, "planId"), cancellationToken);

        if (plan is null)
        {
            return error;
        }

        var result = await SummarizeAsync(services, plan);
        var schedule = new List<object>();

        foreach (var payment in plan.Payments.OrderBy(payment => payment.Number))
        {
            schedule.Add(new
            {
                number = payment.Number,
                isDownPayment = payment.Number == 0,
                due = DueDate(payment.DueUtc),
                amount = payment.Amount,
                status = ToCamel(payment.Status.ToString()),
                paid = await LocalTimeAsync(services, payment.PaidUtc),
                chargeAttempts = payment.ChargeAttempts,
                nextChargeAttempt = await LocalTimeAsync(services, payment.NextChargeAttemptUtc),
                lastFailure = payment.LastFailureMessage,
            });
        }

        var history = new List<object>();

        foreach (var entry in plan.Events.OrderByDescending(entry => entry.CreatedUtc).Take(MaxEvents))
        {
            history.Add(new
            {
                at = await LocalTimeAsync(services, entry.CreatedUtc),
                type = ToCamel(entry.Type.ToString()),
                message = entry.Message,
                paymentNumber = entry.PaymentNumber,
                by = entry.ActorName,
            });
        }

        result["schedule"] = schedule;
        result["savedCard"] = plan.PaymentMethod?.Describe();
        result["hasSavedCard"] = plan.PaymentMethod is not null;
        result["notes"] = plan.Notes;
        result["createdBy"] = plan.CreatedByName;
        result["activated"] = await LocalTimeAsync(services, plan.ActivatedUtc);
        result["completed"] = await LocalTimeAsync(services, plan.CompletedUtc);
        result["canceled"] = await LocalTimeAsync(services, plan.CanceledUtc);
        result["history"] = history;
        result["historyTruncated"] = plan.Events.Count > MaxEvents;
        result["adminPath"] = GetPlanPath(services, plan);
        result["takeDownPaymentPath"] = GetPayPath(services, plan);

        return Json(result);
    }
}
