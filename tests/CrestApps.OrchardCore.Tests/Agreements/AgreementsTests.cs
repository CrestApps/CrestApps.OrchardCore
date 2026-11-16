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
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Agreements;

/// <summary>
/// Agreements: numbered from a locked sequence and checked for the editor and recipes alike; the balance adds up every
/// usage provider's time in the period (the whole agreement for a block, the month for an allowance), each entry rounded
/// up as the agreement says, with what passes the cover as overage; the life cycle expires agreements past their end,
/// tells observers once when one is about to end, and once a period when its used hours pass 80% and 100%.
/// </summary>
public sealed class AgreementsTests
{
    // Monday 16 November 2026, noon in New York.
    private static readonly DateTime _noon = new(2026, 11, 16, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AnAgreement_IsNumbered_AndItsNameHoursAndRoundingAreChecked()
    {
        // Arrange
        await using var kit = await Kit.CreateAsync();

        // Act
        var first = await kit.CreateAsync(agreement => agreement.Name = "Gold support");
        var second = await kit.CreateAsync(agreement => agreement.Name = "Silver support");
        var errors = await kit.Harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IAgreementManager>();
            var agreement = await manager.NewAsync();

            agreement.Hours = new AgreementHours { Kind = AgreementHoursKind.Block, Hours = 0, RoundingMinutes = 7 };
            agreement.EndDate = agreement.StartDate.AddDays(-1);

            return (await manager.ValidateAsync(agreement)).Errors.Select(error => error.ErrorMessage).ToList();
        });

