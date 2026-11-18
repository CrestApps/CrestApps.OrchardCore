using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Core;
using CrestApps.OrchardCore.BusinessHours.Models;
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
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// Moving Work Orders onto the shared Service Levels block: a policy Work Orders kept becomes a shared policy with the
/// same id (its weekly hours become a Business Hours schedule), an open work order gets the timers of its policy without
/// raising again what was already raised, and running the upgrade twice changes nothing more.
/// </summary>
public sealed class WorkOrderServiceLevelUpgradeTests
{
    private const string PolicyId = "4legacypolicy0000000000000";

    [Fact]
    public async Task TheUpgrade_CopiesPolicies_AndGivesOpenWorkOrdersTheirTimers_Once()
    {
        // Arrange
        var schedules = new InMemoryNamedCatalog<BusinessHoursSchedule>();

        await using var harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddWorkOrdersWithServiceLevels();
                services.AddCatalogManagers();
                services.AddSingleton<INamedCatalog<BusinessHoursSchedule>>(schedules);
                services.AddSingleton<ICatalog<BusinessHoursSchedule>>(schedules);
                services.AddBusinessHoursSchedules();
            },
            async schemaBuilder =>
            {
                await WorkOrderTestStores.CreateSchemaAsync(schemaBuilder);

                // The table Work Orders kept its policies in before the move.
                await schemaBuilder.CreateMapIndexTableAsync<ServiceLevelPolicyIndex>(table => table
                    .Column<string>("ItemId", column => column.WithLength(26))
                    .Column<string>("Name", column => column.Nullable().WithLength(255)),
                    collection: WorkOrdersConstants.CollectionName);
            },
            [.. WorkOrderTestStores.IndexProviders, new LegacyPolicyIndexProvider()],
            WorkOrderTestStores.Collections);

        var now = CalendarTestHarness.Now;

        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();

            await session.SaveAsync(new LegacyServiceLevelPolicy
            {
                ItemId = PolicyId,
                Name = "Gold",
                TimeZoneId = "America/New_York",
                WarningPercent = 70,
                PauseStatuses = [WorkOrderStatus.OnHold],
                WeeklyHours = new LegacyWeeklyHours
                {
                    Days = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                },
                Targets =
                [
                    new LegacyServiceLevelTarget { Priority = WorkOrderPriority.High, ResponseMinutes = 60, ResolveMinutes = 240 },
                ],
            }, collection: WorkOrdersConstants.CollectionName);

            await session.SaveAsync(NewWorkOrder("open", WorkOrderStatus.InProgress, now.AddMinutes(-30), sla =>
            {
                sla.FirstResponseUtc = now.AddMinutes(-20);
                sla.ResponseWarned = true;
            }), collection: WorkOrdersConstants.CollectionName);

            await session.SaveAsync(NewWorkOrder("done", WorkOrderStatus.Completed, now.AddDays(-3), _ => { }), collection: WorkOrdersConstants.CollectionName);
        });

        // Act
        var first = await harness.RunAsync(services => services.GetRequiredService<WorkOrderServiceLevelUpgrade>().RunAsync());
        var second = await harness.RunAsync(services => services.GetRequiredService<WorkOrderServiceLevelUpgrade>().RunAsync());

        // Assert
        Assert.Equal((1, 1), first);
        Assert.Equal((0, 0), second);

        var schedule = Assert.Single(schedules.Entries);

        Assert.Equal("Gold hours", schedule.Name);
        Assert.Equal("America/New_York", schedule.Hours.TimeZoneId);
        Assert.Equal(5, schedule.Hours.Windows.Count);
        Assert.All(schedule.Hours.Windows, window => Assert.Equal((540, 1020), (window.OpenMinute, window.CloseMinute)));

        await harness.RunAsync(async services =>
        {
            var policy = await services.GetRequiredService<IServiceLevelPolicyManager>().FindByIdAsync(PolicyId);

            Assert.NotNull(policy);
            Assert.Equal("Gold", policy.Name);
            Assert.Equal(WorkOrdersConstants.ServiceLevelScope, policy.Scope);
            Assert.Equal(schedule.ItemId, policy.HoursScheduleId);
            Assert.Equal(70, policy.WarningPercent);
            Assert.Equal(60, policy.FindTarget(ServiceLevelMetrics.FirstResponse, "High").Minutes);
            Assert.Equal(240, policy.FindTarget(ServiceLevelMetrics.Resolution, "High").Minutes);
            Assert.True(policy.TryGet<WorkOrderServiceLevelSettings>(out var settings));
            Assert.Equal([WorkOrderStatus.OnHold], settings.PauseStatuses);

            var tracker = services.GetRequiredService<IServiceLevelTracker>();
            var openTimers = await tracker.GetTimersAsync(new RecordRef(WorkOrdersConstants.RecordKind, "open"));
            var doneTimers = await tracker.GetTimersAsync(new RecordRef(WorkOrdersConstants.RecordKind, "done"));

            Assert.Equal(2, openTimers.Count);
            Assert.Empty(doneTimers);

            var response = openTimers.Single(timer => timer.Metric == ServiceLevelMetrics.FirstResponse);
            var resolution = openTimers.Single(timer => timer.Metric == ServiceLevelMetrics.Resolution);

            Assert.Equal(ServiceLevelTimerState.Completed, response.State);
            Assert.NotNull(response.WarnedUtc);
            Assert.Equal(ServiceLevelTimerState.Running, resolution.State);
            Assert.Null(resolution.WarnedUtc);

            var workOrder = await services.GetRequiredService<ISession>()
                .Query<WorkOrder, WorkOrderIndex>(index => index.ItemId == "open", collection: WorkOrdersConstants.CollectionName)
                .FirstOrDefaultAsync();

            Assert.Equal(response.DueUtc, workOrder.Sla.RespondByUtc);
            Assert.Equal(resolution.DueUtc, workOrder.Sla.ResolveByUtc);
            Assert.True(workOrder.Sla.ResponseWarned);
        }, commit: false);
    }

    private static WorkOrder NewWorkOrder(string id, WorkOrderStatus status, DateTime createdUtc, Action<WorkOrderSla> configure)
    {
        var workOrder = new WorkOrder
        {
            ItemId = id,
            Number = "WO-" + id,
            Summary = "Boiler service",
            Status = status,
            Priority = WorkOrderPriority.High,
            CreatedUtc = createdUtc,
            Sla = new WorkOrderSla { PolicyId = PolicyId },
        };

        configure(workOrder.Sla);

        return workOrder;
    }

    // Writes the legacy index rows the upgrade reads, the way Work Orders wrote them before the move.
    private sealed class LegacyPolicyIndexProvider : IndexProvider<LegacyServiceLevelPolicy>
    {
        public LegacyPolicyIndexProvider()
        {
            CollectionName = WorkOrdersConstants.CollectionName;
        }

        public override void Describe(DescribeContext<LegacyServiceLevelPolicy> context)
        {
            context.For<ServiceLevelPolicyIndex>()
                .Map(policy => new ServiceLevelPolicyIndex { ItemId = policy.ItemId, Name = policy.Name });
        }
    }
}
