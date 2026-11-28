using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Changes a work order's details as the main section of its editor does, then saves it through
/// <see cref="WorkOrderService.UpdateAsync"/>, which checks it.
/// </summary>
internal sealed class UpdateWorkOrderTool : WorkOrderToolBase
{
    public const string ToolName = "updateWorkOrder";

    private static readonly string[] _fields =
    [
        "summary", "description", "priority", "estimatedMinutes", "customerId", "accountId", "contactId", "contactName", "contactPhone",
        "contactEmail", "serviceAddress", "accessNotes", "assetIds", "earliest", "latest", "lines",
    ];

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "number": { "type": "string", "description": "The work order's number, such as WO-000123, from listWorkOrders. Its ID works too." },
        "note": { "type": "string", "description": "A note to add to the work order's history, signed with the user's name. It can be given alone, to add a note and change nothing else." },
        "summary": { "type": "string", "description": "What the work is, in a few words." },
        "description": { "type": "string", "description": "The details. Empty text clears it." },
        "priority": { "type": "string", "enum": ["Low", "Normal", "High", "Urgent"], "description": "How urgent it is. Changing it does not move the service level due times already set." },
        "estimatedMinutes": { "type": "integer", "minimum": 0, "description": "How long the work should take, in minutes." },
        "customerId": { "type": "string", "description": "The user ID of the customer it is for. Empty text clears it." },
        "accountId": { "type": "string", "description": "The account's content item ID. Empty text clears it; with none, the contact's or customer's account is taken." },
        "contactId": { "type": "string", "description": "The CRM contact's content item ID. Empty text clears it." },
        "contactName": { "type": "string", "description": "The name of the person to contact on site. With none, the CRM contact's name is taken." },
        "contactPhone": { "type": "string", "description": "Their phone number." },
        "contactEmail": { "type": "string", "description": "Their email address." },
        "serviceAddress": {
          "type": "object",
          "description": "Where the work is. Only the parts given change; with no street, city and postal code the work is at the account's address.",
          "properties": {
            "addressLine1": { "type": "string", "description": "The street and number." },
            "addressLine2": { "type": "string", "description": "The apartment, suite or floor." },
            "city": { "type": "string", "description": "The city." },
            "region": { "type": "string", "description": "The state, province or region." },
            "postalCode": { "type": "string", "description": "The postal code." },
            "country": { "type": "string", "description": "The two-letter country code." }
          },
          "additionalProperties": false
        },
        "accessNotes": { "type": "string", "description": "How to get in, such as a gate code. Empty text clears it." },
        "assetIds": { "type": "array", "items": { "type": "string" }, "description": "The IDs of the assets the work is on; replaces the list. An empty list clears it." },
        "earliest": { "type": "string", "description": "The earliest time to visit, as yyyy-MM-ddTHH:mm in the user's time zone. Empty text clears it." },
        "latest": { "type": "string", "description": "The latest time to visit, as yyyy-MM-ddTHH:mm in the user's time zone. Empty text clears it." },
        "lines": {
          "type": "array",
          "description": "The planned lines; replaces the list. Give a line's id to keep it: only the properties given change. Lines left out are removed, except those something was already used on.",
          "items": {
            "type": "object",
            "properties": {
              "id": { "type": "string", "description": "The ID of a line the work order already has. Leave it out to add a line." },
              "kind": { "type": "string", "enum": ["Product", "Service", "Labor", "Expense"], "description": "What the line is. Needed for a new line." },
              "description": { "type": "string", "description": "What the line is for. Needed for a new line." },
              "stockItemId": { "type": "string", "description": "For a product line, the Inventory stock item its parts come from." },
              "quantity": { "type": "number", "minimum": 0, "description": "The quantity planned, such as hours of labor. Defaults to 1." },
              "unitPrice": { "type": "number", "minimum": 0, "description": "The price of one unit." },
              "billable": { "type": "boolean", "description": "Whether the customer is billed for it. Defaults to true." }
            },
            "required": [],
            "additionalProperties": false
          }
        }
      },
      "required": ["number"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Changes a work order's summary, description, priority, customer, contact, service address, access notes, assets, time window, estimated length and lines, and adds a note to its history. It does not change the status. Only the fields given change; a note can be given alone.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkOrdersPermissions.ManageWorkOrders;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var workOrder = await WorkOrderToolSupport.FindWorkOrderAsync(services, WorkOrderToolSupport.Read(arguments, "number"), cancellationToken);

        if (workOrder is null)
        {
            return WorkOrderToolSupport.WorkOrderNotFound;
        }

        var note = WorkOrderToolSupport.Read(arguments, "note");
        var changesFields = _fields.Any(field => WorkOrderToolSupport.Has(arguments, field));

        if (!changesFields && note is null)
        {
            return "Give at least one field to change, or a note.";
        }

        var service = services.GetRequiredService<WorkOrderService>();

        if (changesFields && await UpdateFieldsAsync(arguments, workOrder, services, service, user, cancellationToken) is { } refused)
        {
            return refused;
        }

        if (note is not null)
        {
            var noted = await service.AddNoteAsync(workOrder.ItemId, note, user.Identity?.Name, cancellationToken);

            if (!noted.Succeeded)
            {
                return "The note was not added: " + string.Join(" ", noted.Errors);
            }
        }

        if (await WorkOrderToolSupport.SaveAsync(services, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return WorkOrderToolSupport.Json(new
        {
            updated = changesFields,
            noteAdded = note is not null,
            workOrder = await WorkOrderToolResults.DetailAsync(services, workOrder, cancellationToken),
        });
    }

    // Applies the given fields as the editor does and saves them through the work order service, which checks them.
    private static async Task<string> UpdateFieldsAsync(AIFunctionArguments arguments, WorkOrder workOrder, IServiceProvider services, WorkOrderService service, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var localClock = services.GetRequiredService<ILocalClock>();
        var customers = services.GetRequiredService<WorkOrderCustomers>();
        var model = new WorkOrderEditViewModel();

        // The service gives the lines of a new work order an ID, but not lines added later (on the page too). The editor
        // rules keep what was used only on a line with an ID, so a line saved without one gets it first: its use stays a
        // record even when the given lines leave it out.
        GiveLinesIds(workOrder);

        await WorkOrderEditorRules.FillAsync(workOrder, model, localClock);

        if (await OverlayAsync(arguments, workOrder, model, customers, cancellationToken) is { } refused)
        {
            return refused;
        }

        await LinkProductLinesToStockAsync(services, model, cancellationToken);

        await WorkOrderEditorRules.ApplyAsync(workOrder, model, customers, localClock, services.GetRequiredService<IClock>(), cancellationToken);

        // A line this tool adds gets an ID too, as the schema tells the model to give a line's id to keep it and
        // recordWorkOrderPartUsed takes it, so two lines with the same description can be told apart.
        GiveLinesIds(workOrder);

        var result = await service.UpdateAsync(workOrder, user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            WorkOrderToolSupport.Forget(services, workOrder);

            return "The work order was not changed and no note was added: " + string.Join(" ", result.Errors);
        }

        return null;
    }

    private static void GiveLinesIds(WorkOrder workOrder)
    {
        foreach (var line in workOrder.Lines)
        {
            line.Id ??= CrestApps.Core.UniqueId.GenerateId();
        }
    }

    // A product line given by its SKU and no stock item, as a model writes it from what the user said: link it to the
    // stock item with that SKU, as picking the product on the page does, so recordWorkOrderPartUsed takes the parts off
    // stock instead of only noting them.
    private static async Task LinkProductLinesToStockAsync(IServiceProvider services, WorkOrderEditViewModel model, CancellationToken cancellationToken)
    {
        if (model.Lines is not { Count: > 0 } || services.GetService<IStockItemStore>() is not { } stockItems)
        {
            return;
        }

        foreach (var line in model.Lines.Where(line => line.Kind == WorkOrderLineKind.Product && string.IsNullOrEmpty(line.StockItemId) && !string.IsNullOrWhiteSpace(line.Description)))
        {
            if (await stockItems.FindBySkuAsync(line.Description.Trim(), cancellationToken: cancellationToken) is { } item)
            {
                line.StockItemId = item.ItemId;
            }
        }
    }

    private static async Task<string> OverlayAsync(AIFunctionArguments arguments, WorkOrder workOrder, WorkOrderEditViewModel model, WorkOrderCustomers customers, CancellationToken cancellationToken)
    {
        if (WorkOrderToolSupport.Has(arguments, "summary"))
        {
            model.Summary = WorkOrderToolSupport.ReadRaw(arguments, "summary");
        }

        if (WorkOrderToolSupport.Has(arguments, "description"))
        {
            model.Description = WorkOrderToolSupport.ReadRaw(arguments, "description");
        }

        if (WorkOrderToolSupport.Has(arguments, "priority"))
        {
            if (!WorkOrderToolSupport.TryParseEnum<WorkOrderPriority>(WorkOrderToolSupport.Read(arguments, "priority"), out var priority))
            {
                return $"The priority must be one of {WorkOrderToolSupport.Names<WorkOrderPriority>()}.";
            }

            model.Priority = priority;
        }

        if (WorkOrderToolSupport.Has(arguments, "estimatedMinutes"))
        {
            if (WorkOrderToolSupport.ReadInt(arguments, "estimatedMinutes") is not { } minutes || minutes < 0)
            {
                return "The estimatedMinutes must be a whole number of minutes, 0 or more.";
            }

            model.EstimatedMinutes = minutes;
        }

        foreach (var (name, kind) in new[] { ("customerId", AssetOwnerKind.Customer), ("accountId", AssetOwnerKind.Account), ("contactId", AssetOwnerKind.Contact) })
        {
            if (!WorkOrderToolSupport.Has(arguments, name))
            {
                continue;
            }

            var id = WorkOrderToolSupport.Read(arguments, name);

            if (id is not null && await CheckPartyAsync(customers, kind, id, name, cancellationToken) is { } refused)
            {
                return refused;
            }

            switch (kind)
            {
                case AssetOwnerKind.Customer:
                    model.CustomerId = id;
                    break;
                case AssetOwnerKind.Account:
                    model.AccountId = id;
                    break;
                default:
                    model.ContactId = id;
                    break;
            }
        }

        if (WorkOrderToolSupport.Has(arguments, "contactName"))
        {
            model.ContactName = WorkOrderToolSupport.ReadRaw(arguments, "contactName");
        }
        else if (WorkOrderToolSupport.Has(arguments, "contactId") && !string.Equals(model.ContactId, workOrder.ContactId, StringComparison.Ordinal))
        {
            // A new contact with no name given brings its own name, as picking one in the editor does.
            model.ContactName = null;
        }

        if (WorkOrderToolSupport.Has(arguments, "contactPhone"))
        {
            model.ContactPhone = WorkOrderToolSupport.ReadRaw(arguments, "contactPhone");
        }

        if (WorkOrderToolSupport.Has(arguments, "contactEmail"))
        {
            model.ContactEmail = WorkOrderToolSupport.ReadRaw(arguments, "contactEmail");
        }

        if (WorkOrderToolSupport.Has(arguments, "serviceAddress"))
        {
            if (WorkOrderToolSupport.ReadObject(arguments, "serviceAddress") is not { } address)
            {
                return "The serviceAddress must be an object with addressLine1, addressLine2, city, region, postalCode and country.";
            }

            model.AddressLine1 = Overlay(address, "addressLine1", model.AddressLine1);
            model.AddressLine2 = Overlay(address, "addressLine2", model.AddressLine2);
            model.City = Overlay(address, "city", model.City);
            model.Region = Overlay(address, "region", model.Region);
            model.PostalCode = Overlay(address, "postalCode", model.PostalCode);
            model.Country = Overlay(address, "country", model.Country);
        }

        if (WorkOrderToolSupport.Has(arguments, "accessNotes"))
        {
            model.AccessNotes = WorkOrderToolSupport.ReadRaw(arguments, "accessNotes");
        }

        if (WorkOrderToolSupport.Has(arguments, "assetIds"))
        {
            model.AssetIds = [.. WorkOrderToolSupport.ReadList(arguments, "assetIds")];
        }

        foreach (var name in new[] { "earliest", "latest" })
        {
            if (!WorkOrderToolSupport.Has(arguments, name))
            {
                continue;
            }

            var text = WorkOrderToolSupport.Read(arguments, name);
            DateTime? local = null;

            if (text is not null)
            {
                if (!WorkOrderToolSupport.TryParseLocal(text, out var parsed))
                {
                    return $"The {name} time must be written as yyyy-MM-ddTHH:mm, in the user's time zone.";
                }

                local = parsed;
            }

            if (name == "earliest")
            {
                model.Earliest = local;
            }
            else
            {
                model.Latest = local;
            }
        }

        if (WorkOrderToolSupport.Has(arguments, "lines"))
        {
            if (WorkOrderToolSupport.ReadObjects(arguments, "lines") is not { } lines)
            {
                return "The lines must be a list of objects with kind, description, quantity and unitPrice.";
            }

            if (WorkOrderSettingFields.ReadLines(lines, model.Lines, "The work order", out var rows) is { } refused)
            {
                return refused;
            }

            model.Lines = rows;
        }

        return null;
    }

    private static async Task<string> CheckPartyAsync(WorkOrderCustomers customers, AssetOwnerKind kind, string id, string name, CancellationToken cancellationToken)
    {
        if (!customers.CanPick(kind))
        {
            return $"The {name} can't be set on this site: the feature that keeps those records is off.";
        }

        return await customers.NameAsync(kind, id, cancellationToken) is null
            ? $"Nothing was found with the {name} '{id}'."
            : null;
    }

    private static string Overlay(JsonElement address, string name, string current)
        => WorkOrderToolSupport.HasProperty(address, name) ? WorkOrderToolSupport.GetRaw(address, name) : current;
}
