using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NodaTime;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// Service agreements make the work orders of their visits ahead of time, once each, on the right days across daylight
/// saving changes; expire past their end; start the expiring event once; and bill as agreed.
/// </summary>
public sealed class ServiceAgreementTests
{
    private static readonly DateTimeZone _newYork = DateTimeZoneProviders.Tzdb["America/New_York"];

    [Fact]
    public void Occurrences_AreTheDaysInTheLeadTime_AndStopAtTheEnd()
    {
        var agreement = new ServiceAgreement { StartDate = new DateOnly(2026, 1, 15) };
        var schedule = new AgreementSchedule { Rule = "FREQ=MONTHLY" };

        Assert.Equal(
            [new LocalDate(2026, 11, 15), new LocalDate(2026, 12, 15)],
            ServiceAgreementScheduler.Occurrences(agreement, schedule, _newYork, new LocalDate(2026, 11, 2), new LocalDate(2026, 12, 31)));

        agreement.EndDate = new DateOnly(2026, 12, 1);

        Assert.Equal([new LocalDate(2026, 11, 15)], ServiceAgreementScheduler.Occurrences(agreement, schedule, _newYork, new LocalDate(2026, 11, 2), new LocalDate(2026, 12, 31)));
    }

    [Fact]
    public void AVisitsDay_FollowsDaylightSaving()
    {
        var agreement = new ServiceAgreement { StartDate = new DateOnly(2027, 1, 1) };
        var days = ServiceAgreementScheduler.Occurrences(agreement, new AgreementSchedule { Rule = "FREQ=MONTHLY;BYDAY=2SU" }, _newYork, new LocalDate(2027, 2, 1), new LocalDate(2027, 4, 30)).ToList();

        // The second Sunday of March is the day the clocks go forward.
        Assert.Equal([new LocalDate(2027, 2, 14), new LocalDate(2027, 3, 14), new LocalDate(2027, 4, 11)], days);
        Assert.Equal(new DateTime(2027, 2, 14, 5, 0, 0, DateTimeKind.Utc), ServiceAgreementScheduler.DayOf(days[0], _newYork).StartUtc);
        Assert.Equal(new DateTime(2027, 3, 15, 4, 0, 0, DateTimeKind.Utc), ServiceAgreementScheduler.DayOf(days[1], _newYork).EndUtc);
        Assert.Equal(new DateTime(2027, 4, 11, 4, 0, 0, DateTimeKind.Utc), ServiceAgreementScheduler.DayOf(days[2], _newYork).StartUtc);
    }

    [Fact]
    public async Task TheWorkOrdersOfTheVisits_AreMadeOnce_AheadOfTime()
    {
        await using var setup = await SetupAsync();

        var agreementId = await setup.Harness.RunAsync(services => CreateAgreementAsync(services, setup.TypeId, agreement =>
        {
            agreement.Schedules = [new AgreementSchedule { Id = "weekly", Name = "Filter check", TypeId = setup.TypeId, Rule = "FREQ=WEEKLY;BYDAY=MO", LeadDays = 14 }];
        }));

        // Monday 2 November 2026: this Monday and the next two are within 14 days.
        var first = await setup.Harness.RunAsync(services => services.GetRequiredService<ServiceAgreementScheduler>().RunAsync());
        var second = await setup.Harness.RunAsync(services => services.GetRequiredService<ServiceAgreementScheduler>().RunAsync());

        Assert.Equal(3, first);
        Assert.Equal(0, second);

        await setup.Harness.RunAsync(async services =>
        {
            var workOrders = (await services.GetRequiredService<WorkOrderStore>().GetAllAsync()).OrderBy(item => item.EarliestUtc).ToList();

            Assert.Equal(3, workOrders.Count);
            Assert.All(workOrders, item =>
            {
                Assert.Equal(WorkOrderSource.Agreement, item.Source);
                Assert.Equal(agreementId, item.AgreementId);
                Assert.Equal(0m, item.AgreedPrice);
                Assert.Equal("Pat", item.Contact.Name);
                Assert.StartsWith("Filter check (SA-00001)", item.Summary, StringComparison.Ordinal);
            });

            // 2 November is after the clocks went back, so the day starts at 5 AM UTC.
            Assert.Equal(new DateTime(2026, 11, 2, 5, 0, 0, DateTimeKind.Utc), workOrders[0].EarliestUtc);
            Assert.Equal(new DateTime(2026, 11, 16, 5, 0, 0, DateTimeKind.Utc), workOrders[2].EarliestUtc);
        });
    }

