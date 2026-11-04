using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// Work orders take their type's defaults and a number, move only along the status rules, keep the service level
/// clock (stopping while they wait), can't be completed until their checklist, signature and photos are in, bill their
/// lines, and take parts off a stock location.
/// </summary>
public sealed class WorkOrderTests
{
    [Fact]
    public void TheStatusRules_AllowOnlyTheUsualMoves()
    {
        Assert.True(WorkOrderStatusRules.CanMove(WorkOrderStatus.New, WorkOrderStatus.InProgress));
        Assert.True(WorkOrderStatusRules.CanMove(WorkOrderStatus.Completed, WorkOrderStatus.InProgress));
        Assert.False(WorkOrderStatusRules.CanMove(WorkOrderStatus.New, WorkOrderStatus.Closed));
        Assert.False(WorkOrderStatusRules.CanMove(WorkOrderStatus.Canceled, WorkOrderStatus.New));
        Assert.Empty(WorkOrderStatusRules.Next(WorkOrderStatus.Closed));
    }

    [Fact]
    public void BusinessMinutes_SkipTheHoursClosed()
    {
        // Open 9 to 5 on Friday and Monday; 2 hours from Friday 4 pm end at Monday 10 am.
        var friday = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
        var open = new[] { (friday.AddHours(9), friday.AddHours(17)), (friday.AddDays(3).AddHours(9), friday.AddDays(3).AddHours(17)) };
        var remaining = TimeSpan.FromHours(2);

        Assert.Equal(friday.AddDays(3).AddHours(10), SlaClock.Add(open, friday.AddHours(16), ref remaining));

        var tooMuch = TimeSpan.FromHours(30);

        Assert.Null(SlaClock.Add(open, friday.AddHours(9), ref tooMuch));
        Assert.Equal(TimeSpan.FromHours(14), tooMuch);
    }

    [Fact]
    public async Task WeeklyHours_CountOnlyTheOpenHours_InTheirTimeZone()
    {
        var clock = new SlaClock([]);
        var policy = new ServiceLevelPolicy { WeeklyHours = new WeeklyHours { Days = WeeklyHours.Weekdays() }, TimeZoneId = "America/New_York" };

        // Friday 4 pm in New York (9 pm UTC), 2 working hours later is Monday 10 am in New York (3 pm UTC).
        var due = await clock.DueAsync(new DateTime(2026, 11, 6, 21, 0, 0, DateTimeKind.Utc), 120, policy, TestContext.Current.CancellationToken);

        Assert.Equal(new DateTime(2026, 11, 9, 15, 0, 0, DateTimeKind.Utc), due);
    }

    [Fact]
    public async Task EachKindOfSettingsRecord_IsListedApart_AndReadBackAsSaved()
    {
        await using var setup = await SetupAsync();

        await setup.Harness.RunAsync(async services =>
        {
            var policies = await services.GetRequiredService<WorkOrderSettingManager<ServiceLevelPolicy>>().GetAllAsync();
            var policy = Assert.Single(policies);

            Assert.Equal("Standard", policy.Name);
            Assert.Equal("Safety check", Assert.Single(await services.GetRequiredService<WorkOrderSettingManager<ChecklistTemplate>>().GetAllAsync()).Name);
            Assert.Equal("Boiler service", Assert.Single(await services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>().GetAllAsync()).Name);
            Assert.Empty(await services.GetRequiredService<WorkOrderSettingManager<WorkOrderSubStatus>>().GetAllAsync());

            // A new policy stops the clock while on hold, once: defaults aren't read back twice.
            Assert.Equal([WorkOrderStatus.OnHold], policy.PauseStatuses);
        });
    }

    [Fact]
    public async Task ANewWorkOrder_TakesItsTypesDefaults_ANumber_AndItsTargets()
    {
        await using var setup = await SetupAsync();

        var first = await CreateAsync(setup, WorkOrderPriority.High);
        var second = await CreateAsync(setup, WorkOrderPriority.Normal);

        Assert.Equal("WO-000001", first.Number);
        Assert.Equal("WO-000002", second.Number);
        Assert.Equal(90, first.EstimatedMinutes);
        Assert.Equal(["Filter", "Labor"], first.Lines.Select(line => line.Description));
        Assert.Equal("Safety check", Assert.Single(first.Checklists).Name);
        Assert.Equal(setup.Now.AddHours(2), first.Sla.RespondByUtc);
        Assert.Equal(setup.Now.AddHours(8), first.Sla.ResolveByUtc);
        Assert.Equal(setup.Now.AddHours(4), second.Sla.RespondByUtc);
        Assert.Contains((WorkOrdersConstants.Events.Created, first.Number), setup.Events);
    }

