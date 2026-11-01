using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The open callbacks reserved for an agent show on the calendar through the calendar query service, read from a real
/// SQLite store: only open callbacks in the range, on the agent's personal calendar, read-only, only the viewer's own
/// unless the viewer may monitor the Contact Center, and with the number to call only for those allowed to see it.
/// </summary>
public sealed class CallbackCalendarEventSourceTests
{
    private static readonly string _access = CalendarPermissions.AccessCalendars.Name;
    private static readonly string _monitor = ContactCenterPermissions.MonitorContactCenter.Name;
    private static readonly DateTime _day = new(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Store_ListsOnlyOpenCallbacksOfTheAgentsAskedFor_InTheRange()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedAsync(harness);

        // Act
        var ann = await ListAsync(harness, ["agent-ann"]);
        var any = await ListAsync(harness, null);
        var none = await ListAsync(harness, []);

        // Assert (completed, unreserved and out-of-range callbacks are left out, in scheduled order)
        Assert.Equal(["ann-in-progress", "ann-pending"], ann.Select(callback => callback.ItemId));
        Assert.Equal(["ann-in-progress", "ann-pending", "bob-promoted"], any.Select(callback => callback.ItemId));
        Assert.Empty(none);
    }

    [Fact]
    public async Task Agent_SeesTheirOwnCallbacksOnTheirPersonalCalendar_ReadOnly()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedAsync(harness);
        var annCalendar = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "Ann", c => c.OwnerId = "ann");
        var bobCalendar = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "Bob", c => c.OwnerId = "bob");

        // Act
        var occurrences = await harness.GetOccurrencesAsync(Query([annCalendar.ItemId, bobCalendar.ItemId], CalendarTestHarness.User("ann", _access)));
        var callbacks = Callbacks(occurrences);

        // Assert (Bob's calendar is in the query, but Ann can't monitor, so only her own callbacks show)
        Assert.Equal(["ann-in-progress", "ann-pending"], callbacks.Select(occurrence => occurrence.Event.SourceId));

        var pending = callbacks[1];
        Assert.Equal(annCalendar.ItemId, pending.Event.CalendarId);
        Assert.False(pending.Event.Editable);
        Assert.Equal(CallbackCalendarEventSource.SourceName, pending.Event.Source);
        Assert.Equal(CallbackCalendarEventSource.EventType, pending.Event.EventType);
        Assert.Equal(CallbackCalendarEventSource.Color, pending.Event.Color);
        Assert.Equal("Call back +17025550101", pending.Event.Title);
        Assert.Equal(_day.AddHours(15), pending.StartUtc);
        Assert.Equal(_day.AddHours(15).AddMinutes(15), pending.EndUtc);
        Assert.False(pending.BusyOnly);
    }

    [Fact]
    public async Task Supervisor_SeesEveryAgentsCallbacks_WithTheNumber()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedAsync(harness);
        var annCalendar = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "Ann", c => c.OwnerId = "ann");
        var bobCalendar = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "Bob", c => c.OwnerId = "bob");
        var supervisor = CalendarTestHarness.User("sue", _access, _monitor);

        // Act
        var personal = Callbacks(await harness.GetOccurrencesAsync(Query([annCalendar.ItemId, bobCalendar.ItemId], supervisor)));
        var everyone = Callbacks(await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            AllCalendars = true,
            FromUtc = _day,
            ToUtc = _day.AddDays(1),
            Viewer = supervisor,
            IncludeEventSources = true,
        }));

        // Assert
        Assert.Equal(["ann-in-progress", "ann-pending", "bob-promoted"], personal.Select(occurrence => occurrence.Event.SourceId));
        Assert.Equal(bobCalendar.ItemId, personal[2].Event.CalendarId);
        Assert.Equal("Call back +17025550102", personal[2].Event.Title);
        Assert.Equal(["ann-in-progress", "ann-pending", "bob-promoted"], everyone.Select(occurrence => occurrence.Event.SourceId));
    }

    [Fact]
    public async Task Callbacks_ShowOnlyForTheAgentsTheQueryIsAbout()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedAsync(harness);
        var room = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Company", c => c.Visibility = CalendarVisibility.Public);
        var supervisor = CalendarTestHarness.User("sue", _access, _monitor);

        // Act
        var company = Callbacks(await harness.GetOccurrencesAsync(Query([room.ItemId], supervisor)));
        var invited = Callbacks(await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            AttendeeUserIds = ["bob"],
            FromUtc = _day,
            ToUtc = _day.AddDays(1),
            Viewer = supervisor,
            IncludeEventSources = true,
        }));
        var notAsked = Callbacks(await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            AttendeeUserIds = ["bob"],
            FromUtc = _day,
            ToUtc = _day.AddDays(1),
            Viewer = supervisor,
        }));
        var otherSource = Callbacks(await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            AttendeeUserIds = ["bob"],
            FromUtc = _day,
            ToUtc = _day.AddDays(1),
            Viewer = supervisor,
            IncludeEventSources = true,
            Sources = ["Scheduling"],
        }));

        // Assert (a calendar nobody owns has no agent's callbacks; an attendee's callbacks have no calendar of their own)
        Assert.Empty(company);
        var bob = Assert.Single(invited);
        Assert.Equal("bob-promoted", bob.Event.SourceId);
        Assert.Null(bob.Event.CalendarId);
        Assert.Empty(notAsked);
        Assert.Empty(otherSource);
    }

    [Fact]
    public async Task WithoutAViewer_TheNumberIsHidden_AndAViewerWithoutAnAgentProfileSeesNothing()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedAsync(harness);
        var annCalendar = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "Ann", c => c.OwnerId = "ann");

        // Act
        var trusted = Callbacks(await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            CalendarIds = [annCalendar.ItemId],
            FromUtc = _day,
            ToUtc = _day.AddDays(1),
            IncludeEventSources = true,
        }));
        var stranger = Callbacks(await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            AttendeeUserIds = ["ann"],
            AllCalendars = true,
            FromUtc = _day,
            ToUtc = _day.AddDays(1),
            Viewer = CalendarTestHarness.User("carl", _access),
            IncludeEventSources = true,
        }));

        // Assert
        Assert.Equal(["Callback", "Callback"], trusted.Select(occurrence => occurrence.Event.Title));
        Assert.DoesNotContain(trusted, occurrence => occurrence.Event.Title.Contains("+1", StringComparison.Ordinal));
        Assert.Empty(stranger);
    }

    [Fact]
    public async Task IndexProvider_LeavesAnAgentIdNoAgentCanHaveOutOfTheIndex()
    {
        // Arrange
        await using var harness = await CreateAsync();

        await SaveAsync(harness, new CallbackRequest
        {
            ItemId = "odd-agent",
            AgentId = new string('x', 40),
            Destination = "+17025550109",
            Status = CallbackRequestStatus.Pending,
            ScheduledUtc = _day.AddHours(9),
        });

        // Act
        var any = await ListAsync(harness, null);

        // Assert
        Assert.Empty(any);
    }

    [Fact]
    public void DialerCalendarStartup_RegistersTheEventSource()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new DialerCalendarStartup().ConfigureServices(services);

        // Assert
        Assert.Contains(services, service => service.ServiceType == typeof(ICalendarEventSource) && service.ImplementationType == typeof(CallbackCalendarEventSource));
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        var agents = new Dictionary<string, AgentProfile>(StringComparer.Ordinal)
        {
            ["ann"] = new AgentProfile { ItemId = "agent-ann", UserId = "ann" },
            ["bob"] = new AgentProfile { ItemId = "agent-bob", UserId = "bob" },
        };
        var agentStore = new Mock<IAgentProfileStore>();

        agentStore
            .Setup(store => store.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) => agents.GetValueOrDefault(userId));
        agentStore
            .Setup(store => store.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) => agents.Values.Where(agent => ids.Contains(agent.ItemId)).ToArray());

        return CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddScoped<ICallbackRequestStore, CallbackRequestStore>();
                services.AddSingleton(agentStore.Object);
                new DialerCalendarStartup().ConfigureServices(services);
            },
            CreateCallbackTableAsync,
            [new CallbackRequestIndexProvider()],
            [ContactCenterStorage.CollectionName]);
    }

    private static async Task CreateCallbackTableAsync(ISchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<CallbackRequestIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("ScheduledUtc")
            .Column<DateTime>("LeaseExpiresUtc", column => column.Nullable())
            .Column<DateTime>("ModifiedUtc", column => column.Nullable())
            .Column<string>("AgentId", column => column.Nullable().WithLength(26)),
            collection: ContactCenterStorage.CollectionName);
    }

    private static async Task SeedAsync(CalendarTestHarness harness)
    {
        await SaveAsync(harness, Callback("ann-pending", "agent-ann", CallbackRequestStatus.Pending, _day.AddHours(15), "+17025550101"));
        await SaveAsync(harness, Callback("ann-in-progress", "agent-ann", CallbackRequestStatus.InProgress, _day.AddMinutes(-10), "+17025550103"));
        await SaveAsync(harness, Callback("ann-completed", "agent-ann", CallbackRequestStatus.Completed, _day.AddHours(11), "+17025550104"));
        await SaveAsync(harness, Callback("ann-next-week", "agent-ann", CallbackRequestStatus.Pending, _day.AddDays(7), "+17025550105"));
        await SaveAsync(harness, Callback("ann-long-ago", "agent-ann", CallbackRequestStatus.Pending, _day.AddMinutes(-30), "+17025550106"));
        await SaveAsync(harness, Callback("bob-promoted", "agent-bob", CallbackRequestStatus.Scheduled, _day.AddHours(16), "+17025550102"));
        await SaveAsync(harness, Callback("queue", null, CallbackRequestStatus.Pending, _day.AddHours(10), "+17025550107"));
    }

    private static CallbackRequest Callback(string itemId, string agentId, CallbackRequestStatus status, DateTime scheduledUtc, string destination)
    {
        return new CallbackRequest
        {
            ItemId = itemId,
            AgentId = agentId,
            Destination = destination,
            Status = status,
            ScheduledUtc = scheduledUtc,
            RequestedUtc = scheduledUtc.AddDays(-1),
            CreatedUtc = scheduledUtc.AddDays(-1),
        };
    }

    private static Task SaveAsync(CalendarTestHarness harness, CallbackRequest callback)
    {
        return harness.RunAsync(services => services.GetRequiredService<ISession>().SaveAsync(callback, false, ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken));
    }

    private static Task<IReadOnlyCollection<CallbackRequest>> ListAsync(CalendarTestHarness harness, string[] agentIds)
    {
        return harness.RunAsync(
            services => services.GetRequiredService<ICallbackRequestStore>().ListOpenForAgentsAsync(agentIds, _day.AddMinutes(-15), _day.AddDays(1), 100, TestContext.Current.CancellationToken),
            commit: false);
    }

    private static CalendarOccurrenceQuery Query(IList<string> calendarIds, ClaimsPrincipal viewer)
    {
        return new CalendarOccurrenceQuery
        {
            CalendarIds = calendarIds,
            FromUtc = _day,
            ToUtc = _day.AddDays(1),
            Viewer = viewer,
            IncludeEventSources = true,
        };
    }

    private static List<CalendarOccurrence> Callbacks(IEnumerable<CalendarOccurrence> occurrences)
    {
        return occurrences
            .Where(occurrence => occurrence.Event.Source == CallbackCalendarEventSource.SourceName)
            .OrderBy(occurrence => occurrence.StartUtc)
            .ToList();
    }
}
