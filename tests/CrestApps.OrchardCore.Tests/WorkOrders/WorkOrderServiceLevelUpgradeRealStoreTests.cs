using CrestApps.Core;
using CrestApps.OrchardCore.BusinessHours.Core;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Legacy;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Documents;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// The Work Orders service-level upgrade against the real stores: a policy stored under its old type name is read; a
/// policy's weekly hours count in the timers the upgrade starts; a work order on hold is not charged for its hold and
/// nothing is announced; the due times a work order had win over today's targets; and a second run does nothing.
/// </summary>
public sealed class WorkOrderServiceLevelUpgradeRealStoreTests
{
    private const string PolicyId = "4legacypolicy0000000000000";
    private const string OldTypeName = "CrestApps.OrchardCore.WorkOrders.Models.ServiceLevelPolicy, CrestApps.OrchardCore.WorkOrders.Abstractions";

    // Friday 2026-10-30 and Monday 2026-11-02; the harness's clock reads Monday 12:00 UTC.
    private static readonly DateTime _friday1600 = new(2026, 10, 30, 16, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _monday1600 = new(2026, 11, 2, 16, 0, 0, DateTimeKind.Utc);

    private readonly List<string> _announced = [];

    [Fact]
    public async Task APolicyStoredUnderItsOldTypeName_IsCopied()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedPolicyAsync(harness, weekly: false);
        await RenameStoredTypeAsync(harness);

        // Act
        var copied = await harness.RunAsync(services => services.GetRequiredService<WorkOrderServiceLevelUpgrade>().CopyPoliciesAsync());

        // Assert
        Assert.Equal(1, copied);
        var policy = await harness.RunAsync(async services => await services.GetRequiredService<IServiceLevelPolicyManager>().FindByIdAsync(PolicyId), commit: false);
        Assert.Equal("Gold", policy?.Name);
    }

    [Fact]
    public async Task AWeeklyHoursPolicy_CountsItsHours_InTheTimersTheUpgradeStarts()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedPolicyAsync(harness, weekly: true);
        await SeedWorkOrderAsync(harness, "friday", _friday1600, sla => { });

        // Act: the policies are copied by the migration, the timers in the deferred step after it commits.
        await harness.RunAsync(services => services.GetRequiredService<WorkOrderServiceLevelUpgrade>().CopyPoliciesAsync());
        await harness.RunAsync(services => services.GetRequiredService<WorkOrderServiceLevelUpgrade>().StartOpenWorkOrdersAsync());

