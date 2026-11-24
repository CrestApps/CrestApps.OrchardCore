using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Makes the work orders of an agreement that are due now, as the Run button of its page does.
/// </summary>
internal sealed class RunServiceAgreementTool : WorkOrderToolBase
{
    public const string ToolName = "runServiceAgreement";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "agreement": { "type": "string", "description": "The agreement's number, such as SA-00012, or its ID, from listRecords with the kind agreement." }
      },
      "required": ["agreement"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Makes the work orders of an active agreement that are due now, once each, and returns how many were made. The background task does this hourly; use it to avoid waiting. An agreement past its end date is set to Expired instead, and one near its end raises the Service Agreement Expiring event once. With Dispatch set to create visits for new work orders, each work order gets a visit, which can lead to customer notices.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => AgreementsPermissions.ManageAgreements;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var agreement = await WorkOrderToolSupport.FindAgreementAsync(services, WorkOrderToolSupport.Read(arguments, "agreement"), cancellationToken);

        if (agreement is null)
        {
            return WorkOrderToolSupport.AgreementNotFound;
        }

        if (agreement.Status != AgreementStatus.Active)
        {
            return $"{agreement.Number} is {agreement.Status}: only an active agreement makes work orders. Set it to Active with saveServiceAgreement first.";
        }

        var made = await services.GetRequiredService<ServiceAgreementScheduler>().RunAsync(agreement, cancellationToken);

        if (await WorkOrderToolSupport.SaveAsync(services, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        var workOrders = new List<object>();

        foreach (var workOrder in made)
        {
            workOrders.Add(new
            {
                id = workOrder.ItemId,
                number = workOrder.Number,
                summary = workOrder.Summary,
                earliest = await WorkOrderToolSupport.LocalAsync(services, workOrder.EarliestUtc),
            });
        }

        return WorkOrderToolSupport.Json(new
        {
            agreement = agreement.Number,
            status = agreement.Status.ToString(),
            made = made.Count,
            workOrders,
            timeZoneId = await WorkOrderToolSupport.GetTimeZoneIdAsync(services),
            adminPath = WorkOrderToolSupport.AgreementPath(services, agreement.ItemId),
        });
    }
}
