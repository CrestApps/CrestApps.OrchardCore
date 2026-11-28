using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.WorkOrders.Controllers;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Creates or updates a work order type, checklist, service level or sub-status the way its editor does: the record's
/// fields fill the editor, the given fields replace theirs, the editor's rules apply them and the manager validates the
/// record before it is saved. A refused save changes nothing.
/// </summary>
internal sealed class SaveWorkOrderSettingTool : WorkOrderToolBase
{
    public const string ToolName = "saveWorkOrderSetting";

    private static readonly JsonElement _schema = BuildSchema();

    public override string Name => ToolName;

    public override string Description => "Creates or updates a work order type, checklist, service level or sub-status; only the fields given change. Each field says which kinds it belongs to; getWorkOrderSetting gives a kind's fields. A type refers to checklists, a service level, skills, certifications and appointment types by ID, so create those first.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkOrdersPermissions.ManageWorkOrderSettings;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var kind = WorkOrderSettingFields.ParseKind(WorkOrderToolSupport.Read(arguments, "kind"));

        if (kind is null)
        {
            return "The kind must be types, checklists, service-levels or sub-statuses.";
        }

        var fields = WorkOrderSettingFields.FieldsOf(kind);
        var foreign = WorkOrderSettingFields.AllFields.Where(field => WorkOrderToolSupport.Has(arguments, field) && !fields.Contains(field)).ToList();

        if (foreign.Count > 0)
        {
            return $"A record of kind {kind} has no {string.Join(", ", foreign)}. Its fields are {string.Join(", ", fields)}.";
        }

        var id = WorkOrderToolSupport.Read(arguments, "id");

        if (id is not null && !fields.Any(field => WorkOrderToolSupport.Has(arguments, field)))
        {
            return "Give at least one field to change.";
        }

        var localizer = services.GetRequiredService<IStringLocalizer<WorkOrderSettingsController>>();