    [Fact]
    public async Task Statuses_FollowTheRules_AndTheClockStopsWhileOnHold()
    {
        await using var setup = await SetupAsync();
        var workOrder = await CreateAsync(setup, WorkOrderPriority.High);

        Assert.Equal(["A work order can't move from New to Closed."], await ErrorsAsync(setup, service => service.ChangeStatusAsync(workOrder.ItemId, WorkOrderStatus.Closed, null, null, "dispatcher")));
        Assert.Equal(["Say why the work order is canceled."], await ErrorsAsync(setup, service => service.ChangeStatusAsync(workOrder.ItemId, WorkOrderStatus.Canceled, null, null, "dispatcher")));

        setup.Now = setup.Now.AddMinutes(30);
        await ChangeAsync(setup, workOrder.ItemId, WorkOrderStatus.InProgress);

        setup.Now = setup.Now.AddHours(1);
        await ChangeAsync(setup, workOrder.ItemId, WorkOrderStatus.OnHold);

        setup.Now = setup.Now.AddHours(3);
        var resumed = await ChangeAsync(setup, workOrder.ItemId, WorkOrderStatus.InProgress);

        Assert.Equal(workOrder.CreatedUtc.AddMinutes(30), resumed.Sla.FirstResponseUtc);
        Assert.Equal(3 * 3600, resumed.Sla.PausedSeconds);
        Assert.Equal(workOrder.Sla.ResolveByUtc.Value.AddHours(3), resumed.Sla.ResolveByUtc);
        Assert.Null(resumed.Sla.PausedSinceUtc);
    }

    [Fact]
    public async Task Completing_NeedsTheRequiredAnswers_AndBillsTheLines()
    {
        await using var setup = await SetupAsync();
        var workOrder = await CreateAsync(setup, WorkOrderPriority.Normal);

        await ChangeAsync(setup, workOrder.ItemId, WorkOrderStatus.InProgress);

        Assert.Equal(["Answer \"Gas pressure (mbar)\" on Safety check.", "The customer must sign."], await ErrorsAsync(setup, service => service.CompleteAsync(workOrder.ItemId, "tech")));

        var question = workOrder.Checklists[0].Questions[0];

        Assert.Equal(["\"Gas pressure (mbar)\" must be from 15 to 25."], await ErrorsAsync(setup, service => service.AnswerAsync(workOrder.ItemId, 0, question.Id, "40", "tech")));
        Assert.Empty(await ErrorsAsync(setup, service => service.AnswerAsync(workOrder.ItemId, 0, question.Id, "20", "tech")));
        Assert.Empty(await ErrorsAsync(setup, service => service.SignAsync(workOrder.ItemId, "Pat Customer", null, "tech")));
        Assert.Empty(await ErrorsAsync(setup, service => service.UsePartAsync(workOrder.ItemId, workOrder.Lines[0].Id, 2, "van-12", "tech")));

        var done = await setup.Harness.RunAsync(async services => (await services.GetRequiredService<WorkOrderService>().CompleteAsync(workOrder.ItemId, "tech")).WorkOrder);

        // Two filters at 15 and the estimated 1.5 hours of labor at 80.
        Assert.Equal(WorkOrderStatus.Completed, done.Status);
        Assert.Equal(150m, Assert.Single(setup.Invoiced).Amount);
        Assert.Equal("invoice-1", done.InvoiceTransactionId);
        Assert.Equal([("filter-item", "van-12", 2m)], setup.PartsUsed);
        Assert.Contains((WorkOrdersConstants.Events.Completed, done.Number), setup.Events);
    }

