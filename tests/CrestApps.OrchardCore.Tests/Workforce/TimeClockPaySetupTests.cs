using System.Data.Common;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Payroll.Core;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Migrations;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The time clock on Pay Setup: pay codes and schedules it uses are kept, a shift differential needs its window, and the
/// migration moves the pay periods and pay codes the time clock kept to Pay Setup with their ids, safely twice.
/// </summary>
public sealed class TimeClockPaySetupTests
{
    [Fact]
    public async Task PayCode_CodesAreUnique_AndAShiftDifferentialNeedsItsWindow()
    {
        await using var kit = await TimeClockKit.CreateAsync();

        var (duplicate, noWindow, withWindow) = await kit.Harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayCodeManager>();
            var copy = await manager.NewAsync();

            copy.Code = "reg";

            var night = await manager.NewAsync();

            night.Code = "NIGHT";
            night.Kind = PayCodeKinds.ShiftDifferential;
            night.PremiumPerHour = 2;

            var first = await manager.ValidateAsync(copy);
            var second = await manager.ValidateAsync(night);

            night.Put(new WorkforcePayCodeSettings { WindowStart = new TimeOnly(22, 0), WindowEnd = new TimeOnly(6, 0) });

            return (first, second, await manager.ValidateAsync(night));
        }, commit: false);

        Assert.False(duplicate.Succeeded);
        Assert.False(noWindow.Succeeded);
        Assert.True(withWindow.Succeeded);
    }

    [Fact]
    public async Task Usage_ASchedulePolicyUses_AndACodeATimesheetUses_AreReported()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        await kit.AddTeamAsync("Boston");
        var policy = kit.Policy;
        var spare = await kit.Harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayScheduleManager>();
            var schedule = await manager.NewAsync();

            schedule.Name = "Monthly";
            schedule.Frequency = PayFrequency.Monthly;
            await manager.CreateAsync(schedule);

            return schedule;
        });

        var (calendarUses, spareUses, regularUses, bonusUses) = await kit.Harness.RunAsync(async services =>
        {
            var usage = services.GetServices<IPaySetupUsage>().Single();
            var timesheet = new Timesheet
            {
                ItemId = UniqueId.GenerateId(),
                WorkerId = "worker",
                PayPeriodCalendarId = kit.Calendar.ItemId,
                PeriodStart = kit.Calendar.PeriodOn(TimeClockKit.Monday).Start,
                Lines = [new TimesheetLine { PayCodeId = kit.Codes["REG"].ItemId, Hours = 8 }],
            };

            await services.GetRequiredService<ISession>().SaveAsync(timesheet, false, WorkforceConstants.CollectionName);
            await services.GetRequiredService<ISession>().FlushAsync();

            var bonus = new PayCode { ItemId = UniqueId.GenerateId(), Code = "BONUS", Kind = PayCodeKinds.Bonus };

            return (
                await usage.GetUsagesAsync(kit.Calendar),
                await usage.GetUsagesAsync(spare),
                await usage.GetUsagesAsync(kit.Codes["REG"]),
                await usage.GetUsagesAsync(bonus));
        }, commit: false);

        Assert.Contains(calendarUses, use => use.Contains(policy.Name, StringComparison.Ordinal));
        Assert.Contains(calendarUses, use => use.Contains("timesheet", StringComparison.Ordinal));
        Assert.Empty(spareUses);
        Assert.Equal(["timesheets"], regularUses);
        Assert.Empty(bonusUses);
    }

    [Fact]
    public async Task Move_KeepsIds_MapsOldKindNumbers_PutsTheWindowInSettings_AndReplacesSeededCodes()
    {
        await using var harness = await WorkforceTests.CreateAsync(services => services.AddPaySetupCore(), schema: CreateLegacyIndexTablesAsync);

        // Pay Setup seeds the usual codes when it is turned on, just before the time clock's migration moves the old ones.
        await harness.RunAsync(services => services.GetRequiredService<PayCodeSeeder>().SeedAsync());

        var calendar = new LegacyPayPeriodCalendar { ItemId = UniqueId.GenerateId(), Name = "Every two weeks", Frequency = 2, AnchorDate = new DateOnly(2026, 1, 4), WeekStart = DayOfWeek.Monday };
        var regular = new LegacyPayCode { ItemId = UniqueId.GenerateId(), Code = "REG", Name = "Regular hours", Kind = 0, Multiplier = 1 };
        var night = new LegacyPayCode
        {
            ItemId = UniqueId.GenerateId(),
            Code = "NIGHT",
            Name = "Night",
            Kind = 7,
            PremiumPerHour = 1.5m,
            WindowStart = new TimeOnly(22, 0),
            WindowEnd = new TimeOnly(6, 0),
            Weekdays = [DayOfWeek.Friday, DayOfWeek.Saturday],
        };
        var unpaid = new LegacyPayCode { ItemId = UniqueId.GenerateId(), Code = "LWOP", Name = "Leave without pay", Kind = 8, Multiplier = 0 };

        await InsertLegacyDocumentsAsync(harness.Store, (TimeClockPaySetupMove.LegacyPayPeriodTypeName, calendar), (TimeClockPaySetupMove.LegacyPayCodeTypeName, regular), (TimeClockPaySetupMove.LegacyPayCodeTypeName, night), (TimeClockPaySetupMove.LegacyPayCodeTypeName, unpaid));

        var first = await MoveAsync(harness.Store, dropLegacyTables: true);
        var afterFirst = await ReadAsync(harness);

        // A second run, as on a site whose migration ran again, finds nothing to move and changes nothing.
        var second = await MoveAsync(harness.Store, dropLegacyTables: false);
        var afterSecond = await ReadAsync(harness);
        var (legacyDocuments, legacyTables) = await ReadLegacyStateAsync(harness.Store);

        Assert.Equal((1, 3, 0, 0), (first.Schedules, first.Codes, first.LeftInPlace, first.Renamed));
        Assert.Equal((0, 0, 0, 0), (second.Schedules, second.Codes, second.LeftInPlace, second.Renamed));
        Assert.Equal(0, legacyDocuments);
        Assert.Equal(0, legacyTables);

        var schedule = Assert.Single(afterFirst.Schedules);

        Assert.Equal(calendar.ItemId, schedule.ItemId);
        Assert.Equal(PayFrequency.SemiMonthly, schedule.Frequency);
        Assert.Equal(DayOfWeek.Monday, schedule.WeekStart);

        // The seeded REG gave way to the old one, which keeps its id; the other seeded codes stay.
        var codes = afterFirst.Codes.ToDictionary(code => code.Code, StringComparer.Ordinal);

        Assert.Equal(regular.ItemId, codes["REG"].ItemId);
        Assert.Equal("Regular hours", codes["REG"].Name);
        Assert.Single(afterFirst.Codes, code => code.Code == "REG");
        Assert.Contains("OT", codes.Keys);

        Assert.Equal(night.ItemId, codes["NIGHT"].ItemId);
        Assert.Equal(PayCodeKinds.ShiftDifferential, codes["NIGHT"].Kind);
        Assert.Equal(PayCodeClass.Earning, codes["NIGHT"].Class);
        Assert.Equal(new TimeOnly(22, 0), codes["NIGHT"].GetWorkforceSettings().WindowStart);
        Assert.Equal([DayOfWeek.Friday, DayOfWeek.Saturday], codes["NIGHT"].GetWorkforceSettings().Weekdays);

        Assert.Equal(PayCodeKinds.Unpaid, codes["LWOP"].Kind);
        Assert.Equal(PayCodeClass.Memo, codes["LWOP"].Class);

        Assert.Equal(afterFirst.Codes.Select(code => (code.ItemId, code.Code, code.Kind)).Order(), afterSecond.Codes.Select(code => (code.ItemId, code.Code, code.Kind)).Order());
        Assert.Equal(afterFirst.Schedules.Select(item => item.ItemId), afterSecond.Schedules.Select(item => item.ItemId));
    }

    [Fact]
    public async Task Move_LeavesARecordAlreadyInPaySetupAsItIs()
    {
        await using var harness = await WorkforceTests.CreateAsync(services => services.AddPaySetupCore(), schema: CreateLegacyIndexTablesAsync);
        var existing = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayScheduleManager>();
            var schedule = await manager.NewAsync();

            schedule.Name = "Kept";
            await manager.CreateAsync(schedule);

            return schedule;
        });

        await InsertLegacyDocumentsAsync(harness.Store, (TimeClockPaySetupMove.LegacyPayPeriodTypeName, new LegacyPayPeriodCalendar { ItemId = existing.ItemId, Name = "Old" }));

        var moved = await MoveAsync(harness.Store, dropLegacyTables: true);
        var after = await ReadAsync(harness);

        Assert.Equal((0, 0), (moved.Schedules, moved.Codes));
        Assert.Equal("Kept", Assert.Single(after.Schedules).Name);
    }

    [Fact]
    public async Task Move_LeavesALegacyRecordItCouldNotRead_InPlace_AndReportsIt()
    {
        await using var harness = await WorkforceTests.CreateAsync(services => services.AddPaySetupCore(), schema: CreateLegacyIndexTablesAsync);
        var good = new LegacyPayCode { ItemId = UniqueId.GenerateId(), Code = "TRAIN", Name = "Training", Kind = 0 };

        await InsertLegacyDocumentsAsync(harness.Store, (TimeClockPaySetupMove.LegacyPayCodeTypeName, good));

        // One without an id, one that isn't JSON at all, and a pay period without an id.
        await InsertRawLegacyDocumentAsync(harness.Store, 200_001, TimeClockPaySetupMove.LegacyPayCodeTypeName, "{\"Code\":\"BAD\",\"Name\":\"No id\"}");
        await InsertRawLegacyDocumentAsync(harness.Store, 200_002, TimeClockPaySetupMove.LegacyPayCodeTypeName, "not json");
        await InsertRawLegacyDocumentAsync(harness.Store, 200_003, TimeClockPaySetupMove.LegacyPayPeriodTypeName, "{\"Name\":\"No id\"}");

        var logger = new ListLogger();
        var moved = await MoveAsync(harness.Store, dropLegacyTables: true, logger);
        var after = await ReadAsync(harness);
        var (legacyDocuments, _) = await ReadLegacyStateAsync(harness.Store);

        // The good code moved and its old copy is gone; the three unreadable ones stay where they were.
        Assert.Equal((0, 1, 3), (moved.Schedules, moved.Codes, moved.LeftInPlace));
        Assert.Equal(good.ItemId, Assert.Single(after.Codes, code => code.Code == "TRAIN").ItemId);
        Assert.Equal(3, legacyDocuments);
        Assert.Equal(3, logger.Warnings.Count);
        Assert.Contains(logger.Warnings, warning => warning.Contains("200001", StringComparison.Ordinal) && warning.Contains(TimeClockPaySetupMove.LegacyPayCodeTypeName, StringComparison.Ordinal));
        Assert.Contains(logger.Warnings, warning => warning.Contains("200003", StringComparison.Ordinal) && warning.Contains(TimeClockPaySetupMove.LegacyPayPeriodTypeName, StringComparison.Ordinal));

        // A warning names the document, never what it holds.
        Assert.DoesNotContain(logger.Warnings, warning => warning.Contains("No id", StringComparison.Ordinal) || warning.Contains("not json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Move_KeepsAPayrollCodeSomeoneChanged_AndMovesTheOldOneInUnderASuffixedCode()
    {
        await using var harness = await WorkforceTests.CreateAsync(services => services.AddPaySetupCore(), schema: CreateLegacyIndexTablesAsync);

        await harness.RunAsync(services => services.GetRequiredService<PayCodeSeeder>().SeedAsync());

        // Someone renamed the seeded REG before the move.
        var edited = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayCodeManager>();
            var reg = (await manager.GetAllAsync()).Single(code => code.Code == "REG");

            reg.Name = "Regular (union)";
            await manager.UpdateAsync(reg);

            return reg;
        });

        var regular = new LegacyPayCode { ItemId = UniqueId.GenerateId(), Code = "REG", Name = "Regular hours", Kind = 0, Multiplier = 1 };
        var overtime = new LegacyPayCode { ItemId = UniqueId.GenerateId(), Code = "OT", Name = "Overtime", Kind = 1, Multiplier = 1.5m };

        await InsertLegacyDocumentsAsync(harness.Store, (TimeClockPaySetupMove.LegacyPayCodeTypeName, regular), (TimeClockPaySetupMove.LegacyPayCodeTypeName, overtime));

        var logger = new ListLogger();
        var moved = await MoveAsync(harness.Store, dropLegacyTables: true, logger);
        var codes = (await ReadAsync(harness)).Codes;

        // The changed REG stays as it is; the old one moves in beside it with its own id and a suffixed code.
        var kept = Assert.Single(codes, code => code.ItemId == edited.ItemId);

        Assert.Equal(("REG", "Regular (union)"), (kept.Code, kept.Name));
        Assert.Equal("REG-OLD", Assert.Single(codes, code => code.ItemId == regular.ItemId).Code);

        // The seeded OT nobody touched gives way to the old one, as before.
        Assert.Equal(overtime.ItemId, Assert.Single(codes, code => code.Code == "OT").ItemId);
        Assert.Equal((2, 1), (moved.Codes, moved.Renamed));
        Assert.Contains(logger.Warnings, warning => warning.Contains("REG-OLD", StringComparison.Ordinal) && warning.Contains(regular.ItemId, StringComparison.Ordinal));
    }

    private static async Task InsertRawLegacyDocumentAsync(IStore store, long id, string type, string content)
    {
        await using var connection = store.Configuration.ConnectionFactory.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "INSERT INTO Workforce_Document (Id, Type, Content, Version) VALUES (@id, @type, @content, 0)";
        AddParameter(command, "@id", id);
        AddParameter(command, "@type", type);
        AddParameter(command, "@content", content);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private sealed class ListLogger : Microsoft.Extensions.Logging.ILogger
    {
        public List<string> Warnings { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
            => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel == Microsoft.Extensions.Logging.LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }

    private static Task<(IReadOnlyList<PaySchedule> Schedules, IReadOnlyList<PayCode> Codes)> ReadAsync(CalendarTestHarness harness)
        => harness.RunAsync(async services => (
            (IReadOnlyList<PaySchedule>)(await services.GetRequiredService<IPayScheduleManager>().GetAllAsync()).ToList(),
            (IReadOnlyList<PayCode>)(await services.GetRequiredService<IPayCodeManager>().GetAllAsync()).ToList()), commit: false);

    private static async Task CreateLegacyIndexTablesAsync(ISchemaBuilder schemaBuilder)
    {
        // The tables the time clock's first migration made in the Workforce collection, with their own index names.
        await schemaBuilder.CreateTableAsync("Workforce_PayPeriodCalendarIndex", table => table
            .Column<long>("Id", column => column.PrimaryKey().Identity())
            .Column<long>("DocumentId")
            .Column<string>("ItemId", column => column.WithLength(26)));

        await schemaBuilder.CreateTableAsync("Workforce_PayCodeIndex", table => table
            .Column<long>("Id", column => column.PrimaryKey().Identity())
            .Column<long>("DocumentId")
            .Column<string>("ItemId", column => column.WithLength(26)));
    }

    private static async Task InsertLegacyDocumentsAsync(IStore store, params (string Type, object Document)[] documents)
    {
        await using var connection = store.Configuration.ConnectionFactory.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        // YesSql gives document ids itself, so the old documents take ids well above any the harness has used.
        var id = 100_000;

        foreach (var (type, document) in documents)
        {
            await using var command = connection.CreateCommand();

            command.CommandText = "INSERT INTO Workforce_Document (Id, Type, Content, Version) VALUES (@id, @type, @content, 0)";
            AddParameter(command, "@id", id++);
            AddParameter(command, "@type", type);
            AddParameter(command, "@content", store.Configuration.ContentSerializer.Serialize(document));
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task<TimeClockPaySetupMove.MoveResult> MoveAsync(IStore store, bool dropLegacyTables, Microsoft.Extensions.Logging.ILogger logger = null)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction, throwOnError: true);
        var moved = await TimeClockPaySetupMove.MoveAsync(schemaBuilder, store, session, dropLegacyTables, logger);

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return moved;
    }

    private static async Task<(long Documents, long Tables)> ReadLegacyStateAsync(IStore store)
    {
        await using var connection = store.Configuration.ConnectionFactory.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var documents = connection.CreateCommand();

        documents.CommandText = "SELECT COUNT(*) FROM Workforce_Document WHERE Type IN (@schedule, @code)";
        AddParameter(documents, "@schedule", TimeClockPaySetupMove.LegacyPayPeriodTypeName);
        AddParameter(documents, "@code", TimeClockPaySetupMove.LegacyPayCodeTypeName);

        await using var tables = connection.CreateCommand();

        tables.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Workforce_PayPeriodCalendarIndex', 'Workforce_PayCodeIndex')";

        return (
            Convert.ToInt64(await documents.ExecuteScalarAsync(TestContext.Current.CancellationToken)),
            Convert.ToInt64(await tables.ExecuteScalarAsync(TestContext.Current.CancellationToken)));
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();

        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    // The shape the time clock stored its pay periods in, with the frequency as its old enum number.
    private sealed class LegacyPayPeriodCalendar : CatalogItem
    {
        public string Name { get; set; }

        public int Frequency { get; set; } = 1;

        public DateOnly AnchorDate { get; set; } = new(2026, 1, 4);

        public DayOfWeek WeekStart { get; set; }

        public bool Enabled { get; set; } = true;
    }

    // The shape the time clock stored its pay codes in, with the kind as its old enum number and the window on the code.
    private sealed class LegacyPayCode : CatalogItem
    {
        public string Code { get; set; }

        public string Name { get; set; }

        public int Kind { get; set; }

        public decimal Multiplier { get; set; } = 1;

        public decimal? PremiumPerHour { get; set; }

        public TimeOnly? WindowStart { get; set; }

        public TimeOnly? WindowEnd { get; set; }

        public IList<DayOfWeek> Weekdays { get; set; } = [];

        public bool Enabled { get; set; } = true;
    }
}
