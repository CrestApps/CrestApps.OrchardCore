using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.WorkOrders.Agreements;
using CrestApps.OrchardCore.WorkOrders.Agreements.ViewModels;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Creates or updates a service agreement, a shared agreement with field-service terms, the way the Agreements editor
/// and the work orders' part of it do: the agreement fills the fields, the given fields replace theirs, the editors'
/// rules apply them and the manager validates the agreement before it is saved.
/// </summary>
internal sealed class SaveServiceAgreementTool : WorkOrderToolBase
{
    public const string ToolName = "saveServiceAgreement";

    private static readonly string[] _fields =
    [
        "name", "status", "customerId", "accountId", "contactName", "contactPhone", "contactEmail", "serviceAddress", "accessNotes", "assetIds",
        "startDate", "endDate", "timeZoneId", "pricing", "pricePerVisit", "expiringNoticeDays", "notes", "schedules",
        "hoursKind", "hours", "roundingMinutes",
    ];

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "agreement": { "type": "string", "description": "The number (such as SA-00012) or ID of the agreement to update, from listRecords with the kind agreement. Leave it out to create one." },
        "name": { "type": "string", "description": "The agreement's name, such as Furnace maintenance." },
        "status": { "type": "string", "enum": ["Draft", "Active", "Paused", "Expired", "Canceled"], "description": "Draft while being set up; Active makes its work orders ahead of each visit; Paused and Canceled stop them. A new agreement starts as Draft." },
        "customerId": { "type": "string", "description": "The user ID of the customer. Empty text clears it." },
        "accountId": { "type": "string", "description": "The account's content item ID. Empty text clears it." },
        "contactName": { "type": "string", "description": "The name of the person to contact on site." },
        "contactPhone": { "type": "string", "description": "Their phone number." },
        "contactEmail": { "type": "string", "description": "Their email address." },
        "serviceAddress": {
          "type": "object",
          "description": "Where the visits are. Only the parts given change; with no street, city and postal code, an agreement with schedules has its visits at the account's address.",
          "properties": {
            "addressLine1": { "type": "string", "description": "The street and number." },
            "city": { "type": "string", "description": "The city." },
            "region": { "type": "string", "description": "The state, province or region." },
            "postalCode": { "type": "string", "description": "The postal code." },
            "country": { "type": "string", "description": "The two-letter country code." }
          },
          "additionalProperties": false
        },
        "accessNotes": { "type": "string", "description": "How to get in, such as a gate code. Empty text clears it." },
        "assetIds": { "type": "array", "items": { "type": "string" }, "description": "The IDs of the assets the agreement covers; replaces the list." },
        "startDate": { "type": "string", "description": "The first day, as yyyy-MM-dd." },
        "endDate": { "type": "string", "description": "The last day, as yyyy-MM-dd. Empty text means no end." },
        "timeZoneId": { "type": "string", "description": "The time zone its visit days are in, such as America/New_York. A new agreement takes the user's." },
        "pricing": { "type": "string", "enum": ["Included", "PerVisit"], "description": "Whether the visits are included (the work orders bill nothing) or billed per visit." },
        "pricePerVisit": { "type": "number", "exclusiveMinimum": 0, "description": "The price of a visit, needed when pricing is PerVisit." },
        "expiringNoticeDays": { "type": "integer", "minimum": 0, "maximum": 365, "description": "How many days before the end the Service Agreement Expiring event is raised." },
        "notes": { "type": "string", "description": "Notes. Empty text clears them." },
        "hoursKind": { "type": "string", "enum": ["None", "Block", "MonthlyAllowance"], "description": "How the agreement covers hours of work: None, a Block of hours used over its whole period, or a MonthlyAllowance each calendar month that doesn't roll over." },
        "hours": { "type": "number", "exclusiveMinimum": 0, "maximum": 100000, "description": "The hours of the block, or of each month's allowance. Needed unless hoursKind is None." },
        "roundingMinutes": { "type": "integer", "enum": [0, 6, 15, 30], "description": "What each entry of time is rounded up to, in minutes; 0 counts it as it is." },
        "schedules": {
          "type": "array",
          "description": "The recurring visits; replaces the list. Give a schedule's id to keep it, so its visits already made are not made again; only the properties given change. An active agreement needs at least one.",
          "items": {
            "type": "object",
            "properties": {
              "id": { "type": "string", "description": "The ID of a schedule the agreement already has. Leave it out for a new schedule." },
              "name": { "type": "string", "description": "What the visit is, such as Annual furnace service." },
              "workOrderTypeId": { "type": "string", "description": "The ID of the work order type each visit's work order gets (listWorkOrderSettings with kind types). Needed for a new schedule." },
              "rule": { "type": "string", "description": "When it repeats, as an iCalendar RRULE such as FREQ=YEARLY, FREQ=MONTHLY;INTERVAL=6 or FREQ=WEEKLY;BYDAY=MO, counted from the start date. Needed for a new schedule." },
              "leadDays": { "type": "integer", "minimum": 0, "maximum": 365, "description": "How many days ahead each visit's work order is made. Defaults to 14." },
              "assetIds": { "type": "array", "items": { "type": "string" }, "description": "The assets of this schedule's visits; with none, the agreement's." }
            },
            "required": [],
            "additionalProperties": false
          }
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Creates or updates a service agreement: customer, contact, address, assets, start and end, the hours it covers, schedules (work order type, repeat rule, lead days), pricing and status. Only the fields given change. Setting the status to Active starts making work orders; Canceled stops them.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => AgreementsPermissions.ManageAgreements;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<IAgreementManager>();
        var customers = services.GetRequiredService<WorkOrderCustomers>();
        var reference = WorkOrderToolSupport.Read(arguments, "agreement");
        var isNew = reference is null;
        Agreement agreement;

        if (isNew)
        {
            if (WorkOrderToolSupport.Read(arguments, "name") is null)
            {
                return "A new agreement needs a name.";
            }

            agreement = await manager.NewAsync(cancellationToken: cancellationToken);

            // As the Agreements page starts one: in the time zone the user sees times in (theirs, else the site's).
            agreement.TimeZoneId ??= (await services.GetRequiredService<ILocalClock>().GetLocalTimeZoneAsync())?.TimeZoneId;
        }
        else
        {
            agreement = await WorkOrderToolSupport.FindAgreementAsync(services, reference, cancellationToken);

            if (agreement is null)
            {
                return WorkOrderToolSupport.AgreementNotFound;
            }

            if (!_fields.Any(field => WorkOrderToolSupport.Has(arguments, field)))
            {
                return "Give at least one field to change.";
            }
        }

        // Every given value is checked before the agreement is touched.
        var form = ServiceAgreementEditorRules.Fill(agreement);

        if (await OverlayAsync(arguments, form, services, customers, cancellationToken) is { } refused)
        {
            return refused;
        }

        await ServiceAgreementEditorRules.ApplyAsync(agreement, form, customers, cancellationToken);

        var validation = await manager.ValidateAsync(agreement, cancellationToken);

        if (!validation.Succeeded)
        {
            if (!isNew)
            {
                WorkOrderToolSupport.Forget(services, agreement);
            }

            return "Nothing was saved: " + string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Distinct(StringComparer.Ordinal));
        }

