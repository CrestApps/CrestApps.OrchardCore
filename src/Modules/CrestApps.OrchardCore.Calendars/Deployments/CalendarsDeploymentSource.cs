using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Calendars.Deployments;

internal sealed class CalendarsDeploymentSource : DeploymentSourceBase<CalendarsDeploymentStep>
{
    private const int MaxEventsPerCalendar = 10_000;
    private const int PageSize = 500;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICalendarService _calendarService;
    private readonly ICalendarEventStore _eventStore;

    public CalendarsDeploymentSource(
        ICalendarService calendarService,
        ICalendarEventStore eventStore)
    {
        _calendarService = calendarService;
        _eventStore = eventStore;
    }

    protected override async Task ProcessAsync(CalendarsDeploymentStep step, DeploymentPlanResult result)
    {
        var calendars = new JsonArray();

        foreach (var calendar in await _calendarService.ListAsync(new CalendarListQuery { Limit = 1000 }))
        {
            var node = JsonSerializer.SerializeToNode(calendar, _jsonOptions).AsObject();

            node.Remove("FeedToken");
            node.Remove("BookingVersion");

            if (step.IncludeEvents)
            {
                var events = new JsonArray();

                for (var skip = 0; skip < MaxEventsPerCalendar; skip += PageSize)
                {
                    var page = await _eventStore.ListByCalendarAsync(calendar.ItemId, skip, PageSize);

                    foreach (var calendarEvent in page)
                    {
                        events.Add(JsonSerializer.SerializeToNode(calendarEvent, _jsonOptions));
                    }

                    if (page.Count < PageSize)
                    {
                        break;
                    }
                }

                node["Events"] = events;
            }

            calendars.Add(node);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Calendars"] = calendars,
        });
    }
}
