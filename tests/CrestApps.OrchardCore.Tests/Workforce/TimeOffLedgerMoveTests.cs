using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Migrations;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The upgrade that moves time-off entries from the Workforce collection to the time-off quantity ledger: every entry
/// keeps its id, worker, policy, kind, amount, dates and key, and balances read the same afterwards.
/// </summary>
public sealed class TimeOffLedgerMoveTests
{
    [Fact]
    public async Task MoveLedgerEntriesAsync_MovesEveryEntryWithItsIdAndFields_AndBalancesReadThem()
    {
        // Arrange
        await using var harness = await TimeOffKit.CreateAsync();
        var opening = new TimeOffLedgerEntry
        {
            ItemId = "entry-opening",
            WorkerId = "worker-1",
            PolicyId = "policy-1",
            Kind = TimeOffLedgerKind.Opening,
            Amount = 40,
            EffectiveDate = new DateOnly(2026, 1, 1),
            IdempotencyKey = "opening:worker-1:policy-1",
            Actor = "admin",
            Note = "Carried over",
            CreatedUtc = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
        };
        var grant = new TimeOffLedgerEntry
        {
            ItemId = "entry-grant",
            WorkerId = "worker-1",
            PolicyId = "policy-1",
            Kind = TimeOffLedgerKind.Grant,
            Amount = 8,
            EffectiveDate = new DateOnly(2026, 6, 1),
            ExpiresOn = new DateOnly(2026, 12, 31),
            CreatedUtc = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
        };

        // The entries as an install before the move kept them: documents of the Workforce collection.
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();

            await session.SaveAsync(opening, checkConcurrency: false, collection: WorkforceConstants.CollectionName);
            await session.SaveAsync(grant, checkConcurrency: false, collection: WorkforceConstants.CollectionName);
        });

        // Act
        var moved = await harness.RunAsync(services => TimeOffMigrations.MoveLedgerEntriesAsync(services.GetRequiredService<ISession>()));
        var (entries, grants, byKey, left) = await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<TimeOffLedgerStore>();
            var session = services.GetRequiredService<ISession>();

            return (
                await store.ListAsync("worker-1", "policy-1"),
                await store.ListGrantsAsync(),
                await store.FindByKeyAsync("opening:worker-1:policy-1"),
                await session.Query<TimeOffLedgerEntry>(collection: WorkforceConstants.CollectionName).ListAsync());
        }, commit: false);

        // Assert
        Assert.Equal(2, moved);
        Assert.Empty(left);
        Assert.Equal(["entry-opening", "entry-grant"], entries.Select(entry => entry.ItemId));
        Assert.Equal(TimeOffLedgerKind.Opening, entries[0].Kind);
        Assert.Equal(40, entries[0].Amount);
        Assert.Equal("Carried over", entries[0].Note);
        Assert.Equal(new DateOnly(2026, 12, 31), entries[1].ExpiresOn);
        Assert.Equal("entry-grant", Assert.Single(grants).ItemId);
        Assert.Equal("entry-opening", byKey?.ItemId);
    }
}
