using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Drivers;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Scheduling.AI;

/// <summary>
/// Creates or updates an appointment type through the rules of its editor. Only the fields given change.
/// </summary>
internal sealed class SaveAppointmentTypeTool : SchedulingAdminToolBase
{
    public const string ToolName = "saveAppointmentType";

    private static readonly (string Argument, Action<AppointmentTypeViewModel, int> Set)[] _numbers =
    [
        ("durationMinutes", (model, value) => model.DurationMinutes = value),
        ("slotIntervalMinutes", (model, value) => model.SlotIntervalMinutes = value),
        ("bufferBeforeMinutes", (model, value) => model.BufferBeforeMinutes = value),
        ("bufferAfterMinutes", (model, value) => model.BufferAfterMinutes = value),
        ("minimumNoticeMinutes", (model, value) => model.MinimumNoticeMinutes = value),
        ("horizonDays", (model, value) => model.HorizonDays = value),
        ("maxPerDay", (model, value) => model.MaxPerDay = value),
        ("maxPerWeek", (model, value) => model.MaxPerWeek = value),
        ("maxUpcomingPerCustomer", (model, value) => model.MaxUpcomingPerCustomer = value),
        ("cancelNoticeMinutes", (model, value) => model.CancelNoticeMinutes = value),
        ("rescheduleNoticeMinutes", (model, value) => model.RescheduleNoticeMinutes = value),
        ("capacity", (model, value) => model.Capacity = value),
        ("holdMinutes", (model, value) => model.HoldMinutes = value),
    ];

    private static readonly (string Argument, Action<AppointmentTypeViewModel, bool> Set)[] _switches =
    [
        ("requiresConfirmation", (model, value) => model.RequiresConfirmation = value),
        ("allowSelfService", (model, value) => model.AllowSelfService = value),
        ("enabled", (model, value) => model.Enabled = value),
    ];