        switch (kind)
        {
            case WorkOrderSettingKinds.Types:
                return await SaveAsync<WorkOrderType, ViewModels.WorkOrderTypeEditViewModel>(
                    services,
                    kind,
                    id,
                    WorkOrderSettingsEditorRules.FillType,
                    form => WorkOrderSettingFields.OverlayAsync(arguments, form, services, cancellationToken),
                    (item, form) => WorkOrderSettingsEditorRules.ApplyType(item, form, localizer),
                    WorkOrderSettingFields.Record,
                    cancellationToken);

            case WorkOrderSettingKinds.Checklists:
                return await SaveAsync<ChecklistTemplate, ViewModels.ChecklistTemplateEditViewModel>(
                    services,
                    kind,
                    id,
                    WorkOrderSettingsEditorRules.FillChecklist,
                    form => Task.FromResult(WorkOrderSettingFields.Overlay(arguments, form)),
                    (item, form) => WorkOrderSettingsEditorRules.ApplyChecklist(item, form, localizer),
                    WorkOrderSettingFields.Record,
                    cancellationToken);

            case WorkOrderSettingKinds.ServiceLevels:
                return await SavePolicyAsync(arguments, services, user, id, cancellationToken);

            default:
                return await SaveAsync<WorkOrderSubStatus, ViewModels.WorkOrderSubStatusEditViewModel>(
                    services,
                    kind,
                    id,
                    WorkOrderSettingsEditorRules.FillSubStatus,
                    form => Task.FromResult(WorkOrderSettingFields.Overlay(arguments, form)),
                    (item, form) =>
                    {
                        WorkOrderSettingsEditorRules.ApplySubStatus(item, form);

                        return [];
                    },
                    WorkOrderSettingFields.Record,
                    cancellationToken);
        }
    }

    private static async Task<object> SaveAsync<T, TForm>(
        IServiceProvider services,
        string kind,
        string id,
        Func<T, string, TForm> fill,
        Func<TForm, Task<string>> overlay,
        Func<T, TForm, IReadOnlyList<(string Field, string Message)>> apply,
        Func<T, object> record,
        CancellationToken cancellationToken)
        where T : CatalogItem, new()
    {
        var manager = services.GetRequiredService<WorkOrderSettingManager<T>>();
        var isNew = id is null;
        var item = isNew ? await manager.NewAsync(cancellationToken: cancellationToken) : await manager.FindByIdAsync(id, cancellationToken);

        if (item is null)
        {
            return $"No record of kind {kind} has the ID '{id}'. Use listWorkOrderSettings to find it, or leave the id out to create one.";
        }

        var form = fill(item, id);

        if (await overlay(form) is { } refused)
        {
            return refused;
        }

        var errors = apply(item, form).Select(error => error.Message).ToList();
        var validation = await manager.ValidateAsync(item, cancellationToken);

        errors.AddRange(validation.Errors.Select(error => error.ErrorMessage));

        if (errors.Count > 0)
        {
            if (!isNew)
            {
                WorkOrderToolSupport.Forget(services, item);
            }

            return "Nothing was saved: " + string.Join(" ", errors.Distinct(StringComparer.Ordinal));
        }

        if (isNew)
        {
            await manager.CreateAsync(item, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(item, null, cancellationToken);
        }

        if (await WorkOrderToolSupport.SaveAsync(services, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return WorkOrderToolSupport.Json(new
        {
            saved = true,
            created = isNew,
            kind,
            record = record(item),
            adminPath = WorkOrderToolSupport.SettingAdminPath(services, kind, item.ItemId),
        });
    }

    // Work order service levels are shared Service Levels policies of the Work orders scope, made and changed on the
    // Service Levels page with its permission; the policy manager's handler validates them as it validates that page.
    private static async Task<object> SavePolicyAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, string id, CancellationToken cancellationToken)
    {
        if (await WorkOrderToolSupport.AuthorizeServiceLevelsAsync(services, user) is { } refused)
        {
            return refused;
        }

        var manager = services.GetRequiredService<IServiceLevelPolicyManager>();
        var isNew = id is null;
        var policy = isNew ? await manager.NewAsync(cancellationToken: cancellationToken) : await manager.FindByIdAsync(id, cancellationToken);

        if (policy is null || (!isNew && !WorkOrderSettingFields.IsWorkOrderPolicy(policy)))
        {
            return $"No record of kind {WorkOrderSettingKinds.ServiceLevels} has the ID '{id}'. Use listWorkOrderSettings to find it, or leave the id out to create one.";
        }

        if (isNew)
        {
            policy.Scope = WorkOrdersConstants.ServiceLevelScope;
        }

        if (WorkOrderSettingFields.ApplyPolicy(arguments, policy, isNew) is { } invalid)
        {
            if (!isNew)
            {
                WorkOrderToolSupport.Forget(services, policy);
            }

            return invalid;
        }

        var validation = await manager.ValidateAsync(policy, cancellationToken);

        if (!validation.Succeeded)
        {
            if (!isNew)
            {
                WorkOrderToolSupport.Forget(services, policy);
            }

            return "Nothing was saved: " + string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Distinct(StringComparer.Ordinal));
        }

        if (isNew)
        {
            await manager.CreateAsync(policy, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(policy, null, cancellationToken);
        }

        if (await WorkOrderToolSupport.SaveAsync(services, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return WorkOrderToolSupport.Json(new
        {
            saved = true,
            created = isNew,
            kind = WorkOrderSettingKinds.ServiceLevels,
            record = WorkOrderSettingFields.Record(policy),
            adminPath = WorkOrderToolSupport.SettingAdminPath(services, WorkOrderSettingKinds.ServiceLevels, policy.ItemId),
        });
    }

    private static JsonElement BuildSchema()
    {
        var properties = JsonNode.Parse("""
        {
          "kind": { "type": "string", "enum": ["types", "checklists", "service-levels", "sub-statuses"], "description": "The kind of record. Only the fields marked with this kind can be given." },
          "id": { "type": "string", "description": "The ID of the record to update, from listWorkOrderSettings. Leave it out to create one." }
        }
        """)!.AsObject();

        foreach (var property in JsonNode.Parse(WorkOrderSettingFields.AllFieldsJson())!.AsObject())
        {
            properties[property.Key] = property.Value?.DeepClone();
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = new JsonArray("kind"),
            ["additionalProperties"] = false,
        };

        return JsonSerializer.Deserialize<JsonElement>(schema.ToJsonString());
    }
}
