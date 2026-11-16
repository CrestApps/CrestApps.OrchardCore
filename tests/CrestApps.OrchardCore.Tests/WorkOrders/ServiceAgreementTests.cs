using CrestApps.Core;
using CrestApps.Core.Handlers;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Agreements.Core.Indexes;
using CrestApps.OrchardCore.Agreements.Core.Services;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Legacy;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NodaTime;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;
using IClock = OrchardCore.Modules.IClock;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// Agreements with visits (their Work Orders terms) make the work orders of their visits ahead of time, once each, on the
/// right days across daylight saving changes, and none past their end; their terms are checked by the agreement's own
/// rules; their billable labor counts against the agreement's hours; and the service agreements Work Orders kept move to
/// the Agreements module with the same ids, numbers and terms.
/// </summary>
public sealed class ServiceAgreementTests
{
    private static readonly DateTimeZone _newYork = DateTimeZoneProviders.Tzdb["America/New_York"];

    [Fact]
    public void Occurrences_AreTheDaysInTheLeadTime_AndStopAtTheEnd()
    {
        var agreement = new Agreement { StartDate = new DateOnly(2026, 1, 15) };
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
        var agreement = new Agreement { StartDate = new DateOnly(2027, 1, 1) };
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

        var agreementId = await setup.Harness.RunAsync(services => CreateAgreementAsync(services, setup.TypeId, (agreement, terms) =>
        {
            terms.Schedules = [new AgreementSchedule { Id = "weekly", Name = "Filter check", TypeId = setup.TypeId, Rule = "FREQ=WEEKLY;BYDAY=MO", LeadDays = 14 }];
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
    public async Task AnAgreementPastItsEnd_OrNotActive_MakesNoWorkOrders()
    {
        await using var setup = await SetupAsync();

        await setup.Harness.RunAsync(services => CreateAgreementAsync(services, setup.TypeId, (agreement, terms) =>
        {
            agreement.EndDate = new DateOnly(2026, 10, 31);
            terms.Schedules = [new AgreementSchedule { Id = "weekly", TypeId = setup.TypeId, Rule = "FREQ=WEEKLY;BYDAY=MO", LeadDays = 14 }];
        }));
        await setup.Harness.RunAsync(services => CreateAgreementAsync(services, setup.TypeId, (agreement, terms) =>
        {
            agreement.Status = AgreementStatus.Paused;
            terms.Schedules = [new AgreementSchedule { Id = "weekly", TypeId = setup.TypeId, Rule = "FREQ=WEEKLY;BYDAY=MO", LeadDays = 14 }];
        }));

        var made = await setup.Harness.RunAsync(services => services.GetRequiredService<ServiceAgreementScheduler>().RunAsync());

        Assert.Equal(0, made);
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
            var manager = services.GetRequiredService<IAgreementManager>();
            var agreement = await manager.NewAsync();

            agreement.Name = "Broken";
            agreement.Status = AgreementStatus.Active;
            agreement.EndDate = agreement.StartDate.AddDays(-1);
            agreement.Put(new WorkOrderAgreementTerms
            {
                Pricing = AgreementPricing.PerVisit,
                Schedules = [new AgreementSchedule { Id = "x", TypeId = setup.TypeId, Rule = "FREQ=SOMETIMES" }],
            });

            var result = await manager.ValidateAsync(agreement);

            Assert.False(result.Succeeded);
            Assert.Equal(3, result.Errors.Count);
        });
    }

    [Fact]
    public async Task AScheduleWithItsOwnAssets_UsesThem_AndTheOthersTheAgreements()
    {
        await using var setup = await SetupAsync();

        await setup.Harness.RunAsync(services => CreateAgreementAsync(services, setup.TypeId, (agreement, terms) =>
        {
            terms.AssetIds = ["boiler", "pump"];
            terms.Schedules =
            [
                new AgreementSchedule { Id = "pump", Name = "Pump check", TypeId = setup.TypeId, Rule = "FREQ=YEARLY", LeadDays = 0, AssetIds = ["pump"] },
                new AgreementSchedule { Id = "all", Name = "Full service", TypeId = setup.TypeId, Rule = "FREQ=YEARLY", LeadDays = 0 },
            ];
            agreement.StartDate = new DateOnly(2026, 11, 2);
        }));

        await setup.Harness.RunAsync(services => services.GetRequiredService<ServiceAgreementScheduler>().RunAsync());

        var assets = await setup.Harness.RunAsync(async services => (await services.GetRequiredService<WorkOrderStore>().GetAllAsync())
            .ToDictionary(item => item.Summary.Split(' ')[0], item => item.AssetIds.ToArray()), commit: false);

        Assert.Equal(["pump"], assets["Pump"]);
        Assert.Equal(["boiler", "pump"], assets["Full"]);
    }

    [Fact]
    public async Task TheBillableLaborOfTheAgreementsCompletedWorkOrders_CountsAgainstItsHours()
    {
        // Arrange (a 10-hour block; a completed work order with 1.5 hours of billable labor and half an hour that isn't)
        await using var setup = await SetupAsync();
        var agreementId = await setup.Harness.RunAsync(services => CreateAgreementAsync(services, setup.TypeId, (agreement, _) =>
        {
            agreement.AccountId = "contoso";
            agreement.Hours = new AgreementHours { Kind = AgreementHoursKind.Block, Hours = 10 };
        }));
        await setup.Harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(new WorkOrder
            {
                ItemId = UniqueId.GenerateId(),
                Number = "WO-1",
                Summary = "Boiler repair",
                AccountId = "contoso",
                AgreementId = agreementId,
                Status = WorkOrderStatus.Completed,
                CompletedUtc = setup.Now.AddHours(-2),
                Lines =
                [
                    new WorkOrderLine { Kind = WorkOrderLineKind.Labor, QuantityUsed = 1.5m, Billable = true },
                    new WorkOrderLine { Kind = WorkOrderLineKind.Labor, QuantityUsed = 0.5m, Billable = false },
                    new WorkOrderLine { Kind = WorkOrderLineKind.Product, QuantityUsed = 3, Billable = true },
                ],
            }, false, WorkOrdersConstants.CollectionName);

            return true;
        });

        // Act
        var balance = await setup.Harness.RunAsync(async services =>
        {
            var agreement = await services.GetRequiredService<IAgreementManager>().FindByIdAsync(agreementId);

            return await services.GetRequiredService<IAgreementBalanceService>().GetBalanceAsync(agreement);
        }, commit: false);

        // Assert
        Assert.Equal(90, balance.UsedMinutes);
        var entry = Assert.Single(balance.Entries);
        Assert.Equal("WO-1 Boiler repair", entry.Usage.Description);
    }

