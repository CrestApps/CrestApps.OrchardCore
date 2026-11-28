using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Agreements.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Deletes a draft service agreement, as the Delete button of the Service Agreements page does.
/// </summary>
internal sealed class DeleteServiceAgreementTool : WorkOrderToolBase
{
    public const string ToolName = "deleteServiceAgreement";

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

    public override string Description => "Deletes a service agreement whose status is Draft. An agreement in any other status cannot be deleted; cancel it with saveServiceAgreement instead. It cannot be undone; the work orders it made are kept.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => AgreementsPermissions.ManageAgreements;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var agreement = await WorkOrderToolSupport.FindAgreementAsync(services, WorkOrderToolSupport.Read(arguments, "agreement"), cancellationToken);

        if (agreement is null)
        {
            return WorkOrderToolSupport.AgreementNotFound;
        }

        if (!AgreementEditorRules.CanDelete(agreement))
        {
            return $"Only a draft agreement can be deleted, and {agreement.Number} is {agreement.Status}. Cancel it instead (saveServiceAgreement with status Canceled).";
        }

        await services.GetRequiredService<IAgreementManager>().DeleteAsync(agreement, cancellationToken);

        if (await WorkOrderToolSupport.SaveAsync(services, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return WorkOrderToolSupport.Json(new
        {
            deleted = true,
            id = agreement.ItemId,
            number = agreement.Number,
            name = agreement.Name,
        });
    }
}
