using CrestApps.OrchardCore.Ledgers;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.YesSql.Core.Ledgers;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Core.Ledgers;

/// <summary>
/// The shared quantity ledger: entries posted once by key, balances that leave out entries not started or expired,
/// sums over a period, and two ledgers kept apart in their own collections.
/// </summary>
public sealed class QuantityLedgerTests
{
    private static readonly DateOnly _today = new(2026, 11, 2);

    [Fact]
    public async Task PostAsync_KeepsOneEntryPerKey_InTheSameSessionAndAfterTheCommit()
    {
        // Arrange
        await using var harness = await CreateAsync();

        // Act
        var (first, again) = await harness.RunAsync(async services =>
        {
            var ledger = services.GetRequiredService<IQuantityLedger<CreditLedger>>();
            var one = await ledger.PostAsync(Entry("ana", "ceu", 2, _today, key: "course-1"));
            var two = await ledger.PostAsync(Entry("ana", "ceu", 2, _today, key: "course-1"));

            return (one, two);
        });
        var later = await harness.RunAsync(services => services.GetRequiredService<IQuantityLedger<CreditLedger>>().PostAsync(Entry("ana", "ceu", 5, _today, key: "course-1")));
        var entries = await harness.RunAsync(services => services.GetRequiredService<IQuantityLedger<CreditLedger>>().ListAsync("ana", "ceu"), commit: false);

        // Assert
        Assert.True(first.Created);
        Assert.False(again.Created);
        Assert.False(later.Created);
        Assert.Equal(first.Entry.ItemId, later.Entry.ItemId);
        Assert.Equal(2, Assert.Single(entries).Amount);
    }

    [Fact]
    public async Task GetBalanceAsync_CountsEntriesStartedAndNotExpired_AndSumAsyncCountsAPeriod()
    {
        // Arrange
        await using var harness = await CreateAsync();

        await harness.RunAsync(async services =>
        {
            var ledger = services.GetRequiredService<IQuantityLedger<CreditLedger>>();

            await ledger.PostAsync(Entry("ana", "ceu", 3, _today.AddMonths(-14), expiresOn: _today.AddMonths(-2)));
            await ledger.PostAsync(Entry("ana", "ceu", 4, _today.AddMonths(-6)));
            await ledger.PostAsync(Entry("ana", "ceu", -1, _today.AddMonths(-1)));
            await ledger.PostAsync(Entry("ana", "ceu", 6, _today.AddDays(10)));
            await ledger.PostAsync(Entry("ana", "pdu", 9, _today.AddMonths(-1)));
            await ledger.PostAsync(Entry("ben", "ceu", 7, _today.AddMonths(-1)));
        });

        // Act
        var (balance, lastYear, account) = await harness.RunAsync(async services =>
        {
            var ledger = services.GetRequiredService<IQuantityLedger<CreditLedger>>();

            return (
                await ledger.GetBalanceAsync("ana", "ceu", _today),
                await ledger.SumAsync("ana", "ceu", _today.AddYears(-1), _today),
                await ledger.ListForAccountAsync("ana"));
        }, commit: false);

        // Assert
        Assert.Equal(3, balance);
        Assert.Equal(3, lastYear);
        Assert.Equal(5, account.Count);
    }

    [Fact]
    public async Task TwoLedgers_AreKeptApart()
    {
        // Arrange
        await using var harness = await CreateAsync();

        // Act
        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<IQuantityLedger<CreditLedger>>().PostAsync(Entry("ana", "x", 1, _today, key: "same"));
            await services.GetRequiredService<IQuantityLedger<SeatLedger>>().PostAsync(Entry("ana", "x", 10, _today, key: "same"));
        });

        var (credits, seats) = await harness.RunAsync(async services => (
            await services.GetRequiredService<IQuantityLedger<CreditLedger>>().GetBalanceAsync("ana", "x", _today),
            await services.GetRequiredService<IQuantityLedger<SeatLedger>>().GetBalanceAsync("ana", "x", _today)), commit: false);

        // Assert
        Assert.Equal(1, credits);
        Assert.Equal(10, seats);
    }

    private static QuantityLedgerEntry Entry(string account, string bucket, decimal amount, DateOnly date, DateOnly? expiresOn = null, string key = null)
        => new() { AccountId = account, Bucket = bucket, Kind = "Earned", Amount = amount, EffectiveDate = date, ExpiresOn = expiresOn, IdempotencyKey = key };

    private static Task<CalendarTestHarness> CreateAsync()
        => CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddQuantityLedger<CreditLedger>(options => options.CollectionName = "TestCredits");
                services.AddQuantityLedger<SeatLedger>(options => options.CollectionName = "TestSeats");
            },
            async schemaBuilder =>
            {
                await QuantityLedgerIndexSchema.CreateAsync(schemaBuilder, "TestCredits");
                await QuantityLedgerIndexSchema.CreateAsync(schemaBuilder, "TestSeats");
            },
            [new QuantityLedgerEntryIndexProvider("TestCredits"), new QuantityLedgerEntryIndexProvider("TestSeats")],
            ["TestCredits", "TestSeats"]);

    private sealed class CreditLedger
    {
    }

    private sealed class SeatLedger
    {
    }
}
