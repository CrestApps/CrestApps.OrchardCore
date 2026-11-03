using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.Workforce.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Dispatch.AI;

/// <summary>
/// Creates a work order from a conversation, such as a customer's call.
/// </summary>
public sealed class WorkOrderCreateTool : WorkforceAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "workOrderCreate";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "typeName": { "type": "string", "description": "The work order type, such as Furnace repair. Leave out to use the only type, or to be told the types." },
            "summary": { "type": "string", "description": "What is wrong or wanted, in a few words." },
            "description": { "type": "string", "description": "The details the customer gave." },
            "priority": { "type": "string", "enum": ["Low", "Normal", "High", "Urgent"] },
            "contactName": { "type": "string" },
            "contactPhone": { "type": "string" },
            "contactEmail": { "type": "string" },
            "street": { "type": "string", "description": "The service address's street and number." },
            "city": { "type": "string" },
            "region": { "type": "string", "description": "The state or region." },
            "postalCode": { "type": "string" },
            "accessNotes": { "type": "string", "description": "How to get in, such as a gate code." }
          },
          "required": ["summary"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Creates a work order with the customer's problem, contact and service address, and gives its number. Confirm the details with the person first.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, WorkOrdersPermissions.ManageWorkOrders))
        {
            return "You can't create work orders.";
        }

        var types = (await services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>().GetAllAsync(cancellationToken)).ToList();
        var typeName = Read(arguments, "typeName");
        var type = typeName is null
            ? types.Count == 1 ? types[0] : null
            : types.FirstOrDefault(item => string.Equals(item.Name, typeName, StringComparison.OrdinalIgnoreCase));

        if (type is null)
        {
            return JsonSerializer.Serialize(new { error = "Choose the type of work order.", types = types.Select(item => item.Name) }, JsonOptions);
        }

        var service = services.GetRequiredService<WorkOrderService>();
        var workOrder = await service.NewAsync(type.ItemId, cancellationToken);

        workOrder.Summary = Read(arguments, "summary");
        workOrder.Description = Read(arguments, "description");
        workOrder.AccessNotes = Read(arguments, "accessNotes");

        if (Enum.TryParse<WorkOrderPriority>(Read(arguments, "priority"), true, out var priority))
        {
            workOrder.Priority = priority;
        }

        workOrder.Contact = new WorkOrderContact { Name = Read(arguments, "contactName"), Phone = Read(arguments, "contactPhone"), Email = Read(arguments, "contactEmail") };

        if (Read(arguments, "street") is { } street)
        {
            workOrder.ServiceAddress = new Address { AddressLine1 = street, City = Read(arguments, "city"), Region = Read(arguments, "region"), PostalCode = Read(arguments, "postalCode") };
        }

        var result = await service.CreateAsync(workOrder, user.Identity?.Name, cancellationToken);

        if (result.Errors.Count > 0)
        {
            return JsonSerializer.Serialize(new { error = string.Join(" ", result.Errors) }, JsonOptions);
        }

        await services.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

        return JsonSerializer.Serialize(new { number = result.WorkOrder.Number, type = type.Name, status = result.WorkOrder.Status.ToString() }, JsonOptions);
    }
}

/// <summary>
/// Gives where a work order stands: its status, due times and visits.
/// </summary>
public sealed class WorkOrderStatusTool : WorkforceAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "workOrderStatus";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "number": { "type": "string", "description": "The work order's number, such as WO-000012." }
          },
          "required": ["number"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Gives a work order's status, when it must be answered and resolved by, and its visits with their technicians, times and ETA.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, WorkOrdersPermissions.ViewWorkOrders))
        {
            return "You can't see work orders.";
        }

        var workOrder = await FieldServiceLookups.WorkOrderAsync(services, Read(arguments, "number"), cancellationToken);

        if (workOrder is null)
        {
            return $"No work order is numbered '{Read(arguments, "number")}'.";
        }

        var zone = await FieldServiceLookups.TimeZoneAsync(services);
        var visits = await services.GetRequiredService<IDispatchService>().ListForWorkOrderAsync(workOrder.ItemId, cancellationToken);
        var list = new List<object>();

        foreach (var visit in visits)
        {
            list.Add(new
            {
                number = visit.Number,
                status = visit.Status.ToString(),
                start = FieldServiceLookups.Local(visit.StartUtc, zone),
                end = FieldServiceLookups.Local(visit.EndUtc, zone),
                technicians = await FieldServiceLookups.NamesAsync(services, visit.WorkerIds, cancellationToken),
                eta = FieldServiceLookups.Local(visit.EtaUtc, zone),
                atRisk = visit.AtRiskReason,
            });
        }

        return JsonSerializer.Serialize(new
        {
            number = workOrder.Number,
            summary = workOrder.Summary,
            status = workOrder.Status.ToString(),
            priority = workOrder.Priority.ToString(),
            respondBy = FieldServiceLookups.Local(workOrder.Sla?.RespondByUtc, zone),
            resolveBy = FieldServiceLookups.Local(workOrder.Sla?.ResolveByUtc, zone),
            visits = list,
            timeZone = zone,
        }, JsonOptions);
    }
}

/// <summary>
/// Gives what a technician needs to know about a work order before going: the job, the history and what is left.
/// </summary>
public sealed class WorkOrderSummarizeTool : WorkforceAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "workOrderSummarize";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "number": { "type": "string", "description": "The work order's number, such as WO-000012." }
          },
          "required": ["number"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Gives a work order's details and history (notes, checklist answers, parts used, signatures and visits), so they can be summarized for a technician.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, WorkOrdersPermissions.ViewWorkOrders))
        {
            return "You can't see work orders.";
        }

        var workOrder = await FieldServiceLookups.WorkOrderAsync(services, Read(arguments, "number"), cancellationToken);

        if (workOrder is null)
        {
            return $"No work order is numbered '{Read(arguments, "number")}'.";
        }

        var zone = await FieldServiceLookups.TimeZoneAsync(services);
        var visits = await services.GetRequiredService<IDispatchService>().ListForWorkOrderAsync(workOrder.ItemId, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            number = workOrder.Number,
            summary = workOrder.Summary,
            description = workOrder.Description,
            status = workOrder.Status.ToString(),
            accessNotes = workOrder.AccessNotes,
            contact = workOrder.Contact?.Name,
            notes = workOrder.Notes.TakeLast(20).Select(note => new { at = FieldServiceLookups.Local(note.Utc, zone), by = note.By, text = note.Text }),
            checklists = workOrder.Checklists.Select(checklist => new
            {
                name = checklist.Name,
                answers = checklist.Questions.Select(question => new { question = question.Text, answer = checklist.Answers.FirstOrDefault(answer => answer.QuestionId == question.Id)?.Value }),
            }),
            parts = workOrder.Lines.Where(line => line.QuantityUsed > 0).Select(line => new { line.Description, used = line.QuantityUsed }),
            signedBy = workOrder.Signatures.Select(signature => signature.Name),
            photos = workOrder.Attachments.Count,
            visits = visits.Select(visit => new { number = visit.Number, status = visit.Status.ToString(), start = FieldServiceLookups.Local(visit.StartUtc, zone), incompleteReason = visit.IncompleteReason }),
        }, JsonOptions);
    }
}
