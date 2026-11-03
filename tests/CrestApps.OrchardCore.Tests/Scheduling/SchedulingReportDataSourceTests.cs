using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Scheduling;
using CrestApps.OrchardCore.Scheduling.Core.Indexes;
using CrestApps.OrchardCore.Scheduling.Migrations;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// The scheduling data source reads real appointments, appointment types, resources and waitlist entries from a SQLite
/// store, so the index queries, the fields, the push-down of dates, join keys and statuses, the grouping in the
/// database, and the permissions each data set requires are exercised end to end.
/// </summary>
public sealed class SchedulingReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Scheduler");
    private static readonly DateTime _start = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"scheduling-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new SchedulingResourceIndexProvider(), new AppointmentTypeIndexProvider(), new AppointmentIndexProvider(), new WaitlistEntryIndexProvider()], SchedulingConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(SchedulingConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);

            await new SchedulingMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await Save(session, new SchedulingResource { ItemId = "resource-smith", Name = "Dr. Smith", Kind = SchedulingResourceKind.Staff, UserId = "user-smith", CalendarId = "calendar-smith", TimeZoneId = "America/New_York" });
            await Save(session, new SchedulingResource { ItemId = "resource-room", Name = "Room 1", Kind = SchedulingResourceKind.Room, CalendarId = "calendar-room", AvailabilityMode = SchedulingAvailabilityMode.Shifts, ShiftCalendarId = "calendar-shifts", Enabled = false });

            await Save(session, new AppointmentType { ItemId = "type-consult", Name = "Consultation", Slug = "consult", DurationMinutes = 45, ResourceIds = ["resource-smith", "resource-room"], Assignment = AppointmentAssignment.AllTogether, AllowSelfService = true, MaxPerDay = 4 });
            await Save(session, new AppointmentType { ItemId = "type-checkup", Name = "Check-up", DurationMinutes = 15, ResourceIds = ["resource-smith"], Enabled = false });

            // Four appointments, one a day: confirmed, cancelled, no-show and held.
            await Save(session, Appointment(0, "type-consult", AppointmentStatus.Confirmed, ["resource-smith", "resource-room"]));
            await Save(session, Appointment(1, "type-checkup", AppointmentStatus.Cancelled, ["resource-smith"]));
            await Save(session, Appointment(2, "type-consult", AppointmentStatus.NoShow, ["resource-smith", "resource-room"]));
            await Save(session, Appointment(3, "type-checkup", AppointmentStatus.Held, []));

            await Save(session, new WaitlistEntry { ItemId = "wait-0", AppointmentTypeId = "type-consult", FromUtc = _start.AddDays(5), ToUtc = _start.AddDays(6), CustomerName = "Ada", CustomerEmail = "ada@contoso.com", Status = WaitlistStatus.Waiting, CreatedUtc = _start });
            await Save(session, new WaitlistEntry { ItemId = "wait-1", AppointmentTypeId = "type-checkup", FromUtc = _start.AddDays(5), ToUtc = _start.AddDays(7), CustomerName = "Bo", Status = WaitlistStatus.Notified, CreatedUtc = _start.AddDays(1), NotifiedUtc = _start.AddDays(2) });

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutPermissionOrUser_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: []);
        var withoutUser = Source(session);
        var noUserQuery = Query(SchedulingReportDataSets.Appointments);
        noUserQuery.Context = new DataSourceContext();

        // Act
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(SchedulingReportDataSets.Appointments, new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var appointments = await ReportDesignerTestServices.ReadAsync(source, Query(SchedulingReportDataSets.Appointments), TestContext.Current.CancellationToken);
        var waitlist = await ReportDesignerTestServices.ReadAsync(source, Query(SchedulingReportDataSets.Waitlist), TestContext.Current.CancellationToken);
        var noUserDataSets = await withoutUser.GetDataSetsAsync(new DataSourceContext(), TestContext.Current.CancellationToken);
        var noUserRows = await ReportDesignerTestServices.ReadAsync(withoutUser, noUserQuery, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(appointments.Rows);
        Assert.Empty(waitlist.Rows);
        Assert.Empty(noUserDataSets);
        Assert.Empty(noUserRows.Rows);
    }

    [Fact]
    public async Task ViewAppointments_ListsAllButTheWaitlist()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var viewer = Source(session, [SchedulingPermissions.ViewAppointments.Name]);
        var manager = Source(session, [SchedulingPermissions.ManageAppointments.Name, SchedulingPermissions.ViewAppointments.Name]);
        var context = new DataSourceContext { User = _user };

        // Act
        var viewerDataSets = await viewer.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var managerDataSets = await manager.GetDataSetsAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [SchedulingReportDataSets.Appointments, SchedulingReportDataSets.AppointmentResources, SchedulingReportDataSets.AppointmentTypes, SchedulingReportDataSets.Resources],
            viewerDataSets.Select(dataSet => dataSet.Name));
        Assert.Equal(5, managerDataSets.Count);
        Assert.Contains(managerDataSets, dataSet => dataSet.Name == SchedulingReportDataSets.Waitlist);
    }

    [Fact]
    public async Task Schema_DeclaresKeysAndReferences_AndNeverExposesNotesOrTokens()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var appointments = await source.GetSchemaAsync(SchedulingReportDataSets.Appointments, context, TestContext.Current.CancellationToken);
        var resources = await source.GetSchemaAsync(SchedulingReportDataSets.AppointmentResources, context, TestContext.Current.CancellationToken);
        var waitlist = await source.GetSchemaAsync(SchedulingReportDataSets.Waitlist, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SchedulingReportDataSets.Source, SchedulingReportDataSource.Name);
        Assert.Equal(AppointmentsReportDataSet.StartUtcField, appointments.DataSet.DefaultDateField);
        Assert.True(appointments.FindField(SchedulingReportDataSets.AppointmentTypeIdField).IsKeyFilterable);
        Assert.Equal(SchedulingReportDataSets.AppointmentTypes, Assert.Single(appointments.FindField(SchedulingReportDataSets.AppointmentTypeIdField).References).DataSet);
        Assert.Equal(ReportsConstants.UsersDataSource, Assert.Single(appointments.FindField("UserId").References).Source);
        Assert.Equal("CalendarEvents", Assert.Single(appointments.FindField("CalendarEventId").References).DataSet);
        Assert.DoesNotContain(appointments.Fields, field => field.Name is "Notes" or "ManageToken" or "CancelReason");
        Assert.Equal(SchedulingReportDataSets.Resources, Assert.Single(resources.FindField(SchedulingReportDataSets.ResourceIdField).References).DataSet);
        Assert.True(resources.FindField(SchedulingReportDataSets.AppointmentIdField).IsKeyFilterable);
        Assert.Equal(WaitlistReportDataSet.CreatedUtcField, waitlist.DataSet.DefaultDateField);
    }

    [Fact]
    public async Task Query_Appointments_ReturnTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var table = await ReportDesignerTestServices.ReadAsync(source, Query(SchedulingReportDataSets.Appointments), TestContext.Current.CancellationToken);
        var rows = Rows(table);

        // Assert
        Assert.Equal(["appointment-3", "appointment-2", "appointment-1", "appointment-0"], rows.Select(row => row[SchedulingReportDataSets.ItemIdField]));
        Assert.Equal(["Held", "NoShow", "Cancelled", "Confirmed"], rows.Select(row => row["Status"]));
        Assert.Equal([false, false, true, false], rows.Select(row => row["IsCancelled"]));
        Assert.Equal([false, true, false, false], rows.Select(row => row["IsNoShow"]));

        var confirmed = rows[3];
        Assert.Equal("type-consult", confirmed[SchedulingReportDataSets.AppointmentTypeIdField]);
        Assert.Equal("resource-smith, resource-room", confirmed["ResourceIds"]);
        Assert.Equal(2L, confirmed["ResourceCount"]);
        Assert.Equal("Ada 0", confirmed["CustomerName"]);
        Assert.Equal("Ada0@Contoso.com", confirmed["CustomerEmail"]);
        Assert.Equal("user-0", confirmed["UserId"]);
        Assert.Equal("Admin", confirmed["Source"]);
        Assert.Equal("event-0", confirmed["CalendarEventId"]);
        Assert.Equal(_start.AddDays(10), confirmed[AppointmentsReportDataSet.StartUtcField]);
        Assert.Equal(45L, confirmed["DurationMinutes"]);
        Assert.Equal(10L * 24 * 60, confirmed["LeadTimeMinutes"]);
        Assert.Equal(_start, confirmed["CreatedUtc"]);
        Assert.Null(confirmed["HoldExpiresUtc"]);

        Assert.Null(rows[0]["ResourceIds"]);
        Assert.Equal(0L, rows[0]["ResourceCount"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(rows[0]["HoldExpiresUtc"]).Kind);
        Assert.DoesNotContain(table.Rows.SelectMany(row => row), value => value is string text && text.Contains("private", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Query_ResourcesTypesAndWaitlist_ReturnTypedValues()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var booked = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(SchedulingReportDataSets.AppointmentResources), TestContext.Current.CancellationToken));
        var types = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(SchedulingReportDataSets.AppointmentTypes), TestContext.Current.CancellationToken));
        var resources = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(SchedulingReportDataSets.Resources), TestContext.Current.CancellationToken));
        var waitlist = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(SchedulingReportDataSets.Waitlist), TestContext.Current.CancellationToken));

        // Assert (one row per resource booked; the held appointment books none)
        Assert.Equal(
            ["appointment-2/resource-smith", "appointment-2/resource-room", "appointment-1/resource-smith", "appointment-0/resource-smith", "appointment-0/resource-room"],
            booked.Select(row => $"{row[SchedulingReportDataSets.AppointmentIdField]}/{row[SchedulingReportDataSets.ResourceIdField]}"));
        Assert.Equal(2L, booked[1]["Position"]);
        Assert.Equal("NoShow", booked[0]["Status"]);
        Assert.Equal(45L, booked[0]["DurationMinutes"]);

        var consult = types.Single(row => (string)row[SchedulingReportDataSets.ItemIdField] == "type-consult");
        Assert.Equal("Consultation", consult["Name"]);
        Assert.Equal(45L, consult["DurationMinutes"]);
        Assert.Equal("AllTogether", consult["Assignment"]);
        Assert.Equal(true, consult["AllowSelfService"]);
        Assert.Equal(2L, consult["ResourceCount"]);
        Assert.Equal(4L, consult["MaxPerDay"]);
        Assert.Equal(false, types.Single(row => (string)row[SchedulingReportDataSets.ItemIdField] == "type-checkup")["Enabled"]);

        var smith = resources.Single(row => (string)row[SchedulingReportDataSets.ItemIdField] == "resource-smith");
        Assert.Equal("Staff", smith["Kind"]);
        Assert.Equal("user-smith", smith["UserId"]);
        Assert.Equal("calendar-smith", smith["CalendarId"]);
        Assert.Equal("WorkingHours", smith["AvailabilityMode"]);
        var room = resources.Single(row => (string)row[SchedulingReportDataSets.ItemIdField] == "resource-room");
        Assert.Equal("Shifts", room["AvailabilityMode"]);
        Assert.Equal("calendar-shifts", room["ShiftCalendarId"]);
        Assert.Equal(false, room["Enabled"]);
        Assert.DoesNotContain(resources.SelectMany(row => row.Keys), key => key is "WorkingHours" or "DateOverrides");

        Assert.Equal(["wait-1", "wait-0"], waitlist.Select(row => row[SchedulingReportDataSets.ItemIdField]));
        Assert.Equal("Notified", waitlist[0]["Status"]);
        Assert.Equal(_start.AddDays(2), waitlist[0]["NotifiedUtc"]);
        Assert.Equal("ada@contoso.com", waitlist[1]["CustomerEmail"]);
    }

    [Fact]
    public async Task Query_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(SchedulingReportDataSets.Appointments);
        dates.Conditions.Add(Condition(AppointmentsReportDataSet.StartUtcField, DataFilterOperator.Between, _start.AddDays(11), _start.AddDays(12)));
        var types = Query(SchedulingReportDataSets.Appointments);
        types.Conditions.Add(new DataCondition { Field = SchedulingReportDataSets.AppointmentTypeIdField, Operator = DataFilterOperator.In, Values = ["type-consult"], IsJoinKey = true });
        var status = Query(SchedulingReportDataSets.Appointments);
        status.Conditions.Add(Condition("Status", DataFilterOperator.In, "noshow", "CANCELLED"));
        var booked = Query(SchedulingReportDataSets.AppointmentResources);
        booked.Conditions.Add(new DataCondition { Field = SchedulingReportDataSets.AppointmentIdField, Operator = DataFilterOperator.In, Values = ["appointment-0"], IsJoinKey = true });
        var waitlist = Query(SchedulingReportDataSets.Waitlist);
        waitlist.Conditions.Add(Condition("Status", DataFilterOperator.In, "waiting"));
        var none = Query(SchedulingReportDataSets.Appointments);
        none.Conditions.Add(new DataCondition { Field = SchedulingReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var typeRows = Rows(await ReportDesignerTestServices.ReadAsync(source, types, TestContext.Current.CancellationToken));
        var statusRows = Rows(await ReportDesignerTestServices.ReadAsync(source, status, TestContext.Current.CancellationToken));
        var bookedRows = Rows(await ReportDesignerTestServices.ReadAsync(source, booked, TestContext.Current.CancellationToken));
        var waitlistRows = Rows(await ReportDesignerTestServices.ReadAsync(source, waitlist, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["appointment-2", "appointment-1"], dateRows.Select(row => row[SchedulingReportDataSets.ItemIdField]));
        Assert.Equal(["appointment-2", "appointment-0"], typeRows.Select(row => row[SchedulingReportDataSets.ItemIdField]));
        Assert.Equal(["appointment-2", "appointment-1"], statusRows.Select(row => row[SchedulingReportDataSets.ItemIdField]));
        Assert.Equal(["resource-smith", "resource-room"], bookedRows.Select(row => row[SchedulingReportDataSets.ResourceIdField]));
        Assert.Equal(["wait-0"], waitlistRows.Select(row => row[SchedulingReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_CountsAppointmentsByType_AndDeclinesTheCustomer()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var byType = new DataAggregateQuery
        {
            DataSet = SchedulingReportDataSets.Appointments,
            Groups = [new DataAggregateGroup { Field = SchedulingReportDataSets.AppointmentTypeIdField }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };
        var byCustomer = new DataAggregateQuery
        {
            DataSet = SchedulingReportDataSets.Appointments,
            Groups = [new DataAggregateGroup { Field = "CustomerEmail" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var types = await ((IDataAggregateSource)source).AggregateAsync(byType, TestContext.Current.CancellationToken);
        var customers = await ((IDataAggregateSource)source).AggregateAsync(byCustomer, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(types);
        var counts = types.ToDictionary(row => (string)row[0], row => Convert.ToInt64(row[1], CultureInfo.InvariantCulture));
        Assert.Equal(2L, counts["type-consult"]);
        Assert.Equal(2L, counts["type-checkup"]);

        // The index keeps email addresses in lower case, so the grouping is left to the report.
        Assert.Null(customers);
    }

    [Fact]
    public async Task ReportIndexMigration_CanRunAgain()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migrations = new SchedulingMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) };

        // Act
        var first = await migrations.UpdateFrom1Async();
        var second = await migrations.UpdateFrom1Async();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public async Task DataSourcesStartup_RegistersTheDataSource()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization([SchedulingPermissions.ViewAppointments.Name]));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<SchedulingReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, dataSets.Count);
    }

    private static Appointment Appointment(int day, string typeId, AppointmentStatus status, IList<string> resourceIds)
    {
        var booked = _start.AddDays(day);

        return new Appointment
        {
            ItemId = $"appointment-{day}",
            AppointmentTypeId = typeId,
            ResourceIds = resourceIds,
            StartUtc = _start.AddDays(10 + day),
            EndUtc = _start.AddDays(10 + day).AddMinutes(45),
            TimeZoneId = "America/New_York",
            Status = status,
            HoldExpiresUtc = status == AppointmentStatus.Held ? booked.AddMinutes(10) : null,
            CustomerName = $"Ada {day}",
            CustomerEmail = $"Ada{day}@Contoso.com",
            CustomerPhone = "+17025550100",
            UserId = $"user-{day}",
            Notes = "Private notes",
            ManageToken = "private-token",
            CancelReason = status == AppointmentStatus.Cancelled ? "Private reason" : null,
            CalendarEventId = $"event-{day}",
            Source = "Admin",
            CreatedUtc = booked,
            ModifiedUtc = booked,
        };
    }

    private static Task Save(ISession session, object record)
    {
        return session.SaveAsync(record, false, SchedulingConstants.CollectionName, TestContext.Current.CancellationToken);
    }

    private static SchedulingReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [SchedulingPermissions.ViewAppointments.Name, SchedulingPermissions.ManageAppointments.Name]);

        return new SchedulingReportDataSource(
            new AppointmentsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<AppointmentsReportDataSet>()),
            new AppointmentResourcesReportDataSet(session, authorizationService, new PassThroughStringLocalizer<AppointmentResourcesReportDataSet>()),
            new AppointmentTypesReportDataSet(session, authorizationService, new PassThroughStringLocalizer<AppointmentTypesReportDataSet>()),
            new SchedulingResourcesReportDataSet(session, authorizationService, new PassThroughStringLocalizer<SchedulingResourcesReportDataSet>()),
            new WaitlistReportDataSet(session, authorizationService, new PassThroughStringLocalizer<WaitlistReportDataSet>()));
    }

    private static IAuthorizationService Authorization(string[] grants)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => grants.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorizationService.Object;
    }

    private static DataCondition Condition(string field, DataFilterOperator filterOperator, params object[] values)
    {
        return new DataCondition
        {
            Field = field,
            Operator = filterOperator,
            Values = values,
        };
    }

    private static DataSourceQuery Query(string dataSet)
    {
        return new DataSourceQuery
        {
            DataSet = dataSet,
            Fields = new HashSet<string>(StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = _user },
        };
    }

    private static List<Dictionary<string, object>> Rows(DataRowSet table)
    {
        return table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
    }
}
