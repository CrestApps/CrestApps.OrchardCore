using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The upgrade that copies time-off entries from the Workforce collection to the time-off quantity ledger: every entry
/// keeps its id, worker, policy, kind, amount, dates and key, balances read the same afterwards, the originals stay for
/// nodes of the earlier release, and copying again adds nothing twice.
/// </summary>
public sealed class TimeOffLedgerMoveTests
{
    [Fact]
    public async Task CopyAsync_CopiesEveryEntryWithItsIdAndFields_KeepsTheOriginals_AndCopiesOnce()
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
        var copied = await harness.RunAsync(services => services.GetRequiredService<TimeOffLedgerCopy>().CopyAsync());
        var copiedAgain = await harness.RunAsync(services => services.GetRequiredService<TimeOffLedgerCopy>().CopyAsync());
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
        Assert.Equal(2, copied);
        Assert.Equal(0, copiedAgain);
        Assert.Equal(2, left.Count);
        Assert.Equal(["entry-opening", "entry-grant"], entries.Select(entry => entry.ItemId));
        Assert.Equal(TimeOffLedgerKind.Opening, entries[0].Kind);
        Assert.Equal(40, entries[0].Amount);
        Assert.Equal("Carried over", entries[0].Note);
        Assert.Equal(new DateOnly(2026, 12, 31), entries[1].ExpiresOn);
        Assert.Equal("entry-grant", Assert.Single(grants).ItemId);
        Assert.Equal("entry-opening", byKey?.ItemId);
    }

    [Fact]
    public async Task AnInterruptedCopy_IsNotComplete_HoldsAccrualBack_AndTheNextCopyFinishesWithoutDuplicates()
    {
        // Arrange: review round 5, finding 1. Three old entries, and a copy that stops after its first batch.
        await using var harness = await TimeOffKit.CreateAsync();
        await SaveOldAsync(harness, Entry("e1", "k1"), Entry("e2", "k2"), Entry("e3", "k3"));

        // Act
        var interrupted = await Record.ExceptionAsync(() => harness.RunAsync(async services =>
        {
            var copy = services.GetRequiredService<TimeOffLedgerCopy>();

            copy.AfterBatch = _ => throw new InvalidOperationException("The process stopped.");

            return await copy.CopyAsync(batchSize: 1);
        }));
        var completeAfterInterruption = await harness.RunAsync(services => services.GetRequiredService<TimeOffLedgerCopy>().IsCompleteAsync());
        var accrualWhileIncomplete = await harness.RunAsync(services => services.GetRequiredService<ITimeOffAccrualService>().RunAsync(new DateOnly(2026, 11, 2)));
        var finished = await harness.RunAsync(services => services.GetRequiredService<TimeOffLedgerCopy>().CopyAsync(batchSize: 1));
        var complete = await harness.RunAsync(services => services.GetRequiredService<TimeOffLedgerCopy>().IsCompleteAsync());
        var entries = await LedgerAsync(harness);

        // Assert
        Assert.NotNull(interrupted);
        Assert.False(completeAfterInterruption);
        Assert.Equal(0, accrualWhileIncomplete.Entries);
        Assert.Equal(2, finished);
        Assert.True(complete);
        Assert.Equal(["e1", "e2", "e3"], entries.Select(entry => entry.ItemId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AnEntryWhoseKeyIsAlreadyInTheLedger_IsNotCopied()
    {
        // Arrange: an accrual posted again under another id while the ledger was empty.
        await using var harness = await TimeOffKit.CreateAsync();
        await SaveOldAsync(harness, Entry("old-accrual", "accrual:worker-1:policy-1:20261031"), Entry("old-grant", "grant:1"));
        await harness.RunAsync(async services =>
        {
            CrestApps.OrchardCore.Ledgers.QuantityLedgerEntry quantity = TimeOffLedgerMapping.ToQuantity(Entry("reposted-accrual", "accrual:worker-1:policy-1:20261031"));

            await services.GetRequiredService<ISession>().SaveAsync(quantity, checkConcurrency: false, collection: WorkforceConstants.TimeOffLedgerCollectionName);
        });

        // Act
        var copied = await harness.RunAsync(services => services.GetRequiredService<TimeOffLedgerCopy>().CopyAsync());
        var entries = await LedgerAsync(harness);

        // Assert
        Assert.Equal(1, copied);
        Assert.Equal(["old-grant", "reposted-accrual"], entries.Select(entry => entry.ItemId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ACopyAtTheNextStart_PicksUpEntriesWrittenToTheOldCollectionMeanwhile()
    {
        // Arrange
        await using var harness = await TimeOffKit.CreateAsync();
        await SaveOldAsync(harness, Entry("e1", "k1"));
        await harness.RunAsync(services => services.GetRequiredService<TimeOffLedgerCopy>().CopyAsync());

        // A node of the earlier release writes another entry to the old collection.
        await SaveOldAsync(harness, Entry("e2", "k2"));

        // Act
        var copied = await harness.RunAsync(services => services.GetRequiredService<TimeOffLedgerCopy>().CopyAsync());
        var entries = await LedgerAsync(harness);

        // Assert
        Assert.Equal(1, copied);
        Assert.Equal(["e1", "e2"], entries.Select(entry => entry.ItemId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task TwoNodesStartingTogether_CopyEachEntryOnce()
    {
        // Arrange: review round 6, item 1. Three old entries.
        await using var harness = await TimeOffKit.CreateAsync();
        await SaveOldAsync(harness, Entry("e1", "k1"), Entry("e2", "k2"), Entry("e3", "k3"));

        // Act: one node holds the copy lock while another starts; then two copies start together.
        var whileHeld = await harness.RunAsync(async services =>
        {
            var locks = services.GetRequiredService<global::OrchardCore.Locking.Distributed.IDistributedLock>();
            var (locker, locked) = await locks.TryAcquireLockAsync(TimeOffLedgerCopy.LockKey, TimeSpan.FromSeconds(1));

            Assert.True(locked);

            await using (locker)
            {
                return await harness.RunAsync(other => other.GetRequiredService<TimeOffLedgerCopy>().CopyAsync());
            }
        });
        var together = await Task.WhenAll(
            harness.RunAsync(services => services.GetRequiredService<TimeOffLedgerCopy>().CopyAsync(batchSize: 1)),
            harness.RunAsync(services => services.GetRequiredService<TimeOffLedgerCopy>().CopyAsync(batchSize: 1)));
        var entries = await LedgerAsync(harness);

        // Assert
        Assert.Equal(0, whileHeld);
        Assert.Equal(3, together.Sum());
        Assert.Equal(["e1", "e2", "e3"], entries.Select(entry => entry.ItemId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ACopyOutOfTime_StopsBeforeItsLockExpires_AndTheNextStartFinishes()
    {
        // Arrange: review round 6, item 1. A copy that may run for no time at all stops after its first batch.
        await using var harness = await TimeOffKit.CreateAsync();
        await SaveOldAsync(harness, Entry("e1", "k1"), Entry("e2", "k2"), Entry("e3", "k3"));

        // Act
        var first = await harness.RunAsync(async services =>
        {
            var copy = services.GetRequiredService<TimeOffLedgerCopy>();

            copy.MaxRunTime = TimeSpan.Zero;

            return await copy.CopyAsync(batchSize: 1);
        });
        var completeAfterFirst = await harness.RunAsync(services => services.GetRequiredService<TimeOffLedgerCopy>().IsCompleteAsync());
        var lockFree = await harness.RunAsync(services => services.GetRequiredService<global::OrchardCore.Locking.Distributed.IDistributedLock>().IsLockAcquiredAsync(TimeOffLedgerCopy.LockKey));
        var rest = await harness.RunAsync(services => services.GetRequiredService<TimeOffLedgerCopy>().CopyAsync(batchSize: 1));
        var entries = await LedgerAsync(harness);

        // Assert
        Assert.Equal(1, first);
        Assert.False(completeAfterFirst);
        Assert.False(lockFree);
        Assert.Equal(2, rest);
        Assert.Equal(["e1", "e2", "e3"], entries.Select(entry => entry.ItemId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AnInstallWithNothingToCopy_IsComplete()
    {
        // Arrange
        await using var harness = await TimeOffKit.CreateAsync();

        // Act
        var complete = await harness.RunAsync(services => services.GetRequiredService<TimeOffLedgerCopy>().IsCompleteAsync());

        // Assert
        Assert.True(complete);
    }

    private static TimeOffLedgerEntry Entry(string id, string key)
        => new()
        {
            ItemId = id,
            WorkerId = "worker-1",
            PolicyId = "policy-1",
            Kind = TimeOffLedgerKind.Accrual,
            Amount = 8,
            EffectiveDate = new DateOnly(2026, 10, 31),
            IdempotencyKey = key,
            CreatedUtc = new DateTime(2026, 10, 31, 9, 0, 0, DateTimeKind.Utc),
        };

    private static Task SaveOldAsync(CrestApps.OrchardCore.Tests.Calendars.CalendarTestHarness harness, params TimeOffLedgerEntry[] entries)
        => harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();

            foreach (var entry in entries)
            {
                await session.SaveAsync(entry, checkConcurrency: false, collection: WorkforceConstants.CollectionName);
            }
        });

    private static Task<IReadOnlyList<TimeOffLedgerEntry>> LedgerAsync(CrestApps.OrchardCore.Tests.Calendars.CalendarTestHarness harness)
        => harness.RunAsync(services => services.GetRequiredService<TimeOffLedgerStore>().ListAsync("worker-1", "policy-1"), commit: false);
}
