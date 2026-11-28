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
/// Lists one kind of work order settings record, as the Work Order Types, Checklists, Service Levels and Sub-statuses
/// pages do, with a search on the name.
/// </summary>
internal sealed class ListWorkOrderSettingsTool : WorkOrderToolBase
{
    public const string ToolName = "listWorkOrderSettings";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "kind": { "type": "string", "enum": ["types", "checklists", "service-levels", "sub-statuses"], "description": "Which records: work order types, checklists, service levels or sub-statuses." },
        "search": { "type": "string", "description": "Text the name contains." },
        "page": { "type": "integer", "minimum": 1, "description": "The page to return, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "How many records per page, at most 50. Defaults to 20." }
      },
      "required": ["kind"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists the work order types, checklists, service levels or sub-statuses (choose the kind), with a name search. Use it to find a record's ID.";

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

        var search = WorkOrderToolSupport.Read(arguments, "search");
        var (page, pageSize) = WorkOrderToolSupport.ReadPaging(arguments);
        var types = (await services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>().GetAllAsync(cancellationToken)).ToList();
        IReadOnlyList<(string Name, Func<Task<object>> Row)> rows = kind switch
        {
            WorkOrderSettingKinds.Types => await TypeRowsAsync(services, types, cancellationToken),
            WorkOrderSettingKinds.Checklists => await ChecklistRowsAsync(services, types, cancellationToken),
            WorkOrderSettingKinds.ServiceLevels => await PolicyRowsAsync(services, cancellationToken),
            _ => await SubStatusRowsAsync(services, cancellationToken),
        };

        var matching = rows.Where(row => search is null || row.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true).ToList();
        var items = new List<object>();

        foreach (var row in matching.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(await row.Row());
        }

        return WorkOrderToolSupport.Json(new
        {
            kind,
            items,
            total = matching.Count,
            page,
            pageSize,
            truncated = ((page - 1) * pageSize) + items.Count < matching.Count,
        });
    }

    private static async Task<IReadOnlyList<(string Name, Func<Task<object>> Row)>> TypeRowsAsync(IServiceProvider services, List<WorkOrderType> types, CancellationToken cancellationToken)
    {
        var policies = (await services.GetRequiredService<IServiceLevelPolicyManager>().ListByScopeAsync(WorkOrdersConstants.ServiceLevelScope, cancellationToken)).ToDictionary(policy => policy.ItemId, policy => policy.Name, StringComparer.Ordinal);
        var store = services.GetRequiredService<WorkOrderStore>();

        return [.. types.OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase).Select(type => (type.Name, (Func<Task<object>>)(async () =>
        {
            var counts = await store.CountByTypeAsync([type.ItemId], cancellationToken);

            return new
            {
                id = type.ItemId,
                name = type.Name,
                defaultPriority = type.DefaultPriority.ToString(),
                defaultDurationMinutes = type.DefaultDurationMinutes,
                billingMode = type.BillingMode.ToString(),
                fixedPrice = type.FixedPrice,
                currency = type.Currency,
                serviceLevel = string.IsNullOrEmpty(type.ServiceLevelPolicyId) ? null : policies.GetValueOrDefault(type.ServiceLevelPolicyId) ?? type.ServiceLevelPolicyId,
                checklists = type.ChecklistTemplateIds.Count,
                workOrders = counts.GetValueOrDefault(type.ItemId),
            };
        })))];
    }

    private static async Task<IReadOnlyList<(string Name, Func<Task<object>> Row)>> ChecklistRowsAsync(IServiceProvider services, List<WorkOrderType> types, CancellationToken cancellationToken)
    {
        var checklists = await services.GetRequiredService<WorkOrderSettingManager<ChecklistTemplate>>().GetAllAsync(cancellationToken);

        return [.. checklists.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Select(item => (item.Name, (Func<Task<object>>)(() => Task.FromResult<object>(new
        {
            id = item.ItemId,
            name = item.Name,
            questions = item.Questions.Count,
            required = item.Questions.Count(question => question.Required),
            usedBy = types.Where(type => type.ChecklistTemplateIds.Contains(item.ItemId)).Select(type => type.Name),
        }))))];
    }

    private static async Task<IReadOnlyList<(string Name, Func<Task<object>> Row)>> PolicyRowsAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // Work order service levels are the shared Service Levels policies of the Work orders scope.
        var policies = await services.GetRequiredService<IServiceLevelPolicyManager>().ListByScopeAsync(WorkOrdersConstants.ServiceLevelScope, cancellationToken);

        return [.. policies.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Select(item => (item.Name, (Func<Task<object>>)(() => Task.FromResult(WorkOrderSettingFields.Record(item)))))];
    }

    private static async Task<IReadOnlyList<(string Name, Func<Task<object>> Row)>> SubStatusRowsAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var subStatuses = await services.GetRequiredService<WorkOrderSettingManager<WorkOrderSubStatus>>().GetAllAsync(cancellationToken);

        return [.. subStatuses.OrderBy(item => item.ParentStatus).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Select(item => (item.Name, (Func<Task<object>>)(() => Task.FromResult(WorkOrderSettingFields.Record(item)))))];
    }
}
