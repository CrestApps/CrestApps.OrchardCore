using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Subscriptions.AI;

/// <summary>
/// Cancels an installment plan through the service the plan's Cancel action uses
/// (<see cref="IInstallmentPlanService.CancelAsync"/>): payments not yet received are canceled with their ledger
/// transactions, a draft's down payment checkout is closed, and a customer whose plan had started is emailed. Nothing
/// already received is refunded.
/// </summary>
internal sealed class CancelInstallmentPlanTool : InstallmentPlanToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "cancelInstallmentPlan";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "planId": {
          "type": "string",
          "description": "The plan's id, from listInstallmentPlans."
        },
        "reason": {
          "type": "string",
          "description": "Why the plan is canceled, kept in its history and on the canceled transactions."
        }
      },
      "required": ["planId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Cancels a plan so payments not yet received are no longer collected. Cannot be undone; payments already received are not refunded. A customer whose plan had started is emailed.";

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

        if (plan.Status is InstallmentPlanStatus.Completed or InstallmentPlanStatus.Canceled)
        {
            return $"Nothing was canceled: plan '{plan.Title}' is already {ToCamel(plan.Status.ToString())}.";
        }

        var result = await services.GetRequiredService<IInstallmentPlanService>().CancelAsync(plan.ItemId, ReadString(arguments, "reason"), cancellationToken);

        if (!result.Succeeded)
        {
            return $"Nothing was canceled: {string.Join(" ", result.Errors.Select(entry => entry.Value))}";
        }

        var canceled = result.Plan ?? plan;
        var summary = await SummarizeAsync(services, canceled);

        summary["refunded"] = false;
        summary["adminPath"] = GetPlanPath(services, canceled);
        summary["message"] = $"Plan '{canceled.Title}' was canceled: payments not yet received will not be collected. {Format(canceled.AmountPaid, canceled.Currency)} already received stays received and was not refunded.";

        return Json(summary);
    }
}
