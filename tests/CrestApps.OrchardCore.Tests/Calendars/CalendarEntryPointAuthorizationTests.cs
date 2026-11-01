using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.AI;
using CrestApps.OrchardCore.Calendars.Api;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// The calendars API and AI tools ask the same questions as the calendar screens: the right to use calendars, then the
/// person's access to the calendar the event is on.
/// </summary>
public sealed class CalendarEntryPointAuthorizationTests
{
    private static readonly string _access = CalendarPermissions.AccessCalendars.Name;

    // The harness clock says 12:00 UTC on 2 November 2026.
    private static readonly DateTime _start = new(2026, 11, 4, 10, 0, 0);

    [Fact]
    public async Task Api_Lists_RefuseSomeoneWhoMayNotUseCalendars()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "John", c => c.OwnerId = "john");

        // Act
        var strangerCalendars = await ListCalendarsAsync(harness, CalendarTestHarness.User("stranger"));
        var strangerEvents = await ListEventsAsync(harness, CalendarTestHarness.User("stranger"));
        var johnCalendars = await ListCalendarsAsync(harness, CalendarTestHarness.User("john", _access));
        var johnEvents = await ListEventsAsync(harness, CalendarTestHarness.User("john", _access));

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(strangerCalendars));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(strangerEvents));
        Assert.Equal(StatusCodes.Status200OK, StatusOf(johnCalendars));
        Assert.Equal(StatusCodes.Status200OK, StatusOf(johnEvents));
    }

    [Fact]
    public async Task Api_ChangingAnEvent_NeedsWriteAccessToItsCalendar()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var personal = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "John", c => c.OwnerId = "john");
        var created = await harness.CreateEventAsync(personal.ItemId, _start, _start.AddHours(1));
        var jane = CalendarTestHarness.User("jane", _access);
        var john = CalendarTestHarness.User("john", _access);

        // Act (Jane sees John's personal calendar as busy time only)
        var janeCreates = await CallAsync(harness, jane, (services, context) => CalendarApiEndpoints.CreateEventAsync(Input(personal.ItemId), context, Calendars(services), Events(services), Editor(services)));
        var janeUpdates = await CallAsync(harness, jane, (services, context) => CalendarApiEndpoints.UpdateEventAsync(created.Value.ItemId, null, null, new CalendarEventInput { Title = "Taken over" }, context, Calendars(services), Events(services), Editor(services)));
        var janeCancels = await CallAsync(harness, jane, (services, context) => CalendarApiEndpoints.CancelEventAsync(created.Value.ItemId, null, null, context, Calendars(services), Events(services), Editor(services)));
        var janeDeletes = await CallAsync(harness, jane, (services, context) => CalendarApiEndpoints.DeleteEventAsync(created.Value.ItemId, context, Calendars(services), Events(services), Editor(services)));
        var unchanged = await FindEventAsync(harness, created.Value.ItemId);
        var johnCreates = await CallAsync(harness, john, (services, context) => CalendarApiEndpoints.CreateEventAsync(Input(personal.ItemId), context, Calendars(services), Events(services), Editor(services)));
        var johnUpdates = await CallAsync(harness, john, (services, context) => CalendarApiEndpoints.UpdateEventAsync(created.Value.ItemId, null, null, new CalendarEventInput { Title = "Moved" }, context, Calendars(services), Events(services), Editor(services)));
        var johnCancels = await CallAsync(harness, john, (services, context) => CalendarApiEndpoints.CancelEventAsync(created.Value.ItemId, null, null, context, Calendars(services), Events(services), Editor(services)));

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(janeCreates));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(janeUpdates));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(janeCancels));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(janeDeletes));
        Assert.Equal("Test", unchanged.Title);
        Assert.Equal(CalendarEventStatus.Confirmed, unchanged.Status);
        Assert.Equal(StatusCodes.Status201Created, StatusOf(johnCreates));
        Assert.Equal(StatusCodes.Status200OK, StatusOf(johnUpdates));
        Assert.Equal(StatusCodes.Status200OK, StatusOf(johnCancels));
        Assert.Equal(CalendarEventStatus.Cancelled, (await FindEventAsync(harness, created.Value.ItemId)).Status);
    }

    [Fact]
    public async Task Api_SharedWriteAccess_LetsSomeoneAddEvents_AndInvalidInputIsAValidationProblem()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var projects = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Projects", c =>
        {
            c.Visibility = CalendarVisibility.Private;
            c.Shares.Add(new CalendarShare { PrincipalKind = CalendarPrincipalKind.User, PrincipalId = "jane", Access = CalendarAccessLevel.Write });
        });
        var jane = CalendarTestHarness.User("jane", _access);

        // Act
        var added = await CallAsync(harness, jane, (services, context) => CalendarApiEndpoints.CreateEventAsync(Input(projects.ItemId), context, Calendars(services), Events(services), Editor(services)));
        var badTimes = await CallAsync(harness, jane, (services, context) => CalendarApiEndpoints.CreateEventAsync(new CalendarEventInput { CalendarId = projects.ItemId, Title = "Backwards", Start = "2026-11-04T11:00:00Z", End = "2026-11-04T10:00:00Z" }, context, Calendars(services), Events(services), Editor(services)));
        var noCalendar = await CallAsync(harness, jane, (services, context) => CalendarApiEndpoints.CreateEventAsync(Input("missing"), context, Calendars(services), Events(services), Editor(services)));

        // Assert
        Assert.Equal(StatusCodes.Status201Created, StatusOf(added));
        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(badTimes));
        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(noCalendar));
    }

    [Fact]
    public async Task Api_GetEvent_HidesAPrivateEventFromReaders_AndUnknownEventsAreNotFound()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var company = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Company", c => c.Visibility = CalendarVisibility.Public);
        var open = await harness.CreateEventAsync(company.ItemId, _start, _start.AddHours(1));
        var hidden = await harness.CreateEventAsync(company.ItemId, _start.AddHours(2), _start.AddHours(3), e => e.Visibility = CalendarEventVisibility.Private);
        var reader = CalendarTestHarness.User("reader", _access);
        var admin = CalendarTestHarness.User("admin", CalendarPermissions.ManageAllCalendars.Name);

        // Act
        var readsOpen = await GetEventAsync(harness, reader, open.Value.ItemId);
        var readsHidden = await GetEventAsync(harness, reader, hidden.Value.ItemId);
        var adminReadsHidden = await GetEventAsync(harness, admin, hidden.Value.ItemId);
        var unknown = await GetEventAsync(harness, admin, "missing");

        // Assert
        Assert.Equal(StatusCodes.Status200OK, StatusOf(readsOpen));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(readsHidden));
        Assert.Equal(StatusCodes.Status200OK, StatusOf(adminReadsHidden));
        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(unknown));
    }

    [Fact]
    public async Task Api_FreeBusy_ChecksThePermissionTheCalendarsAndTheRange()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var mine = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "John", c => c.OwnerId = "john");
        var secret = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Board", c => c.Visibility = CalendarVisibility.Private);
        var john = CalendarTestHarness.User("john", _access);
        var from = new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);

        // Act
        var stranger = await FreeBusyAsync(harness, CalendarTestHarness.User("stranger"), new CalendarFreeBusyInput { CalendarIds = [mine.ItemId], From = from, To = from.AddDays(1) });
        var privateCalendar = await FreeBusyAsync(harness, john, new CalendarFreeBusyInput { CalendarIds = [secret.ItemId], From = from, To = from.AddDays(1) });
        var noBody = await FreeBusyAsync(harness, john, null);
        var backwards = await FreeBusyAsync(harness, john, new CalendarFreeBusyInput { CalendarIds = [mine.ItemId], From = from, To = from.AddHours(-1) });
        var own = await FreeBusyAsync(harness, john, new CalendarFreeBusyInput { CalendarIds = [mine.ItemId], From = from, To = from.AddDays(1) });

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(stranger));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(privateCalendar));
        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(noBody));
        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(backwards));
        Assert.Equal(StatusCodes.Status200OK, StatusOf(own));
    }

    [Fact]
    public async Task AITools_RefuseSomeoneWhoMayNotUseCalendars_WithoutMakingThemACalendar()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var arguments = new Dictionary<string, object> { ["title"] = "Dentist", ["start"] = "2026-11-04T10:00:00-05:00", ["end"] = "2026-11-04T11:00:00-05:00" };

        // Act
        var strangerResult = await InvokeAsync(harness, CalendarTestHarness.User("stranger"), new CreateCalendarEventTool(), arguments);
        var strangerCalendars = await OwnedCalendarsAsync(harness, "stranger");
        var strangerFreeTime = await InvokeAsync(harness, CalendarTestHarness.User("stranger"), new FindFreeTimeTool(), new Dictionary<string, object>
        {
            ["from"] = "2026-11-04T09:00:00Z",
            ["to"] = "2026-11-04T17:00:00Z",
            ["durationMinutes"] = "30",
            ["userIds"] = "john",
        });
        var johnResult = await InvokeAsync(harness, CalendarTestHarness.User("john", _access), new CreateCalendarEventTool(), arguments);
        var johnCalendars = await OwnedCalendarsAsync(harness, "john");

        // Assert
        Assert.Equal("The user isn't allowed to use calendars.", strangerResult);
        Assert.Equal("The user isn't allowed to use calendars.", strangerFreeTime);
        Assert.Empty(strangerCalendars);
        Assert.Contains("Dentist", johnResult?.ToString(), StringComparison.Ordinal);
        Assert.Single(johnCalendars);
    }

    [Fact]
    public async Task AITools_CantChangeAnEventOnACalendarTheUserCantWrite()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var personal = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "John", c => c.OwnerId = "john");
        var created = await harness.CreateEventAsync(personal.ItemId, _start, _start.AddHours(1));
        var arguments = new Dictionary<string, object> { ["eventId"] = created.Value.ItemId };

        // Act
        var janeCancels = await InvokeAsync(harness, CalendarTestHarness.User("jane", _access), new CancelCalendarEventTool(), arguments);
        var stillOn = await FindEventAsync(harness, created.Value.ItemId);
        var janeMoves = await InvokeAsync(harness, CalendarTestHarness.User("jane", _access), new MoveCalendarEventTool(), new Dictionary<string, object> { ["eventId"] = created.Value.ItemId, ["start"] = "2026-11-05T10:00:00-05:00" });
        var johnCancels = await InvokeAsync(harness, CalendarTestHarness.User("john", _access), new CancelCalendarEventTool(), arguments);

        // Assert
        Assert.Equal("The user can't change that event.", janeCancels);
        Assert.Equal("The user can't change that event.", janeMoves);
        Assert.Equal(CalendarEventStatus.Confirmed, stillOn.Status);
        Assert.Equal("The event has been cancelled.", johnCancels);
        Assert.Equal(CalendarEventStatus.Cancelled, (await FindEventAsync(harness, created.Value.ItemId)).Status);
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return CalendarTestHarness.CreateAsync(services =>
        {
            services.AddScoped<CalendarEventEditor>();
            services.AddSingleton<TestHttpContextAccessor>();
            services.AddSingleton<IHttpContextAccessor>(provider => provider.GetRequiredService<TestHttpContextAccessor>());
        });
    }

    private static CalendarEventInput Input(string calendarId)
    {
        return new CalendarEventInput
        {
            CalendarId = calendarId,
            Title = "Review",
            Start = "2026-11-05T10:00:00-05:00",
            End = "2026-11-05T11:00:00-05:00",
        };
    }

    private static Task<IResult> ListCalendarsAsync(CalendarTestHarness harness, ClaimsPrincipal user)
    {
        return CallAsync(harness, user, (services, context) => CalendarApiEndpoints.ListCalendarsAsync(context, services.GetRequiredService<IAuthorizationService>(), services.GetRequiredService<ICalendarAccessService>()));
    }

    private static Task<IResult> ListEventsAsync(CalendarTestHarness harness, ClaimsPrincipal user)
    {
        return CallAsync(harness, user, (services, context) => CalendarApiEndpoints.ListEventsAsync(null, "2026-11-01T00:00:00Z", "2026-11-08T00:00:00Z", context, services.GetRequiredService<IAuthorizationService>(), services.GetRequiredService<ICalendarAccessService>(), services.GetRequiredService<ICalendarQueryService>()));
    }

    private static Task<IResult> GetEventAsync(CalendarTestHarness harness, ClaimsPrincipal user, string id)
    {
        return CallAsync(harness, user, (services, context) => CalendarApiEndpoints.GetEventAsync(id, context, Calendars(services), Events(services), Editor(services)));
    }

    private static Task<IResult> FreeBusyAsync(CalendarTestHarness harness, ClaimsPrincipal user, CalendarFreeBusyInput input)
    {
        return CallAsync(harness, user, (services, context) => CalendarApiEndpoints.FreeBusyAsync(input, context, services.GetRequiredService<IAuthorizationService>(), Calendars(services), services.GetRequiredService<ICalendarAccessService>(), services.GetRequiredService<ICalendarAvailabilityService>()));
    }

    private static Task<IResult> CallAsync(CalendarTestHarness harness, ClaimsPrincipal user, Func<IServiceProvider, HttpContext, Task<IResult>> call)
    {
        return harness.RunAsync(services => call(services, new DefaultHttpContext { User = user, RequestServices = services }));
    }

    private static Task<object> InvokeAsync(CalendarTestHarness harness, ClaimsPrincipal user, AIFunction tool, Dictionary<string, object> arguments)
    {
        return harness.RunAsync(async services =>
        {
            services.GetRequiredService<TestHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = user, RequestServices = services };

            return await tool.InvokeAsync(new AIFunctionArguments(arguments) { Services = services }, TestContext.Current.CancellationToken);
        });
    }

    private static Task<CalendarEvent> FindEventAsync(CalendarTestHarness harness, string id)
    {
        return harness.RunAsync(services => Events(services).FindByIdAsync(id).AsTask(), commit: false);
    }

    private static Task<IReadOnlyList<Calendar>> OwnedCalendarsAsync(CalendarTestHarness harness, string ownerId)
    {
        return harness.RunAsync(services =>
        {
            var query = new CalendarListQuery();

            query.OwnerIds.Add(ownerId);

            return Calendars(services).ListAsync(query);
        }, commit: false);
    }

    private static int StatusOf(IResult result)
    {
        return result switch
        {
            ForbidHttpResult => StatusCodes.Status403Forbidden,
            IStatusCodeHttpResult { StatusCode: { } code } => code,
            _ => throw new InvalidOperationException($"No status code on {result?.GetType().Name}."),
        };
    }

    private static ICalendarService Calendars(IServiceProvider services) => services.GetRequiredService<ICalendarService>();

    private static ICalendarEventManager Events(IServiceProvider services) => services.GetRequiredService<ICalendarEventManager>();

    private static CalendarEventEditor Editor(IServiceProvider services) => services.GetRequiredService<CalendarEventEditor>();

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; }
    }
}