    [Fact]
    public async Task AnAgreement_StartsTheExpiringEventOnce_AndExpiresAfterItsEnd()
    {
        await using var setup = await SetupAsync();

        var agreementId = await setup.Harness.RunAsync(services => CreateAgreementAsync(services, setup.TypeId, agreement =>
        {
            agreement.EndDate = new DateOnly(2026, 11, 20);
            agreement.Schedules = [new AgreementSchedule { Id = "yearly", TypeId = setup.TypeId, Rule = "FREQ=YEARLY", LeadDays = 0 }];
        }));

        await setup.Harness.RunAsync(services => services.GetRequiredService<ServiceAgreementScheduler>().RunAsync());
        await setup.Harness.RunAsync(services => services.GetRequiredService<ServiceAgreementScheduler>().RunAsync());

        Assert.Equal(["SA-00001"], setup.Expiring);

        setup.Now = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
        await setup.Harness.RunAsync(services => services.GetRequiredService<ServiceAgreementScheduler>().RunAsync());

        await setup.Harness.RunAsync(async services =>
        {
            var agreement = await services.GetRequiredService<ServiceAgreementManager>().FindByIdAsync(agreementId);

            Assert.Equal(ServiceAgreementStatus.Expired, agreement.Status);
        });
    }

    [Fact]
    public void AnAgreedPrice_ReplacesTheTypesBilling()
    {
        var type = new WorkOrderType { BillingMode = WorkOrderBillingMode.TimeAndMaterials };
        var workOrder = new WorkOrder { Lines = [new WorkOrderLine { Kind = WorkOrderLineKind.Labor, QuantityEstimated = 2, UnitPrice = 90, Billable = true }] };

        Assert.Equal(180m, WorkOrderService.BillableAmount(workOrder, type));

        workOrder.AgreedPrice = 0m;
        Assert.Equal(0m, WorkOrderService.BillableAmount(workOrder, type));

        workOrder.AgreedPrice = 120m;
        Assert.Equal(120m, WorkOrderService.BillableAmount(workOrder, new WorkOrderType { BillingMode = WorkOrderBillingMode.NotBillable }));
    }

    [Fact]
    public async Task AnAgreement_IsCheckedBeforeItIsSaved()
    {
        await using var setup = await SetupAsync();

        await setup.Harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ServiceAgreementManager>();
            var agreement = await manager.NewAsync();

            agreement.Name = "Broken";
            agreement.Status = ServiceAgreementStatus.Active;
            agreement.EndDate = agreement.StartDate.AddDays(-1);
            agreement.Pricing = AgreementPricing.PerVisit;
            agreement.Schedules = [new AgreementSchedule { Id = "x", TypeId = setup.TypeId, Rule = "FREQ=SOMETIMES" }];

            var result = await manager.ValidateAsync(agreement);

            Assert.False(result.Succeeded);
            Assert.Equal(3, result.Errors.Count);
        });
    }

    private static async Task<string> CreateAgreementAsync(IServiceProvider services, string typeId, Action<ServiceAgreement> configure)
    {
        var manager = services.GetRequiredService<ServiceAgreementManager>();
        var agreement = await manager.NewAsync();

        agreement.Name = "Furnace care";
        agreement.Status = ServiceAgreementStatus.Active;
        agreement.StartDate = new DateOnly(2026, 10, 1);
        agreement.TimeZoneId = "America/New_York";
        agreement.CustomerId = "customer-1";
        agreement.Contact = new WorkOrderContact { Name = "Pat" };
        configure(agreement);
        await manager.CreateAsync(agreement);

        return agreement.ItemId;
    }

    private static async Task<Setup> SetupAsync()
    {
        var setup = new Setup();
        var clock = new Mock<IClock>();
        var observer = new Mock<IServiceAgreementObserver>();

        clock.SetupGet(c => c.UtcNow).Returns(() => setup.Now);
        observer.Setup(o => o.OnExpiringAsync(It.IsAny<ServiceAgreement>(), It.IsAny<CancellationToken>()))
            .Callback((ServiceAgreement agreement, CancellationToken _) => setup.Expiring.Add(agreement.Number))
            .Returns(Task.CompletedTask);

        setup.Harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddSingleton(clock.Object);
                services.AddSingleton(observer.Object);
                services.AddWorkOrdersCore();
                services.AddServiceAgreementsCore();
            },
            async builder =>
            {
                await WorkOrdersIndexSchema.CreateAsync(builder);
                await ServiceAgreementsIndexSchema.CreateAsync(builder);
            },
            [new WorkOrderIndexProvider(), new WorkOrderTypeIndexProvider(), new ChecklistTemplateIndexProvider(), new ServiceLevelPolicyIndexProvider(), new WorkOrderSubStatusIndexProvider(), new ServiceAgreementIndexProvider()],
            [WorkOrdersConstants.CollectionName]);

        setup.TypeId = await setup.Harness.RunAsync(async services =>
        {
            var types = services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>();
            var type = await types.NewAsync();

            type.Name = "Furnace service";
            type.BillingMode = WorkOrderBillingMode.FixedPrice;
            type.FixedPrice = 150;
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

        public List<string> Expiring { get; } = [];

        public ValueTask DisposeAsync() => Harness?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