    private static readonly (string Argument, Action<AppointmentTypeViewModel, string> Set)[] _texts =
    [
        ("name", (model, value) => model.Name = value),
        ("description", (model, value) => model.Description = value),
        ("slug", (model, value) => model.Slug = value),
        ("location", (model, value) => model.Location = value),
        ("color", (model, value) => model.Color = value),
    ];

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "appointmentTypeId": { "type": "string", "description": "The ID of the type to update. Leave it out to create a type." },
        "name": { "type": "string", "description": "The name customers see. Required for a new type." },
        "description": { "type": "string", "description": "A short description." },
        "slug": { "type": "string", "description": "The address of its public booking page, such as initial-consultation. Empty text removes it." },
        "durationMinutes": { "type": "integer", "minimum": 5, "description": "How long an appointment lasts, in minutes, at least 5." },
        "slotIntervalMinutes": { "type": "integer", "minimum": 5, "description": "The time between possible start times, in minutes, at least 5." },
        "bufferBeforeMinutes": { "type": "integer", "minimum": 0, "description": "Free time kept before each appointment, in minutes." },
        "bufferAfterMinutes": { "type": "integer", "minimum": 0, "description": "Free time kept after each appointment, in minutes." },
        "minimumNoticeMinutes": { "type": "integer", "minimum": 0, "description": "How long before the start a customer can still book, in minutes." },
        "horizonDays": { "type": "integer", "minimum": 1, "maximum": 730, "description": "How many days ahead it can be booked." },
        "maxPerDay": { "type": "integer", "minimum": 0, "description": "The most appointments of this type a resource takes in a day; 0 for no limit." },
        "maxPerWeek": { "type": "integer", "minimum": 0, "description": "The most appointments of this type a resource takes in a week; 0 for no limit." },
        "maxUpcomingPerCustomer": { "type": "integer", "minimum": 0, "description": "How many upcoming appointments of this type one customer may hold; 0 for no limit." },
        "cancelNoticeMinutes": { "type": "integer", "minimum": 0, "description": "How long before the start a customer can still cancel, in minutes." },
        "rescheduleNoticeMinutes": { "type": "integer", "minimum": 0, "description": "How long before the start a customer can still move it, in minutes." },
        "capacity": { "type": "integer", "minimum": 1, "description": "How many customers one appointment takes; above 1 it is a group session." },
        "requiresConfirmation": { "type": "boolean", "description": "Whether a booking waits for staff to confirm it." },
        "holdMinutes": { "type": "integer", "minimum": 1, "maximum": 1440, "description": "How long a time is held while a customer books, in minutes." },
        "allowSelfService": { "type": "boolean", "description": "Whether customers can book it themselves, on the booking page and through the customer AI tools." },
        "location": { "type": "string", "description": "Where it takes place." },
        "color": { "type": "string", "description": "The color on the calendar, such as #0b7285." },
        "enabled": { "type": "boolean", "description": "Whether it can be booked." },
        "resourceGroups": {
          "type": "array",
          "description": "Replaces the groups of resources the type needs; one resource from each group is booked (all of them for AllTogether). A resource can be in only one group. Leave it out to keep the groups.",
          "items": {
            "type": "object",
            "properties": {
              "name": { "type": "string", "description": "The group's name, such as Staff or Room." },
              "assignment": { "type": "string", "enum": ["AnyAvailable", "RoundRobin", "AllTogether"], "description": "AnyAvailable takes any free resource, RoundRobin shares bookings out, AllTogether books every resource in the group." },
              "resourceIds": { "type": "array", "items": { "type": "string" }, "description": "The IDs of the group's resources, from listRecords with kind schedulingResource." },
              "weights": {
                "type": "array",
                "description": "For RoundRobin only: resources that take a bigger share, from 2 to 100 (the default is 1).",
                "items": {
                  "type": "object",
                  "properties": {
                    "resourceId": { "type": "string", "description": "The resource's ID." },
                    "weight": { "type": "integer", "minimum": 1, "maximum": 100, "description": "Its share." }
                  },
                  "required": ["resourceId", "weight"],
                  "additionalProperties": false
                }
              }
            },
            "required": ["resourceIds"],
            "additionalProperties": false
          }
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Creates or updates an appointment type: name, slug, duration, slot interval, buffers, notice, horizon, daily and weekly limits, capacity, confirmation, hold time, self-service, location, color, enabled and the groups of resources it needs. A resource can be in only one group. Only the fields given change.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => SchedulingPermissions.ManageScheduling;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var typeStore = services.GetRequiredService<AppointmentTypeStore>();
        var id = SchedulingToolSupport.Read(arguments, "appointmentTypeId");
        var isNew = id is null;
        var type = isNew ? new AppointmentType() : await typeStore.FindByIdAsync(id, cancellationToken);

        if (type is null)
        {
            return SchedulingToolResults.AppointmentTypeNotFound;
        }

        var model = new AppointmentTypeViewModel();

        AppointmentTypeEditorRules.Fill(model, type);

        foreach (var (argument, set) in _texts)
        {
            if (SchedulingToolSupport.Has(arguments, argument))
            {
                set(model, SchedulingToolSupport.ReadRaw(arguments, argument));
            }
        }

        foreach (var (argument, set) in _numbers)
        {
            if (!SchedulingToolSupport.Has(arguments, argument))
            {
                continue;
            }

            if (SchedulingToolSupport.ReadInt(arguments, argument) is not { } number)
            {
                return $"Nothing was saved: {argument} must be a whole number.";
            }

            set(model, number);
        }

        foreach (var (argument, set) in _switches)
        {
            if (!SchedulingToolSupport.Has(arguments, argument))
            {
                continue;
            }

            if (SchedulingToolSupport.ReadBool(arguments, argument) is not { } value)
            {
                return $"Nothing was saved: {argument} must be true or false.";
            }

            set(model, value);
        }

        if (SchedulingToolSupport.Has(arguments, "resourceGroups"))
        {
            var (groups, error) = await ReadGroupsAsync(arguments, services, cancellationToken);

            if (error is not null)
            {
                return "Nothing was saved: " + error;
            }

            model.Requirements = groups;
        }

        var localizer = services.GetRequiredService<IStringLocalizer<AppointmentTypeFieldsDisplayDriver>>();

        // The rules change the type they check even when they refuse it, so they check a stand-in first: the type itself is
        // changed only once everything is accepted. The stand-in keeps the ID, so the type's own address is not taken as used.
        var errors = await AppointmentTypeEditorRules.ApplyAsync(new AppointmentType { ItemId = type.ItemId }, model, typeStore, localizer);

        if (errors.Count > 0)
        {
            return "Nothing was saved: " + string.Join(" ", errors.Select(error => error.Value.Value));
        }

        await AppointmentTypeEditorRules.ApplyAsync(type, model, typeStore, localizer);

        if (isNew)
        {
            await typeStore.CreateAsync(type, cancellationToken);
        }
        else
        {
            await typeStore.UpdateAsync(type, cancellationToken);
        }

        return SchedulingToolSupport.Json(new
        {
            created = isNew,
            appointmentType = await GetAppointmentTypeTool.DescribeAsync(services, type, cancellationToken),
        });
    }

