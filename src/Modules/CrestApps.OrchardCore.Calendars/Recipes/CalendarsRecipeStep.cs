using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Calendars.Recipes;

/// <summary>
/// Imports calendars and their events: <c>{ "name": "Calendars", "Calendars": [ { ...calendar, "Events": [ ... ] } ] }</c>.
/// Calendars and events are matched by id, so running the step again updates them.
/// </summary>
internal sealed class CalendarsRecipeStep : NamedRecipeStepHandler
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICalendarService _calendarService;
    private readonly ICalendarEventManager _eventManager;

    public CalendarsRecipeStep(
        ICalendarService calendarService,
        ICalendarEventManager eventManager)
        : base(CalendarsConstants.RecipeStepName)
    {
        _calendarService = calendarService;
        _eventManager = eventManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var options = new CalendarChangeOptions { Actor = "recipe", SkipConflictCheck = true, SendInvitations = false };

        foreach (var node in (context.Step["Calendars"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var events = node["Events"] as JsonArray;
            var data = node.DeepClone().AsObject();

            data.Remove("Events");

            var incoming = data.Deserialize<Calendar>(_jsonOptions);

            if (incoming is null || string.IsNullOrWhiteSpace(incoming.Type))
            {
                context.Errors.Add("A calendar in the Calendars step has no type.");

                continue;
            }

            var calendar = string.IsNullOrEmpty(incoming.ItemId) ? null : await _calendarService.FindByIdAsync(incoming.ItemId);
            CalendarResult<Calendar> result;

            if (calendar is null)
            {
                var created = await _calendarService.NewAsync(incoming.Type);

                if (created is null)
                {
                    context.Errors.Add($"The calendar type '{incoming.Type}' of '{incoming.Name}' is not enabled.");

                    continue;
                }

                Copy(incoming, created);
                created.ItemId = string.IsNullOrEmpty(incoming.ItemId) ? created.ItemId : incoming.ItemId;
                result = await _calendarService.CreateAsync(created, options);
            }
            else
            {
                Copy(incoming, calendar);
                result = await _calendarService.UpdateAsync(calendar, options);
            }

            if (!result.Succeeded)
            {
                context.Errors.Add($"{incoming.Name}: {string.Join(" ", result.Errors.Select(error => error.Message))}");

                continue;
            }

            foreach (var eventNode in (events ?? []).OfType<JsonObject>())
            {
                var incomingEvent = eventNode.Deserialize<CalendarEvent>(_jsonOptions);

                if (incomingEvent is null)
                {
                    continue;
                }

                incomingEvent.CalendarId = result.Value.ItemId;

                var existing = string.IsNullOrEmpty(incomingEvent.ItemId) ? null : await _eventManager.FindByIdAsync(incomingEvent.ItemId);
                var eventResult = existing is null
                    ? await _eventManager.CreateAsync(incomingEvent, options)
                    : await _eventManager.UpdateAsync(existing, target => CalendarEventEditor.CopyEditableFields(incomingEvent, target, includeRecurrence: true), options);

                if (!eventResult.Succeeded)
                {
                    context.Errors.Add($"{incomingEvent.Title}: {string.Join(" ", eventResult.Errors.Select(error => error.Message))}");
                }
            }
        }
    }

    private static void Copy(Calendar source, Calendar target)
    {
        target.Name = source.Name;
        target.Description = source.Description;
        target.Color = source.Color ?? target.Color;
        target.TimeZoneId = source.TimeZoneId ?? target.TimeZoneId;
        target.OwnerId = source.OwnerId ?? target.OwnerId;
        target.Visibility = source.Visibility;
        target.Shares = source.Shares ?? [];
        target.ConflictPolicy = source.ConflictPolicy;
        target.Capacity = source.Capacity < 1 ? target.Capacity : source.Capacity;
        target.Enabled = source.Enabled;
        target.Source = source.Source;
        target.SourceId = source.SourceId;

        foreach (var property in source.Properties)
        {
            target.Properties[property.Key] = property.Value;
        }
    }
}