        // Assert: Friday 16:00 + 8 hours of Monday to Friday 9 to 5 is Monday 16:00, not Saturday 00:00.
        var resolution = await TimerAsync(harness, "friday", ServiceLevelMetrics.Resolution);
        Assert.Equal(_monday1600, resolution.DueUtc);
    }

    [Fact]
    public async Task TwoPoliciesWithTheSameName_EachGetASchedule()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedPolicyAsync(harness, weekly: true);
        await SeedPolicyAsync(harness, weekly: true, id: "4legacypolicy0000000000002");

        // Act
        var copied = await harness.RunAsync(services => services.GetRequiredService<WorkOrderServiceLevelUpgrade>().CopyPoliciesAsync());

        // Assert
        Assert.Equal(2, copied);
        var schedules = await harness.RunAsync(async services => await services.GetRequiredService<CrestApps.Core.Services.ICatalog<BusinessHoursSchedule>>().GetAllAsync(), commit: false);
        Assert.Equal(2, schedules.Select(schedule => schedule.Name).Distinct().Count());
    }

    [Fact]
    public async Task AWorkOrderOnHold_IsNotChargedForItsHold_AndNothingIsAnnounced()
    {
        // Arrange: created Monday 26 October 09:00, on hold since 10:00, an 8-hour resolution around the clock.
        await using var harness = await CreateAsync();
        await SeedPolicyAsync(harness, weekly: false);
        var created = new DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);
        await SeedWorkOrderAsync(harness, "held", created, sla => sla.PausedSinceUtc = created.AddHours(1), WorkOrderStatus.OnHold);

        // Act
        await harness.RunAsync(services => services.GetRequiredService<WorkOrderServiceLevelUpgrade>().CopyPoliciesAsync());
        await harness.RunAsync(services => services.GetRequiredService<WorkOrderServiceLevelUpgrade>().StartOpenWorkOrdersAsync());

        // Assert
        var resolution = await TimerAsync(harness, "held", ServiceLevelMetrics.Resolution);
        Assert.Equal(ServiceLevelTimerState.Paused, resolution.State);
        Assert.Equal(3600, resolution.UsedSeconds);
        Assert.Null(resolution.BreachedUtc);
        Assert.Equal(created.AddHours(1), resolution.PausedSinceUtc);
        Assert.Empty(_announced);
    }

    [Fact]
    public async Task TheDueTimesAWorkOrderHad_WinOverTodaysTargets_AndAResponseGivenIsCompletedQuietly()
    {
        // Arrange: created Normal with a 48-hour resolution, then made Urgent (8 hours); the old system kept 48 hours.
        await using var harness = await CreateAsync();
        await SeedPolicyAsync(harness, weekly: false);
        var created = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        await SeedWorkOrderAsync(harness, "edited", created, sla =>
        {
            sla.ResolveByUtc = created.AddHours(48);
            sla.RespondByUtc = created.AddHours(4);
            sla.FirstResponseUtc = created.AddHours(1);
        }, priority: WorkOrderPriority.Urgent);

        // Act
        await harness.RunAsync(services => services.GetRequiredService<WorkOrderServiceLevelUpgrade>().CopyPoliciesAsync());
        await harness.RunAsync(services => services.GetRequiredService<WorkOrderServiceLevelUpgrade>().StartOpenWorkOrdersAsync());

        // Assert
        var resolution = await TimerAsync(harness, "edited", ServiceLevelMetrics.Resolution);
        var response = await TimerAsync(harness, "edited", ServiceLevelMetrics.FirstResponse);
        Assert.Equal(created.AddHours(48), resolution.DueUtc);
        Assert.Null(resolution.BreachedUtc);
        Assert.Equal(ServiceLevelTimerState.Completed, response.State);
        Assert.Equal(created.AddHours(1), response.EndedUtc);
        Assert.Empty(_announced);
    }

    [Fact]
    public async Task ASecondRun_DoesNothing_AcrossBatches()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedPolicyAsync(harness, weekly: false);

        for (var index = 0; index < 5; index++)
        {
            await SeedWorkOrderAsync(harness, $"wo-{index}", new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc), sla => { });
        }

        await harness.RunAsync(services => services.GetRequiredService<WorkOrderServiceLevelUpgrade>().CopyPoliciesAsync());

        // Act
        var first = await harness.RunAsync(services => Upgrade(services, batchSize: 2).StartOpenWorkOrdersAsync());
        var second = await harness.RunAsync(services => Upgrade(services, batchSize: 2).StartOpenWorkOrdersAsync());

        // Assert
        Assert.Equal(5, first);
        Assert.Equal(0, second);

        for (var index = 0; index < 5; index++)
        {
            var timers = await harness.RunAsync(services => services.GetRequiredService<IServiceLevelTracker>().GetTimersAsync(new RecordRef(WorkOrdersConstants.RecordKind, $"wo-{index}")), commit: false);
            Assert.Equal(2, timers.Count);
        }
    }

    private static WorkOrderServiceLevelUpgrade Upgrade(IServiceProvider services, int batchSize)
    {
        var upgrade = services.GetRequiredService<WorkOrderServiceLevelUpgrade>();
        upgrade.BatchSize = batchSize;

        return upgrade;
    }

    private static Task<ServiceLevelTimer> TimerAsync(CalendarTestHarness harness, string workOrderId, string metric)
        => harness.RunAsync(
            async services => (await services.GetRequiredService<IServiceLevelTracker>().GetTimersAsync(new RecordRef(WorkOrdersConstants.RecordKind, workOrderId)))
                .Single(timer => timer.Metric == metric),
            commit: false);

    private async Task<CalendarTestHarness> CreateAsync()
        => await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddWorkOrdersWithServiceLevels();
                services.AddCatalogs().AddCatalogManagers();
                services.AddSingleton<CommittedDocuments>();
                services.AddScoped<IDocumentManager<DictionaryDocument<BusinessHoursSchedule>>, CommittingDocumentManager<BusinessHoursSchedule>>();
                services.AddBusinessHoursSchedules();
                services.AddScoped<IServiceLevelObserver>(_ => new RecordingObserver(_announced));
            },
            async schemaBuilder =>
            {
                await WorkOrderTestStores.CreateSchemaAsync(schemaBuilder);
                await schemaBuilder.CreateMapIndexTableAsync<ServiceLevelPolicyIndex>(table => table
                    .Column<string>("ItemId", column => column.WithLength(26))
                    .Column<string>("Name", column => column.Nullable().WithLength(255)),
                    collection: WorkOrdersConstants.CollectionName);
            },
            [.. WorkOrderTestStores.IndexProviders, new LegacyPolicyIndexProvider()],
            WorkOrderTestStores.Collections);

    private static Task SeedPolicyAsync(CalendarTestHarness harness, bool weekly, string id = PolicyId)
        => harness.RunAsync(services => services.GetRequiredService<ISession>().SaveAsync(new LegacyServiceLevelPolicy
        {
            ItemId = id,
            Name = "Gold",
            TimeZoneId = "UTC",
            WarningPercent = 80,
            WeeklyHours = weekly
                ? new LegacyWeeklyHours { Days = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday] }
                : null,
            Targets =
            [
                new LegacyServiceLevelTarget { Priority = WorkOrderPriority.Normal, ResponseMinutes = 240, ResolveMinutes = 480 },
                new LegacyServiceLevelTarget { Priority = WorkOrderPriority.Urgent, ResponseMinutes = 60, ResolveMinutes = 480 },
            ],
        }, collection: WorkOrdersConstants.CollectionName));

    private static Task SeedWorkOrderAsync(CalendarTestHarness harness, string id, DateTime createdUtc, Action<WorkOrderSla> configure, WorkOrderStatus status = WorkOrderStatus.InProgress, WorkOrderPriority priority = WorkOrderPriority.Normal)
    {
        var workOrder = new WorkOrder
        {
            ItemId = id,
            Number = "WO-" + id,
            Summary = "Boiler service",
            Status = status,
            Priority = priority,
            CreatedUtc = createdUtc,
            Sla = new WorkOrderSla { PolicyId = PolicyId },
        };

        configure(workOrder.Sla);

        return harness.RunAsync(services => services.GetRequiredService<ISession>().SaveAsync(workOrder, collection: WorkOrdersConstants.CollectionName));
    }

    // Writes the stored type name a tenant on the old version has: the class it was saved as no longer exists.
    private static Task RenameStoredTypeAsync(CalendarTestHarness harness)
        => harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var connection = await session.CreateConnectionAsync();
            var table = harness.Store.Configuration.TablePrefix + harness.Store.Configuration.TableNameConvention.GetDocumentTable(WorkOrdersConstants.CollectionName);

            await using var command = connection.CreateCommand();
            command.Transaction = await session.BeginTransactionAsync();
            command.CommandText = $"UPDATE \"{table}\" SET \"Type\" = '{OldTypeName}' WHERE \"Type\" LIKE '%LegacyServiceLevelPolicy%'";
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        });

    private sealed class LegacyPolicyIndexProvider : IndexProvider<LegacyServiceLevelPolicy>
    {
        public LegacyPolicyIndexProvider()
        {
            CollectionName = WorkOrdersConstants.CollectionName;
        }

        public override void Describe(DescribeContext<LegacyServiceLevelPolicy> context)
            => context.For<ServiceLevelPolicyIndex>()
                .Map(policy => new ServiceLevelPolicyIndex { ItemId = policy.ItemId, Name = policy.Name });
    }

    private sealed class RecordingObserver : IServiceLevelObserver
    {
        private readonly List<string> _announced;

        public RecordingObserver(List<string> announced)
        {
            _announced = announced;
        }

        public Task WarningAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
        {
            _announced.Add("warned " + timer.Subject);

            return Task.CompletedTask;
        }

        public Task BreachedAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
        {
            _announced.Add("breached " + timer.Subject);

            return Task.CompletedTask;
        }

        public Task CompletedAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
        {
            _announced.Add("completed " + timer.Subject);

            return Task.CompletedTask;
        }
    }
}