        if (isNew)
        {
            await manager.CreateAsync(agreement, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(agreement, null, cancellationToken);
        }

        if (await WorkOrderToolSupport.SaveAsync(services, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return WorkOrderToolSupport.Json(new
        {
            saved = true,
            created = isNew,
            agreement = await ServiceAgreementToolResults.DetailAsync(services, agreement, cancellationToken),
        });
    }

    private static async Task<string> OverlayAsync(AIFunctionArguments arguments, ServiceAgreementForm form, IServiceProvider services, WorkOrderCustomers customers, CancellationToken cancellationToken)
    {
        if (WorkOrderToolSupport.Has(arguments, "name"))
        {
            form.General.Name = WorkOrderToolSupport.ReadRaw(arguments, "name");
        }

        if (WorkOrderToolSupport.Has(arguments, "status"))
        {
            if (!WorkOrderToolSupport.TryParseEnum<AgreementStatus>(WorkOrderToolSupport.Read(arguments, "status"), out var status))
            {
                return $"The status must be one of {WorkOrderToolSupport.Names<AgreementStatus>()}.";
            }

            form.General.Status = status;
        }

        foreach (var (name, kind) in new[] { ("customerId", AssetOwnerKind.Customer), ("accountId", AssetOwnerKind.Account) })
        {
            if (!WorkOrderToolSupport.Has(arguments, name))
            {
                continue;
            }

            var id = WorkOrderToolSupport.Read(arguments, name);

            if (id is not null)
            {
                if (!customers.CanPick(kind))
                {
                    return $"The {name} can't be set on this site: the feature that keeps those records is off.";
                }

                if (await customers.NameAsync(kind, id, cancellationToken) is null)
                {
                    return $"Nothing was found with the {name} '{id}'.";
                }
            }

            if (kind == AssetOwnerKind.Customer)
            {
                form.Terms.CustomerId = id;
            }
            else
            {
                form.General.AccountId = id;
            }
        }

        if (WorkOrderToolSupport.Has(arguments, "contactName"))
        {
            form.Terms.ContactName = WorkOrderToolSupport.ReadRaw(arguments, "contactName");
        }

        if (WorkOrderToolSupport.Has(arguments, "contactPhone"))
        {
            form.Terms.ContactPhone = WorkOrderToolSupport.ReadRaw(arguments, "contactPhone");
        }

        if (WorkOrderToolSupport.Has(arguments, "contactEmail"))
        {
            form.Terms.ContactEmail = WorkOrderToolSupport.ReadRaw(arguments, "contactEmail");
        }

        if (WorkOrderToolSupport.Has(arguments, "serviceAddress"))
        {
            if (WorkOrderToolSupport.ReadObject(arguments, "serviceAddress") is not { } address)
            {
                return "The serviceAddress must be an object with addressLine1, city, region, postalCode and country.";
            }

            form.Terms.AddressLine1 = Overlay(address, "addressLine1", form.Terms.AddressLine1);
            form.Terms.City = Overlay(address, "city", form.Terms.City);
            form.Terms.Region = Overlay(address, "region", form.Terms.Region);
            form.Terms.PostalCode = Overlay(address, "postalCode", form.Terms.PostalCode);
            form.Terms.Country = Overlay(address, "country", form.Terms.Country);
        }

        if (WorkOrderToolSupport.Has(arguments, "accessNotes"))
        {
            form.Terms.AccessNotes = WorkOrderToolSupport.ReadRaw(arguments, "accessNotes");
        }

        if (WorkOrderToolSupport.Has(arguments, "notes"))
        {
            form.General.Notes = WorkOrderToolSupport.ReadRaw(arguments, "notes");
        }

        if (WorkOrderToolSupport.ReadList(arguments, "assetIds") is { } assetIds)
        {
            form.Terms.AssetIds = [.. assetIds];
        }

        if (WorkOrderToolSupport.Has(arguments, "startDate"))
        {
            if (!WorkOrderToolSupport.TryParseDay(WorkOrderToolSupport.Read(arguments, "startDate"), out var start))
            {
                return "The startDate must be a day written as yyyy-MM-dd.";
            }

            form.General.StartDate = start;
        }

        if (WorkOrderToolSupport.Has(arguments, "endDate"))
        {
            var text = WorkOrderToolSupport.Read(arguments, "endDate");

            if (text is null)
            {
                form.General.EndDate = null;
            }
            else if (WorkOrderToolSupport.TryParseDay(text, out var end))
            {
                form.General.EndDate = end;
            }
            else
            {
                return "The endDate must be a day written as yyyy-MM-dd, or empty text for no end.";
            }
        }

        if (WorkOrderToolSupport.Has(arguments, "timeZoneId"))
        {
            var zone = WorkOrderToolSupport.Read(arguments, "timeZoneId");

            if (zone is null || !CalendarTime.IsValidZone(zone))
            {
                return $"'{zone}' is not a known time zone. Use an IANA name such as America/New_York.";
            }

            form.General.TimeZoneId = CalendarTime.NormalizeZoneId(zone);
        }

        if (WorkOrderToolSupport.Has(arguments, "pricing"))
        {
            if (!WorkOrderToolSupport.TryParseEnum<AgreementPricing>(WorkOrderToolSupport.Read(arguments, "pricing"), out var pricing))
            {
                return $"The pricing must be one of {WorkOrderToolSupport.Names<AgreementPricing>()}.";
            }

            form.Terms.Pricing = pricing;
        }

        if (WorkOrderToolSupport.Has(arguments, "pricePerVisit"))
        {
            if (WorkOrderToolSupport.ReadDecimal(arguments, "pricePerVisit") is not { } price)
            {
                return "The pricePerVisit must be a number.";
            }

            form.Terms.PricePerVisit = price;
        }

        if (WorkOrderToolSupport.Has(arguments, "expiringNoticeDays"))
        {
            if (WorkOrderToolSupport.ReadInt(arguments, "expiringNoticeDays") is not { } days)
            {
                return "The expiringNoticeDays must be a whole number of days.";
            }

            form.General.ExpiringNoticeDays = days;
        }

        // The hours the agreement covers, as its editor's hours section takes them; the agreement's rules check them.
        if (WorkOrderToolSupport.Has(arguments, "hoursKind"))
        {
            if (!WorkOrderToolSupport.TryParseEnum<AgreementHoursKind>(WorkOrderToolSupport.Read(arguments, "hoursKind"), out var hoursKind))
            {
                return $"The hoursKind must be one of {WorkOrderToolSupport.Names<AgreementHoursKind>()}.";
            }

            form.HoursKind = hoursKind;
        }

        if (WorkOrderToolSupport.Has(arguments, "hours"))
        {
            if (WorkOrderToolSupport.ReadDecimal(arguments, "hours") is not { } hours)
            {
                return "The hours must be a number.";
            }

            form.Hours = hours;
        }

        if (WorkOrderToolSupport.Has(arguments, "roundingMinutes"))
        {
            if (WorkOrderToolSupport.ReadInt(arguments, "roundingMinutes") is not { } roundingMinutes)
            {
                return "The roundingMinutes must be 0, 6, 15 or 30.";
            }

            form.RoundingMinutes = roundingMinutes;
        }

        if (WorkOrderToolSupport.Has(arguments, "schedules"))
        {
            if (WorkOrderToolSupport.ReadObjects(arguments, "schedules") is not { } schedules)
            {
                return "The schedules must be a list of objects with a workOrderTypeId and a rule.";
            }

            var types = services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>();
            var rows = new List<ScheduleEditModel>();

            foreach (var schedule in schedules)
            {
                // A schedule with the ID of a saved one starts from it, so only the properties given change.
                var id = WorkOrderToolSupport.GetText(schedule, "id");
                var current = id is null ? null : form.Terms.Schedules.FirstOrDefault(row => row.Id == id);

                if (id is not null && current is null)
                {
                    return $"The agreement has no schedule with the ID '{id}'. Leave the id out to add a schedule.";
                }

                // Every rule is given as a custom rule; the editor's presets are such rules too.
                var row = new ScheduleEditModel
                {
                    Id = id,
                    Name = current?.Name,
                    TypeId = current?.TypeId,
                    Rule = string.Empty,
                    CustomRule = current?.CustomRule,
                    LeadDays = current?.LeadDays ?? 14,
                    AssetIds = [.. current?.AssetIds ?? []],
                };

                if (WorkOrderToolSupport.HasProperty(schedule, "workOrderTypeId") || current is null)
                {
                    var typeId = WorkOrderToolSupport.GetText(schedule, "workOrderTypeId");

                    if (typeId is null || await types.FindByIdAsync(typeId, cancellationToken) is null)
                    {
                        return $"No work order type has the ID '{typeId}'. Use listWorkOrderSettings with kind types.";
                    }

                    row.TypeId = typeId;
                }

                if (WorkOrderToolSupport.HasProperty(schedule, "rule") || current is null)
                {
                    row.CustomRule = WorkOrderToolSupport.GetText(schedule, "rule");
                }

                if (WorkOrderToolSupport.HasProperty(schedule, "name"))
                {
                    row.Name = WorkOrderToolSupport.GetText(schedule, "name");
                }

                if (WorkOrderToolSupport.HasProperty(schedule, "leadDays"))
                {
                    if (WorkOrderToolSupport.GetInt(schedule, "leadDays") is not { } leadDays)
                    {
                        return "Each schedule's leadDays must be a whole number of days.";
                    }

                    row.LeadDays = leadDays;
                }

                if (WorkOrderToolSupport.GetList(schedule, "assetIds") is { } scheduleAssetIds)
                {
                    row.AssetIds = [.. scheduleAssetIds];
                }

                rows.Add(row);
            }

            form.Terms.Schedules = rows;
        }

        return null;
    }

    private static string Overlay(JsonElement address, string name, string current)
        => WorkOrderToolSupport.HasProperty(address, name) ? WorkOrderToolSupport.GetRaw(address, name) : current;
}