    private static async Task<(List<AppointmentResourceRequirementViewModel> Groups, string Error)> ReadGroupsAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var items = SchedulingToolSupport.ReadObjects(arguments, "resourceGroups");

        if (items is null)
        {
            return (null, "resourceGroups must be a list of groups, each with its resourceIds.");
        }

        var groups = new List<AppointmentResourceRequirementViewModel>();

        foreach (var item in items)
        {
            var resourceIds = item.TryGetProperty("resourceIds", out var ids) && ids.ValueKind == JsonValueKind.Array
                ? ids.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())).Select(value => value.GetString().Trim()).ToArray()
                : [];

            if (resourceIds.Length == 0)
            {
                return (null, "Every resource group needs at least one resource ID.");
            }

            var assignment = SchedulingToolSupport.GetText(item, "assignment") ?? nameof(AppointmentAssignment.AnyAvailable);

            if (!Enum.TryParse<AppointmentAssignment>(assignment, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return (null, "A group's assignment must be AnyAvailable, RoundRobin or AllTogether.");
            }

            var weights = new Dictionary<string, int>(StringComparer.Ordinal);

            if (item.TryGetProperty("weights", out var weightList) && weightList.ValueKind == JsonValueKind.Array)
            {
                foreach (var weight in weightList.EnumerateArray())
                {
                    var resourceId = SchedulingToolSupport.GetText(weight, "resourceId");

                    if (resourceId is null || !weight.TryGetProperty("weight", out var share) || !share.TryGetInt32(out var value) || !resourceIds.Contains(resourceId, StringComparer.Ordinal))
                    {
                        return (null, "Each weight needs the ID of a resource in its group and a whole number.");
                    }

                    weights[resourceId] = value;
                }
            }

            groups.Add(new AppointmentResourceRequirementViewModel
            {
                Name = SchedulingToolSupport.GetText(item, "name"),
                ResourceIds = resourceIds,
                Assignment = parsed.ToString(),
                Weights = weights,
            });
        }

        // The editor offers only existing resources; a tool is told which IDs are unknown instead of saving them.
        var wanted = groups.SelectMany(group => group.ResourceIds).Distinct(StringComparer.Ordinal).ToList();
        var known = (await services.GetRequiredService<SchedulingResourceStore>().GetAsync(wanted, cancellationToken)).Select(resource => resource.ItemId).ToHashSet(StringComparer.Ordinal);
        var unknown = wanted.Where(resourceId => !known.Contains(resourceId)).ToList();

        return unknown.Count > 0
            ? (null, $"There is no scheduling resource with the ID {string.Join(", ", unknown)}. Use listRecords with kind schedulingResource to find the IDs.")
            : (groups, null);
    }
}
