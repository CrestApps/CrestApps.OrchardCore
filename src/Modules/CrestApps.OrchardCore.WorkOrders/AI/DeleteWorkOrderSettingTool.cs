using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Deletes a work order settings record nothing uses, as the Delete button of the settings pages does.
/// </summary>
internal sealed class DeleteWorkOrderSettingTool : WorkOrderToolBase
{
    public const string ToolName = "deleteWorkOrderSetting";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "kind": { "type": "string", "enum": ["types", "checklists", "service-levels", "sub-statuses"], "description": "The kind of record." },
        "id": { "type": "string", "description": "The record's ID, from listWorkOrderSettings." }
      },
      "required": ["kind", "id"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Deletes a work order type, checklist, service level or sub-status. Refused while work orders use the type, while a type uses the checklist, or while anything uses the service level or timers still count on it. It cannot be undone: a record made again with saveWorkOrderSetting gets a new ID.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkOrdersPermissions.ManageWorkOrderSettings;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var kind = WorkOrderSettingFields.ParseKind(WorkOrderToolSupport.Read(arguments, "kind"));

        if (kind is null)
        {
            return "The kind must be types, checklists, service-levels or sub-statuses.";
        }

        var id = WorkOrderToolSupport.Read(arguments, "id");

        if (kind == WorkOrderSettingKinds.ServiceLevels)
        {
            return await DeletePolicyAsync(services, user, id, cancellationToken);
        }

        var result = await services.GetRequiredService<WorkOrderSettingDeletion>().DeleteAsync(kind, id, cancellationToken);

        if (result is null || result.Outcome == WorkOrderSettingDeleteOutcome.NotFound)
        {
            return $"No record of kind {kind} has the ID '{id}'. Use listWorkOrderSettings to find it.";
        }

        if (result.Outcome == WorkOrderSettingDeleteOutcome.InUse)
        {
            return kind == WorkOrderSettingKinds.Types
                ? $"{result.Name} can't be deleted: work orders use it."
                : $"{result.Name} can't be deleted: work order types use it. Take it off those types first (saveWorkOrderSetting).";
        }

        if (await WorkOrderToolSupport.SaveAsync(services, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return WorkOrderToolSupport.Json(new
        {
            deleted = true,
            kind,
            id,
            name = result.Name,
        });
    }

    // A work order service level is a shared Service Levels policy, deleted with the Service Levels page's permission and
    // rules: not while a record of any module names it or timers still count on it.
    private static async Task<object> DeletePolicyAsync(IServiceProvider services, ClaimsPrincipal user, string id, CancellationToken cancellationToken)
    {
        if (await WorkOrderToolSupport.AuthorizeServiceLevelsAsync(services, user) is { } refused)
        {
            return refused;
        }

        var (result, usages) = await services.GetRequiredService<WorkOrderSettingDeletion>().DeletePolicyAsync(id, cancellationToken);

        if (result.Outcome == WorkOrderSettingDeleteOutcome.NotFound)
        {
            return $"No record of kind {WorkOrderSettingKinds.ServiceLevels} has the ID '{id}'. Use listWorkOrderSettings to find it.";
        }

        if (result.Outcome == WorkOrderSettingDeleteOutcome.InUse)
        {
            return $"{result.Name} can't be deleted while it is in use: {string.Join(", ", usages)}. Take it off those records first.";
        }

        if (await WorkOrderToolSupport.SaveAsync(services, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return WorkOrderToolSupport.Json(new
        {
            deleted = true,
            kind = WorkOrderSettingKinds.ServiceLevels,
            id,
            name = result.Name,
        });
    }
}