    [Fact]
    public async Task APartRefusedByInventory_IsNotRecorded()
    {
        await using var setup = await SetupAsync();
        var workOrder = await CreateAsync(setup, WorkOrderPriority.Normal);

        setup.RefuseParts = "Van 12 has only 1 available.";

        Assert.Equal(["Van 12 has only 1 available."], await ErrorsAsync(setup, service => service.UsePartAsync(workOrder.ItemId, workOrder.Lines[0].Id, 2, "van-12", "tech")));

        var stored = await setup.Harness.RunAsync(async services => await services.GetRequiredService<WorkOrderService>().FindAsync(workOrder.ItemId), commit: false);

        Assert.Equal(0, stored.Lines[0].QuantityUsed);
    }

    [Fact]
    public async Task TheMonitor_WarnsThenBreaches_OnceEach()
    {
        await using var setup = await SetupAsync();
        var workOrder = await CreateAsync(setup, WorkOrderPriority.High);

        Task<int> CheckAsync() => setup.Harness.RunAsync(services => services.GetRequiredService<SlaMonitor>().CheckAsync());

        // 2 hours to respond, warned at 80%: 96 minutes.
        setup.Now = workOrder.CreatedUtc.AddMinutes(95);
        Assert.Equal(0, await CheckAsync());

        setup.Now = workOrder.CreatedUtc.AddMinutes(97);
        Assert.Equal(1, await CheckAsync());
        Assert.Equal(0, await CheckAsync());

        setup.Now = workOrder.CreatedUtc.AddMinutes(121);
        Assert.Equal(1, await CheckAsync());
        Assert.Equal(0, await CheckAsync());

        Assert.Equal([("Response", WorkOrdersConstants.Events.SlaWarning), ("Response", WorkOrdersConstants.Events.SlaBreached)], setup.SlaEvents);
    }

    [Fact]
    public async Task AFollowUp_KeepsTheCustomerPlaceAndAssets()
    {
        await using var setup = await SetupAsync();
        var workOrder = await CreateAsync(setup, WorkOrderPriority.Normal);

        var followUp = await setup.Harness.RunAsync(async services => (await services.GetRequiredService<WorkOrderService>().FollowUpAsync(workOrder.ItemId, null, "tech")).WorkOrder);

        Assert.Equal(workOrder.ItemId, followUp.ParentWorkOrderId);
        Assert.Equal("C-1001", followUp.CustomerId);
        Assert.Equal(["asset-1"], followUp.AssetIds);
        Assert.Equal("Follow-up to WO-000001", followUp.Summary);
        Assert.Equal("WO-000002", followUp.Number);
    }

    private static Task<WorkOrder> ChangeAsync(Setup setup, string id, WorkOrderStatus status)
        => setup.Harness.RunAsync(async services =>
        {
            var result = await services.GetRequiredService<WorkOrderService>().ChangeStatusAsync(id, status, null, null, "dispatcher");

            Assert.Empty(result.Errors);

            return result.WorkOrder;
        });

    private static Task<IReadOnlyList<string>> ErrorsAsync(Setup setup, Func<WorkOrderService, Task<WorkOrderResult>> work)
        => setup.Harness.RunAsync(async services => (await work(services.GetRequiredService<WorkOrderService>())).Errors);

    private static Task<WorkOrder> CreateAsync(Setup setup, WorkOrderPriority priority)
        => setup.Harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<WorkOrderService>();
            var workOrder = await service.NewAsync(setup.TypeId);

            workOrder.Summary = "Boiler service";
            workOrder.Priority = priority;
            workOrder.CustomerId = "C-1001";
            workOrder.AssetIds = ["asset-1"];

            var result = await service.CreateAsync(workOrder, "dispatcher");

            Assert.Empty(result.Errors);

