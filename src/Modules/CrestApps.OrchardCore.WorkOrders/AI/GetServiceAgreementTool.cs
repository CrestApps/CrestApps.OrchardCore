using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Agreements;
using Microsoft.Extensions.AI;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Gets one service agreement as its page shows it: the agreement, its schedules with their next visit days, and the
/// work orders it made.
/// </summary>
internal sealed class GetServiceAgreementTool : WorkOrderToolBase
{
    public const string ToolName = "getServiceAgreement";

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

    public override string Description => "Gets one agreement's field-service terms: customer, contact, place, assets, period, the hours it covers, pricing, each schedule with its repeat rule and next three visit days, and the latest 50 work orders it made. listRecords with the kind agreement lists agreements; getAgreementBalance gives the hours used and left.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => AgreementsPermissions.ViewAgreements;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var agreement = await WorkOrderToolSupport.FindAgreementAsync(services, WorkOrderToolSupport.Read(arguments, "agreement"), cancellationToken);

        if (agreement is null)
        {
            return WorkOrderToolSupport.AgreementNotFound;
        }

        return WorkOrderToolSupport.Json(await ServiceAgreementToolResults.DetailAsync(services, agreement, cancellationToken));
    }
}
