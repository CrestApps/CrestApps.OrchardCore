using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Migrations;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Reports.DataSources;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// The calendars data source reads real calendars and events from a SQLite store through the calendar services, so the
/// security boundary (only calendars the principal can read, private details hidden), the fields, and the push-down of
/// dates, join keys and statuses are exercised end to end.
/// </summary>
public sealed class CalendarReportDataSourceTests
{
    private static readonly string _access = CalendarPermissions.AccessCalendars.Name;
    private static readonly DateTime _day = new(2026, 11, 3);

    [Fact]
    public async Task WithoutPermissionOrUser_NothingIsExposed()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedAsync(harness);
        var stranger = CalendarTestHarness.User("stranger");

        // Act
        var (dataSets, schema, rows) = await RunAsync(harness, async source =>
        {
            var context = new DataSourceContext { User = stranger };

            return (
                await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken),
                await source.GetSchemaAsync(CalendarReportDataSets.Events, context, TestContext.Current.CancellationToken),
                await ReportDesignerTestServices.ReadAsync(source, Query(CalendarReportDataSets.Events, null), TestContext.Current.CancellationToken));
        });

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(rows.Rows);
    }

    [Fact]
    public async Task Calendars_OnlyThoseThePrincipalCanRead()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var seed = await SeedAsync(harness);

        // Act
        var alice = await ReadAsync(harness, CalendarReportDataSets.Calendars, CalendarTestHarness.User("alice", _access));
        var auditor = await ReadAsync(harness, CalendarReportDataSets.Calendars, CalendarTestHarness.User("auditor", CalendarPermissions.ViewAllCalendars.Name));

        // Assert (Bob's personal calendar only shows Alice busy time, and the hidden calendar nothing)
        Assert.Equal(
            new[] { seed.Alice, seed.Company }.Order(),
            alice.Select(row => (string)row[CalendarReportDataSets.ItemIdField]).Order());
        Assert.Equal(
            new[] { seed.Alice, seed.Bob, seed.Company, seed.Hidden }.Order(),
            auditor.Select(row => (string)row[CalendarReportDataSets.ItemIdField]).Order());

        var personal = alice.Single(row => (string)row[CalendarReportDataSets.ItemIdField] == seed.Alice);
        Assert.Equal(CalendarTestHarness.Personal, personal["Type"]);
        Assert.Equal("User", personal["OwnerKind"]);
        Assert.Equal("alice", personal["OwnerUserId"]);
        Assert.Equal(true, personal["Enabled"]);
        Assert.Equal(0L, personal["ShareCount"]);

        var company = alice.Single(row => (string)row[CalendarReportDataSets.ItemIdField] == seed.Company);
        Assert.Equal("Public", company["Visibility"]);
        Assert.Null(company["OwnerUserId"]);
        Assert.DoesNotContain(alice.SelectMany(row => row.Keys), key => key is "FeedToken" or "Shares");
    }

    [Fact]
    public async Task Events_OnlyOnReadableCalendars_AndPrivateDetailsHidden()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var seed = await SeedAsync(harness);

        // Act
        var alice = await ReadAsync(harness, CalendarReportDataSets.Events, CalendarTestHarness.User("alice", _access));
        var auditor = await ReadAsync(harness, CalendarReportDataSets.Events, CalendarTestHarness.User("auditor", CalendarPermissions.ViewAllCalendars.Name));
        var admin = await ReadAsync(harness, CalendarReportDataSets.Events, CalendarTestHarness.User("admin", CalendarPermissions.ManageAllCalendars.Name));

        // Assert
        Assert.Equal(
            new[] { seed.AliceMeeting, seed.AlicePrivate, seed.CompanyAllHands, seed.CompanyPrivate, seed.CompanySeries }.Order(),
            alice.Select(row => (string)row[CalendarReportDataSets.ItemIdField]).Order());
        Assert.Equal(7, auditor.Count);
        Assert.Equal(7, admin.Count);

        // Alice owns her calendar, so she sees her own private event.
        var own = Row(alice, seed.AlicePrivate);
        Assert.Equal("Dentist", own["Title"]);
        Assert.Equal(false, own["DetailsHidden"]);

        // She can only read the company calendar, so its private event is busy time.
        var hidden = Row(alice, seed.CompanyPrivate);
        Assert.Null(hidden["Title"]);
        Assert.Null(hidden["AttendeeCount"]);
        Assert.Equal(true, hidden["DetailsHidden"]);
        Assert.Equal(60L, hidden["DurationMinutes"]);
        Assert.DoesNotContain(alice.SelectMany(row => row.Values), value => value is string text && text.Contains("Secret", StringComparison.Ordinal));

        // Seeing every calendar is reading, not changing, so private details stay hidden; managing them shows them.
        Assert.Null(Row(auditor, seed.CompanyPrivate)["Title"]);
        Assert.Null(Row(auditor, seed.AlicePrivate)["Title"]);
        Assert.Equal("Secret merger", Row(admin, seed.CompanyPrivate)["Title"]);
    }

    [Fact]
    public async Task Events_ReturnTypedValues()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var seed = await SeedAsync(harness);

        // Act
        var rows = await ReadAsync(harness, CalendarReportDataSets.Events, CalendarTestHarness.User("alice", _access));

        // Assert
        var meeting = Row(rows, seed.AliceMeeting);
        Assert.Equal(seed.Alice, meeting[CalendarReportDataSets.CalendarIdField]);
        Assert.Equal(CalendarTestHarness.Personal, meeting["CalendarType"]);
        Assert.Equal("Planning", meeting["Title"]);
        Assert.Equal("Confirmed", meeting["Status"]);
        Assert.Equal("Busy", meeting["Transparency"]);
        Assert.Equal(90L, meeting["DurationMinutes"]);
        Assert.Equal(2L, meeting["AttendeeCount"]);
        Assert.Equal(false, meeting["AllDay"]);
        Assert.Equal(false, meeting["IsRecurring"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(meeting[CalendarEventsReportDataSet.StartUtcField]).Kind);
        Assert.Equal(((DateTime)meeting[CalendarEventsReportDataSet.StartUtcField]).AddMinutes(90), meeting["EndUtc"]);

        var series = Row(rows, seed.CompanySeries);
        Assert.Equal(true, series["IsRecurring"]);
        Assert.Equal(CalendarTestHarness.General, series["CalendarType"]);

        var allHands = Row(rows, seed.CompanyAllHands);
        Assert.Equal("Tentative", allHands["Status"]);
        Assert.Equal("Free", allHands["Transparency"]);
    }

    [Fact]
    public async Task Events_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var seed = await SeedAsync(harness);
        var auditor = CalendarTestHarness.User("auditor", CalendarPermissions.ViewAllCalendars.Name);
        var all = await ReadAsync(harness, CalendarReportDataSets.Events, auditor);
        var dayTwo = Utc(_day.AddDays(1));

        var dates = Query(CalendarReportDataSets.Events, auditor);
        dates.Conditions.Add(new DataCondition { Field = CalendarEventsReportDataSet.StartUtcField, Operator = DataFilterOperator.Between, Values = [dayTwo.AddHours(-12), dayTwo.AddHours(36)] });
        var calendars = Query(CalendarReportDataSets.Events, auditor);
        calendars.Conditions.Add(new DataCondition { Field = CalendarReportDataSets.CalendarIdField, Operator = DataFilterOperator.In, Values = [seed.Company], IsJoinKey = true });
        var status = Query(CalendarReportDataSets.Events, auditor);
        status.Conditions.Add(new DataCondition { Field = "Status", Operator = DataFilterOperator.In, Values = ["tentative"] });
        var none = Query(CalendarReportDataSets.Events, auditor);
        none.Conditions.Add(new DataCondition { Field = CalendarReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var dateRows = await ReadAsync(harness, dates);
        var calendarRows = await ReadAsync(harness, calendars);
        var statusRows = await ReadAsync(harness, status);
        var noRows = await ReadAsync(harness, none);

        // Assert
        Assert.Equal(
            all.Where(row => (DateTime)row[CalendarEventsReportDataSet.StartUtcField] >= dayTwo.AddHours(-12) && (DateTime)row[CalendarEventsReportDataSet.StartUtcField] <= dayTwo.AddHours(36)).Select(Id).Order(),
            dateRows.Select(Id).Order());
        Assert.NotEmpty(dateRows);
        Assert.Equal(
            new[] { seed.CompanyAllHands, seed.CompanyPrivate, seed.CompanySeries }.Order(),
            calendarRows.Select(Id).Order());
        Assert.Equal([seed.CompanyAllHands], statusRows.Select(Id));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Events_DeclineAggregationInTheDatabase()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedAsync(harness);

        // Act
        var result = await RunAsync(harness, source => ((IDataAggregateSource)source).AggregateAsync(
            new DataAggregateQuery
            {
                DataSet = CalendarReportDataSets.Events,
                Groups = [new DataAggregateGroup { Field = CalendarReportDataSets.CalendarIdField }],
                Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
                Context = new DataSourceContext { User = CalendarTestHarness.User("alice", _access) },
            },
            TestContext.Current.CancellationToken));

        // Assert (the grouping statement would read events on every calendar)
        Assert.Null(result);
    }

    [Fact]
    public async Task Schema_DeclaresKeysAndReferences()
    {
        // Arrange
        await using var harness = await CreateAsync();

        // Act
        var (calendars, events) = await RunAsync(harness, async source =>
        {
            var context = new DataSourceContext { User = CalendarTestHarness.User("alice", _access) };

            return (
                await source.GetSchemaAsync(CalendarReportDataSets.Calendars, context, TestContext.Current.CancellationToken),
                await source.GetSchemaAsync(CalendarReportDataSets.Events, context, TestContext.Current.CancellationToken));
        });

        // Assert
        Assert.Equal(CalendarReportDataSets.Source, CalendarReportDataSource.Name);
        Assert.True(calendars.FindField(CalendarReportDataSets.ItemIdField).IsKeyFilterable);
        Assert.Equal(ReportsConstants.UsersDataSource, Assert.Single(calendars.FindField("OwnerUserId").References).Source);
        Assert.Equal(CalendarEventsReportDataSet.StartUtcField, events.DataSet.DefaultDateField);
        Assert.True(events.FindField(CalendarReportDataSets.CalendarIdField).IsKeyFilterable);
        var calendar = Assert.Single(events.FindField(CalendarReportDataSets.CalendarIdField).References);
        Assert.Equal(CalendarReportDataSets.Calendars, calendar.DataSet);
        Assert.DoesNotContain(events.Fields, field => field.Name is "Description" or "Location" or "Attendees" or "ProviderMappings" or "Uid");
    }

    [Fact]
    public async Task ReportIndexMigration_CanRunAgain()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await using var session = harness.Store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migrations = new CalendarMigrations { SchemaBuilder = new SchemaBuilder(harness.Store.Configuration, transaction) };

        // Act
        var first = await migrations.UpdateFrom2Async();
        var second = await migrations.UpdateFrom2Async();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, first);
        Assert.Equal(3, second);
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return CalendarTestHarness.CreateAsync(services => new DataSourcesStartup().ConfigureServices(services));
    }

    private static async Task<Seed> SeedAsync(CalendarTestHarness harness)
    {
        var alice = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "Alice", c => c.OwnerId = "alice");
        var bob = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "Bob", c => c.OwnerId = "bob");
        var company = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Company", c => c.Visibility = CalendarVisibility.Public);
        var hidden = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Hidden", c => c.Visibility = CalendarVisibility.Private);

        var aliceMeeting = await harness.CreateEventAsync(alice.ItemId, _day.AddHours(9), _day.AddHours(10).AddMinutes(30), e =>
        {
            e.Title = "Planning";
            e.Attendees.Add(new CalendarAttendee { Email = "one@contoso.com", Name = "One" });
            e.Attendees.Add(new CalendarAttendee { Email = "two@contoso.com", Name = "Two" });
        });
        var alicePrivate = await harness.CreateEventAsync(alice.ItemId, _day.AddHours(13), _day.AddHours(14), e =>
        {
            e.Title = "Dentist";
            e.Visibility = CalendarEventVisibility.Private;
        });
        await harness.CreateEventAsync(bob.ItemId, _day.AddHours(9), _day.AddHours(10), e => e.Title = "Bob's own");
        var allHands = await harness.CreateEventAsync(company.ItemId, _day.AddDays(1).AddHours(9), _day.AddDays(1).AddHours(10), e =>
        {
            e.Title = "All hands";
            e.Status = CalendarEventStatus.Tentative;
            e.Transparency = CalendarTransparency.Free;
        });
        var companyPrivate = await harness.CreateEventAsync(company.ItemId, _day.AddDays(2).AddHours(9), _day.AddDays(2).AddHours(10), e =>
        {
            e.Title = "Secret merger";
            e.Visibility = CalendarEventVisibility.Private;
            e.Attendees.Add(new CalendarAttendee { Email = "ceo@contoso.com", Name = "Secret CEO" });
        });
        var series = await harness.CreateEventAsync(company.ItemId, _day.AddDays(3).AddHours(9), _day.AddDays(3).AddHours(9).AddMinutes(15), e =>
        {
            e.Title = "Stand-up";
            e.Recurrence = new CalendarRecurrence { Rule = "FREQ=DAILY;COUNT=5" };
        });
        await harness.CreateEventAsync(hidden.ItemId, _day.AddHours(11), _day.AddHours(12), e => e.Title = "Hidden");

        return new Seed(
            alice.ItemId,
            bob.ItemId,
            company.ItemId,
            hidden.ItemId,
            Created(aliceMeeting),
            Created(alicePrivate),
            Created(allHands),
            Created(companyPrivate),
            Created(series));
    }

    private static string Created(CalendarResult<CalendarEvent> result)
    {
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Message)));

        return result.Value.ItemId;
    }

    private static async Task<T> RunAsync<T>(CalendarTestHarness harness, Func<CalendarReportDataSource, Task<T>> work)
    {
        await using var scope = harness.CreateScope();

        return await work(scope.ServiceProvider.GetRequiredService<CalendarReportDataSource>());
    }

    private static Task<List<Dictionary<string, object>>> ReadAsync(CalendarTestHarness harness, string dataSet, ClaimsPrincipal user)
    {
        return ReadAsync(harness, Query(dataSet, user));
    }

    private static Task<List<Dictionary<string, object>>> ReadAsync(CalendarTestHarness harness, DataSourceQuery query)
    {
        return RunAsync(harness, async source => Rows(await ReadRowsAsync(source, query)));
    }

    private static Task<DataRowSet> ReadRowsAsync(CalendarReportDataSource source, DataSourceQuery query)
    {
        return ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);
    }

    private static DataSourceQuery Query(string dataSet, ClaimsPrincipal user)
    {
        return new DataSourceQuery
        {
            DataSet = dataSet,
            Fields = new HashSet<string>(StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = user },
        };
    }

    private static Dictionary<string, object> Row(List<Dictionary<string, object>> rows, string itemId)
    {
        return rows.Single(row => (string)row[CalendarReportDataSets.ItemIdField] == itemId);
    }

    private static string Id(Dictionary<string, object> row)
    {
        return (string)row[CalendarReportDataSets.ItemIdField];
    }

    private static DateTime Utc(DateTime newYork)
    {
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(newYork, DateTimeKind.Unspecified), TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
    }

    private static List<Dictionary<string, object>> Rows(DataRowSet table)
    {
        return table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
    }

    private sealed record Seed(
        string Alice,
        string Bob,
        string Company,
        string Hidden,
        string AliceMeeting,
        string AlicePrivate,
        string CompanyAllHands,
        string CompanyPrivate,
        string CompanySeries);
}