            return result.WorkOrder;
        });

    private static async Task<Setup> SetupAsync()
    {
        var setup = new Setup();
        var clock = new Mock<IClock>();
        var observer = new Mock<IWorkOrderObserver>();
        var parts = new Mock<IWorkOrderPartsService>();
        var billing = new Mock<IWorkOrderBilling>();

        clock.SetupGet(c => c.UtcNow).Returns(() => setup.Now);
        observer.Setup(o => o.OnEventAsync(It.IsAny<WorkOrder>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback((WorkOrder workOrder, string name, IReadOnlyDictionary<string, object> detail, CancellationToken _) =>
            {
                setup.Events.Add((name, workOrder.Number));

                if (detail.TryGetValue("Target", out var target))
                {
                    setup.SlaEvents.Add(((string)target, name));
                }
            })
            .Returns(Task.CompletedTask);
        parts.Setup(p => p.UseAsync(It.IsAny<WorkOrder>(), It.IsAny<WorkOrderLine>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkOrder _, WorkOrderLine line, decimal quantity, string _, CancellationToken _) =>
            {
                if (setup.RefuseParts is not null)
                {
                    return setup.RefuseParts;
                }

                setup.PartsUsed.Add((line.StockItemId, line.LocationId, quantity));

                return null;
            });
        billing.Setup(b => b.InvoiceAsync(It.IsAny<WorkOrder>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkOrder workOrder, decimal amount, string _, CancellationToken _) =>
            {
                setup.Invoiced.Add((workOrder.Number, amount));

                return "invoice-" + setup.Invoiced.Count;
            });

        setup.Harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddSingleton(clock.Object);
                services.AddSingleton(observer.Object);
                services.AddSingleton(parts.Object);
                services.AddSingleton(billing.Object);
                services.AddWorkOrdersCore();
            },
            WorkOrdersIndexSchema.CreateAsync,
            [new WorkOrderIndexProvider(), new WorkOrderTypeIndexProvider(), new ChecklistTemplateIndexProvider(), new ServiceLevelPolicyIndexProvider(), new WorkOrderSubStatusIndexProvider()],
            [WorkOrdersConstants.CollectionName]);

        setup.TypeId = await setup.Harness.RunAsync(async services =>
        {
            var policies = services.GetRequiredService<WorkOrderSettingManager<ServiceLevelPolicy>>();
            var policy = await policies.NewAsync();

            policy.Name = "Standard";
            policy.Targets =
            [
                new ServiceLevelTarget { Priority = WorkOrderPriority.High, ResponseMinutes = 120, ResolveMinutes = 480 },
                new ServiceLevelTarget { Priority = WorkOrderPriority.Normal, ResponseMinutes = 240, ResolveMinutes = 1440 },
            ];
            await policies.CreateAsync(policy);

            var checklists = services.GetRequiredService<WorkOrderSettingManager<ChecklistTemplate>>();
            var checklist = await checklists.NewAsync();

            checklist.Name = "Safety check";
            checklist.Questions = [new ChecklistQuestion { Id = "pressure", Text = "Gas pressure (mbar)", Kind = ChecklistQuestionKind.Number, Required = true, Min = 15, Max = 25 }];
            await checklists.CreateAsync(checklist);

            var types = services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>();
            var type = await types.NewAsync();

            type.Name = "Boiler service";
            type.DefaultDurationMinutes = 90;
            type.BillingMode = WorkOrderBillingMode.TimeAndMaterials;
            type.RequiresSignature = true;
            type.ServiceLevelPolicyId = policy.ItemId;
            type.ChecklistTemplateIds = [checklist.ItemId];
            type.DefaultLines =
            [
                new WorkOrderLine { Kind = WorkOrderLineKind.Product, Description = "Filter", StockItemId = "filter-item", QuantityEstimated = 1, UnitPrice = 15 },
                new WorkOrderLine { Kind = WorkOrderLineKind.Labor, Description = "Labor", QuantityEstimated = 1.5m, UnitPrice = 80 },
            ];
            await types.CreateAsync(type);

            return type.ItemId;
        });

        return setup;
    }

    private sealed class Setup : IAsyncDisposable
    {
        public CalendarTestHarness Harness { get; set; }

        public string TypeId { get; set; }

        public DateTime Now { get; set; } = CalendarTestHarness.Now;

        public string RefuseParts { get; set; }

        public List<(string Name, string Number)> Events { get; } = [];

        public List<(string Target, string Name)> SlaEvents { get; } = [];

        public List<(string StockItemId, string LocationId, decimal Quantity)> PartsUsed { get; } = [];

        public List<(string Number, decimal Amount)> Invoiced { get; } = [];

        public ValueTask DisposeAsync() => Harness?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
