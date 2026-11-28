using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Gets one work order settings record in full, with the JSON schema of the fields <c>saveWorkOrderSetting</c> takes for
/// its kind; without an ID, the fields a new record of the kind starts with.
/// </summary>
internal sealed class GetWorkOrderSettingTool : WorkOrderToolBase
{
    public const string ToolName = "getWorkOrderSetting";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "kind": { "type": "string", "enum": ["types", "checklists", "service-levels", "sub-statuses"], "description": "The kind of record." },
        "id": { "type": "string", "description": "The record's ID, from listWorkOrderSettings. Leave it out to see the fields a new record starts with." }
      },
      "required": ["kind"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gets one work order type, checklist, service level or sub-status in full, with the JSON schema of that kind's fields. Use it before saveWorkOrderSetting.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkOrdersPermissions.ManageWorkOrderSettings;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var kind = WorkOrderSettingFields.ParseKind(WorkOrderToolSupport.Read(arguments, "kind"));

        if (kind is null)
        {
            return "The kind must be types, checklists, service-levels or sub-statuses.";
        }

        if (kind == WorkOrderSettingKinds.ServiceLevels && await WorkOrderToolSupport.AuthorizeServiceLevelsAsync(services, user) is { } refused)
        {
            return refused;
        }

        var id = WorkOrderToolSupport.Read(arguments, "id");
        var record = kind switch
        {
            WorkOrderSettingKinds.Types => await RecordAsync<WorkOrderType>(services, id, WorkOrderSettingFields.Record, cancellationToken),
            WorkOrderSettingKinds.Checklists => await RecordAsync<ChecklistTemplate>(services, id, WorkOrderSettingFields.Record, cancellationToken),
            WorkOrderSettingKinds.ServiceLevels => await PolicyRecordAsync(services, id, cancellationToken),
            _ => await RecordAsync<WorkOrderSubStatus>(services, id, WorkOrderSettingFields.Record, cancellationToken),
        };

        if (record is null)
        {
            return $"No record of kind {kind} has the ID '{id}'. Use listWorkOrderSettings to find it.";
        }

        return WorkOrderToolSupport.Json(new
        {
            kind,
            isNew = id is null,
            record,
            schema = WorkOrderSettingFields.SchemaOf(kind),
            adminPath = id is null ? null : WorkOrderToolSupport.SettingAdminPath(services, kind, id),
        });
    }

    // A work order service level is a shared Service Levels policy of the Work orders scope, read with the Service Levels
    // page's permission. A new one is made, never saved, with the defaults its editor starts with.
    private static async Task<object> PolicyRecordAsync(IServiceProvider services, string id, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<IServiceLevelPolicyManager>();

        if (id is null)
        {
            var policy = await manager.NewAsync(cancellationToken: cancellationToken);

            policy.Scope = WorkOrdersConstants.ServiceLevelScope;
            WorkOrderSettingFields.ApplyPolicy(new AIFunctionArguments(), policy, isNew: true);

            return WorkOrderSettingFields.Record(policy);
        }

        var existing = await manager.FindByIdAsync(id, cancellationToken);

        return WorkOrderSettingFields.IsWorkOrderPolicy(existing) ? WorkOrderSettingFields.Record(existing) : null;
    }

    private static async Task<object> RecordAsync<T>(IServiceProvider services, string id, Func<T, object> fields, CancellationToken cancellationToken)
        where T : CrestApps.Core.Models.CatalogItem, new()
    {
        var manager = services.GetRequiredService<WorkOrderSettingManager<T>>();

        // A new record is made, never saved, so its defaults are those the editor starts with.
        var item = id is null ? await manager.NewAsync(cancellationToken: cancellationToken) : await manager.FindByIdAsync(id, cancellationToken);

        if (item is null)
        {
            return null;
        }

        if (id is null && item is WorkOrderSubStatus subStatus)
        {
            subStatus.ParentStatus = WorkOrderStatus.OnHold;
        }

        return fields(item);
    }
}