        // Assert
        Assert.Equal("SA-00001", first.Number);
        Assert.Equal("SA-00002", second.Number);
        Assert.Equal(4, errors.Count);
    }

    [Fact]
    public async Task ABlocksBalance_AddsUpEveryProvidersTime_RoundedUp_WithTheOverage()
    {
        // Arrange (10 hours; entries of 50, 7 and 600 minutes, rounded to 15: 60 + 15 + 600 = 675)
        await using var kit = await Kit.CreateAsync();
        var agreement = await kit.CreateAsync(item =>
        {
            item.Name = "Gold support";
            item.Status = AgreementStatus.Active;
            item.StartDate = new DateOnly(2026, 11, 1);
            item.Hours = new AgreementHours { Kind = AgreementHoursKind.Block, Hours = 10, RoundingMinutes = 15 };
        });
        kit.Usage.Add((agreement.ItemId, _noon.AddDays(-10), 50));
        kit.Usage.Add((agreement.ItemId, _noon.AddDays(-5), 7));
        kit.Usage.Add((agreement.ItemId, _noon.AddDays(-1), 600));
        kit.Usage.Add((agreement.ItemId, _noon.AddDays(-30), 90));

        // Act
        var balance = await kit.Harness.RunAsync(services => services.GetRequiredService<IAgreementBalanceService>().GetBalanceAsync(agreement), commit: false);

        // Assert
        Assert.Equal(new DateOnly(2026, 11, 1), balance.PeriodStart);
        Assert.Null(balance.PeriodEnd);
        Assert.Equal(600, balance.CoveredMinutes);
        Assert.Equal(675, balance.UsedMinutes);
        Assert.Equal(0, balance.LeftMinutes);
        Assert.Equal(75, balance.OverageMinutes);
        Assert.Equal([60d, 15d, 600d], balance.Entries.Select(entry => entry.RoundedMinutes));
    }

    [Fact]
    public async Task AMonthlyAllowance_CountsTheCalendarMonth_InTheAgreementsTimeZone()
    {
        // Arrange (5 hours a month in New York: 1 November 3:00 UTC is still 31 October there)
        await using var kit = await Kit.CreateAsync();
        var agreement = await kit.CreateAsync(item =>
        {
            item.Name = "Monthly support";
            item.Status = AgreementStatus.Active;
            item.StartDate = new DateOnly(2026, 1, 1);
            item.TimeZoneId = "America/New_York";
            item.Hours = new AgreementHours { Kind = AgreementHoursKind.MonthlyAllowance, Hours = 5 };
        });
        kit.Usage.Add((agreement.ItemId, new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc), 120));
        kit.Usage.Add((agreement.ItemId, new DateTime(2026, 11, 2, 15, 0, 0, DateTimeKind.Utc), 45));

        // Act
        var november = await kit.Harness.RunAsync(services => services.GetRequiredService<IAgreementBalanceService>().GetBalanceAsync(agreement), commit: false);
        var october = await kit.Harness.RunAsync(services => services.GetRequiredService<IAgreementBalanceService>().GetBalanceAsync(agreement, new DateOnly(2026, 10, 15)), commit: false);

        // Assert
        Assert.Equal(new DateOnly(2026, 11, 1), november.PeriodStart);
        Assert.Equal(new DateOnly(2026, 11, 30), november.PeriodEnd);
        Assert.Equal(300, november.CoveredMinutes);
        Assert.Equal(45, november.UsedMinutes);
        Assert.Equal(120, october.UsedMinutes);
    }

    [Fact]
    public async Task TheLifecycle_WarnsAt80And100PercentOnce_TellsOfTheEndOnce_AndExpiresPastTheEnd()
    {
        // Arrange (10 hours; 8.5 hours used; it ends in 20 days, with 30 days' notice)
        await using var kit = await Kit.CreateAsync();
        var agreement = await kit.CreateAsync(item =>
        {
            item.Name = "Gold support";
            item.Status = AgreementStatus.Active;
            item.StartDate = new DateOnly(2026, 11, 1);
            item.EndDate = new DateOnly(2026, 12, 5);
            item.ExpiringNoticeDays = 30;
            item.Hours = new AgreementHours { Kind = AgreementHoursKind.Block, Hours = 10 };
        });
        kit.Usage.Add((agreement.ItemId, _noon.AddDays(-2), 510));

        // Act
        await kit.RunLifecycleAsync();
        await kit.RunLifecycleAsync();
        kit.Usage.Add((agreement.ItemId, _noon.AddDays(-1), 120));
        await kit.RunLifecycleAsync();
        kit.Now = _noon.AddDays(25);
        await kit.RunLifecycleAsync();
        var expired = await kit.Harness.RunAsync(async services => await services.GetRequiredService<IAgreementManager>().FindByIdAsync(agreement.ItemId), commit: false);

        // Assert
        Assert.Equal(["Expiring", "Hours:80", "Hours:100"], kit.Told);
        Assert.Equal(AgreementStatus.Expired, expired.Status);
    }

    [Fact]
    public async Task TheAgreementsInForce_AreTheAccountsActiveOnesForTheDay()
    {
        // Arrange
        await using var kit = await Kit.CreateAsync();
        await kit.CreateAsync(item => { item.Name = "Current"; item.AccountId = "contoso"; item.Status = AgreementStatus.Active; item.StartDate = new DateOnly(2026, 1, 1); });
        await kit.CreateAsync(item => { item.Name = "Draft"; item.AccountId = "contoso"; item.Status = AgreementStatus.Draft; item.StartDate = new DateOnly(2026, 1, 1); });
        await kit.CreateAsync(item => { item.Name = "Later"; item.AccountId = "contoso"; item.Status = AgreementStatus.Active; item.StartDate = new DateOnly(2027, 1, 1); });
        await kit.CreateAsync(item => { item.Name = "Other"; item.AccountId = "fabrikam"; item.Status = AgreementStatus.Active; item.StartDate = new DateOnly(2026, 1, 1); });

        // Act
        var inForce = await kit.Harness.RunAsync(services => services.GetRequiredService<IAgreementBalanceService>().ListInForceAsync("contoso"), commit: false);

        // Assert
        Assert.Equal(["Current"], inForce.Select(agreement => agreement.Name));
    }

    private sealed class Kit : IAsyncDisposable
    {
        public CalendarTestHarness Harness { get; private set; }

        public DateTime Now { get; set; } = _noon;

        public List<(string AgreementId, DateTime StartUtc, double Minutes)> Usage { get; } = [];

        public List<string> Told { get; } = [];

        public static async Task<Kit> CreateAsync()
        {
            var kit = new Kit();
            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(() => kit.Now);

            var provider = new Mock<IAgreementUsageProvider>();
            provider.SetupGet(value => value.Name).Returns("Test");
            provider.Setup(value => value.ListAsync(It.IsAny<Agreement>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Agreement agreement, DateTime from, DateTime to, CancellationToken _) => kit.Usage
                    .Where(entry => entry.AgreementId == agreement.ItemId && entry.StartUtc >= from && entry.StartUtc < to)
                    .Select(entry => new AgreementUsage { StartUtc = entry.StartUtc, Minutes = entry.Minutes })
                    .ToList());

            var observer = new Mock<IAgreementObserver>();
            observer.Setup(value => value.OnExpiringAsync(It.IsAny<Agreement>(), It.IsAny<CancellationToken>()))
                .Callback(() => kit.Told.Add("Expiring"))
                .Returns(Task.CompletedTask);
            observer.Setup(value => value.OnHoursUsedAsync(It.IsAny<Agreement>(), It.IsAny<int>(), It.IsAny<AgreementBalance>(), It.IsAny<CancellationToken>()))
                .Callback((Agreement _, int percent, AgreementBalance _, CancellationToken _) => kit.Told.Add("Hours:" + percent))
                .Returns(Task.CompletedTask);

            kit.Harness = await CalendarTestHarness.CreateAsync(
                services =>
                {
                    services.AddAgreementsCore();
                    services.AddScoped<ICatalogEntryHandler<Agreement>, AgreementHandler>();
                    services.AddSingleton(clock.Object);
                    services.AddSingleton<INumberSequence, InMemoryNumberSequence>();
                    services.AddSingleton(provider.Object);
                    services.AddSingleton(observer.Object);
                },
                AgreementsIndexSchema.CreateAsync,
                [new AgreementIndexProvider()],
                [AgreementsConstants.CollectionName]);

            return kit;
        }

        public Task<Agreement> CreateAsync(Action<Agreement> configure)
            => Harness.RunAsync(async services =>
            {
                var manager = services.GetRequiredService<IAgreementManager>();
                var agreement = await manager.NewAsync();

                configure(agreement);

                var validation = await manager.ValidateAsync(agreement);
                Assert.True(validation.Succeeded, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));
                await manager.CreateAsync(agreement);

                return agreement;
            });

        public Task<int> RunLifecycleAsync()
            => Harness.RunAsync(services => services.GetRequiredService<AgreementLifecycle>().RunAsync());

        public ValueTask DisposeAsync()
            => Harness.DisposeAsync();
    }
}