    [Fact]
    public async Task TheServiceAgreementsWorkOrdersKept_MoveToTheAgreementsModule_WithTheirIdsNumbersAndTerms()
    {
        // Arrange (an agreement stored as Work Orders kept it)
        await using var setup = await SetupAsync(legacy: true);
        await setup.Harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            session.Store.TypeNames[typeof(LegacyServiceAgreement)] = ServiceAgreementUpgrade.StoredTypeName;
            await session.SaveAsync(new LegacyServiceAgreement
            {
                ItemId = "4legacyagreement0000000001",
                Number = "SA-00007",
                Name = "Furnace care",
                Status = "Active",
                AccountId = "contoso",
                CustomerId = "customer-1",
                Contact = new WorkOrderContact { Name = "Pat" },
                StartDate = new DateOnly(2026, 1, 1),
                TimeZoneId = "America/New_York",
                Pricing = AgreementPricing.PerVisit,
                PricePerVisit = 120,
                Schedules = [new AgreementSchedule { Id = "yearly", TypeId = setup.TypeId, Rule = "FREQ=YEARLY", LeadDays = 14 }],
            }, false, WorkOrdersConstants.CollectionName);

            return true;
        });

        // Act
        var first = await setup.Harness.RunAsync(services => services.GetRequiredService<ServiceAgreementUpgrade>().RunAsync());
        var again = await setup.Harness.RunAsync(services => services.GetRequiredService<ServiceAgreementUpgrade>().RunAsync());
        var moved = await setup.Harness.RunAsync(async services => await services.GetRequiredService<IAgreementManager>().FindByIdAsync("4legacyagreement0000000001"), commit: false);

        // Assert
        Assert.Equal(1, first);
        Assert.Equal(0, again);
        Assert.Equal("SA-00007", moved.Number);
        Assert.Equal(AgreementStatus.Active, moved.Status);
        Assert.Equal("contoso", moved.AccountId);
        Assert.True(moved.TryGet<WorkOrderAgreementTerms>(out var terms));
        Assert.Equal("customer-1", terms.CustomerId);
        Assert.Equal("Pat", terms.Contact.Name);
        Assert.Equal(120, terms.PricePerVisit);
        Assert.Equal("yearly", Assert.Single(terms.Schedules).Id);
    }

    private static async Task<string> CreateAgreementAsync(IServiceProvider services, string typeId, Action<Agreement, WorkOrderAgreementTerms> configure)
    {
        var manager = services.GetRequiredService<IAgreementManager>();
        var agreement = await manager.NewAsync();
        var terms = new WorkOrderAgreementTerms { CustomerId = "customer-1", Contact = new WorkOrderContact { Name = "Pat" } };

        agreement.Name = "Furnace care";
        agreement.Status = AgreementStatus.Active;
        agreement.StartDate = new DateOnly(2026, 10, 1);
        agreement.TimeZoneId = "America/New_York";
        configure(agreement, terms);
        agreement.Put(terms);

        var validation = await manager.ValidateAsync(agreement);
        Assert.True(validation.Succeeded, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));
        await manager.CreateAsync(agreement);

        return agreement.ItemId;
    }

    private static async Task<Setup> SetupAsync(bool legacy = false)
    {
        var setup = new Setup();
        var clock = new Mock<IClock>();

        clock.SetupGet(c => c.UtcNow).Returns(() => setup.Now);

        setup.Harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddSingleton(clock.Object);
                services.AddWorkOrdersWithServiceLevels();
                services.AddServiceAgreementsCore();
                services.AddScoped<ICatalogEntryHandler<Agreement>, AgreementHandler>();
                services.AddSingleton<INumberSequence, InMemoryNumberSequence>();
            },
            async builder =>
            {
                await WorkOrderTestStores.CreateSchemaAsync(builder);
                await AgreementsIndexSchema.CreateAsync(builder);

                if (legacy)
                {
                    await builder.CreateMapIndexTableAsync<ServiceAgreementIndex>(table => table.Column<string>("ItemId", column => column.WithLength(26)), collection: WorkOrdersConstants.CollectionName);
                }
            },
            [.. WorkOrderTestStores.IndexProviders, new AgreementIndexProvider(), .. legacy ? new IIndexProvider[] { new LegacyServiceAgreementIndexProvider() } : []],
            [.. WorkOrderTestStores.Collections, AgreementsConstants.CollectionName]);

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

    // Writes the index row Work Orders wrote for its own agreements.
    private sealed class LegacyServiceAgreementIndexProvider : IndexProvider<LegacyServiceAgreement>
    {
        public LegacyServiceAgreementIndexProvider()
        {
            CollectionName = WorkOrdersConstants.CollectionName;
        }

        public override void Describe(DescribeContext<LegacyServiceAgreement> context)
            => context.For<ServiceAgreementIndex>().Map(item => new ServiceAgreementIndex { ItemId = item.ItemId });
    }

    private sealed class Setup : IAsyncDisposable
    {
        public CalendarTestHarness Harness { get; set; }

        public string TypeId { get; set; }

        public DateTime Now { get; set; } = CalendarTestHarness.Now;

        public ValueTask DisposeAsync() => Harness?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
