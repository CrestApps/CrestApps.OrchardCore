using CrestApps.OrchardCore.Omnichannel.Messaging.Email;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Migrations;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// Runs the deliverability storage against a real database: one sending state per address and one suppression per
/// address, the migrations that make it so, the delivery log's prune and the suppression list's removal.
/// </summary>
public sealed class EmailDeliverabilitySqliteTests
{
    private const string Collection = EmailChannelConstants.DeliverabilityCollectionName;
    private const string TablePrefix = "tenant_";

    [Fact]
    public async Task CreateAsync_MakesTheSendingStatesAddressUnique()
    {
        await WithStoreAsync(async store =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await SeedStatesAsync(store, new EmailSendingState { ItemId = "first", AddressId = "address-1" });

            await using var session = store.CreateSession();
            await session.SaveAsync(new EmailSendingState { ItemId = "second", AddressId = "address-1" }, collection: Collection, cancellationToken: cancellationToken);

            await Assert.ThrowsAnyAsync<Exception>(() => session.SaveChangesAsync(cancellationToken));
        });
    }

    [Fact]
    public async Task UpdateFrom1Async_KeepsTheOldestSendingStateOfEachAddressAndMakesTheAddressUnique()
    {
        await WithStoreAsync(
            async store =>
            {
                var cancellationToken = TestContext.Current.CancellationToken;

                // What version 1 allowed: two processes each created a record for one address.
                await SeedStatesAsync(store, new EmailSendingState { ItemId = "first", AddressId = "address-1" });
                await SeedStatesAsync(
                    store,
                    new EmailSendingState { ItemId = "second", AddressId = "address-1" },
                    new EmailSendingState { ItemId = "other", AddressId = "address-2" });

                await using (var session = store.CreateSession())
                {
                    var transaction = await session.BeginTransactionAsync(cancellationToken);
                    var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
                    var migration = CreateMigration(store, schemaBuilder);

                    Assert.Equal(2, await migration.UpdateFrom1Async());

                    // A second app instance activating the tenant at the same time finds the work done.
                    Assert.Equal(2, await migration.UpdateFrom1Async());
                    Assert.True(await IndexTableIndexCreation.ExistsAsync(schemaBuilder, typeof(EmailSendingStateIndex), EmailDeliverabilityMigrations.SendingStateAddressUniqueIndexName, Collection));

                    await transaction.CommitAsync(cancellationToken);
                }

                await using var check = store.CreateSession();
                var states = (await check.Query<EmailSendingState>(collection: Collection).ListAsync(cancellationToken))
                    .Select(state => state.ItemId)
                    .Order(StringComparer.Ordinal)
                    .ToArray();

                Assert.Equal(["first", "other"], states);
            },
            uniqueIndex: false);
    }

    [Fact]
    public async Task CreateAsync_MakesTheSuppressionAddressUnique()
    {
        await WithStoreAsync(async store =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await SeedSuppressionsAsync(store, Suppression("s-1", "ann@example.com", EmailSuppressionReason.HardBounce));

            await using var session = store.CreateSession();
            await session.SaveAsync(Suppression("s-2", "ann@example.com", EmailSuppressionReason.Complaint), collection: Collection, cancellationToken: cancellationToken);

            await Assert.ThrowsAnyAsync<Exception>(() => session.SaveChangesAsync(cancellationToken));
        });
    }

    [Fact]
    public async Task UpdateFrom2Async_KeepsTheOldestSuppressionOfEachAddressAndMakesTheAddressUnique()
    {
        await WithStoreAsync(
            async store =>
            {
                var cancellationToken = TestContext.Current.CancellationToken;

                // What version 2 allowed: two reports suppressed one address at the same moment.
                await SeedSuppressionsAsync(store, Suppression("s-1", "ann@example.com", EmailSuppressionReason.HardBounce));
                await SeedSuppressionsAsync(
                    store,
                    Suppression("s-2", "ann@example.com", EmailSuppressionReason.Complaint),
                    Suppression("s-3", "bob@example.com", EmailSuppressionReason.Manual));

                await using (var session = store.CreateSession())
                {
                    var transaction = await session.BeginTransactionAsync(cancellationToken);
                    var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
                    var migration = CreateMigration(store, schemaBuilder);

                    Assert.Equal(3, await migration.UpdateFrom2Async());

                    // A second app instance activating the tenant at the same time finds the work done.
                    Assert.Equal(3, await migration.UpdateFrom2Async());
                    Assert.True(await IndexTableIndexCreation.ExistsAsync(schemaBuilder, typeof(EmailSuppressionIndex), EmailDeliverabilityMigrations.SuppressionAddressUniqueIndexName, Collection));

                    await transaction.CommitAsync(cancellationToken);
                }

                await using var check = store.CreateSession();
                var suppressions = (await check.Query<EmailSuppression>(collection: Collection).ListAsync(cancellationToken))
                    .OrderBy(suppression => suppression.ItemId, StringComparer.Ordinal)
                    .ToArray();

                Assert.Equal(["s-1", "s-3"], suppressions.Select(suppression => suppression.ItemId).ToArray());
                Assert.Equal(EmailSuppressionReason.HardBounce, suppressions[0].Reason);
            },
            uniqueIndex: false);
    }

    [Fact]
    public async Task FindFreshAsync_MergesAnAddressesDuplicateStatesIntoTheOldest()
    {
        await WithStoreAsync(
            async store =>
            {
                var cancellationToken = TestContext.Current.CancellationToken;
                var throttledUntil = new DateTime(2026, 10, 9, 12, 15, 0, DateTimeKind.Utc);
                var blockedUntil = new DateTime(2026, 10, 9, 13, 0, 0, DateTimeKind.Utc);

                await SeedStatesAsync(store, new EmailSendingState { ItemId = "first", AddressId = "address-1", PauseKind = EmailSendingPauseKind.Throttled, PausedUntilUtc = throttledUntil, ConsecutiveThrottles = 1 });

                var second = new EmailSendingState { ItemId = "second", AddressId = "address-1", PauseKind = EmailSendingPauseKind.Blocked, PausedUntilUtc = blockedUntil, ConsecutiveBlocks = 2 };
                second.DomainBackoffs["gmail.com"] = blockedUntil;
                await SeedStatesAsync(store, second);

                await using (var session = store.CreateSession())
                {
                    var stateStore = new EmailSendingStateStore(session);

                    Assert.Equal("first", (await stateStore.FindAsync("address-1", cancellationToken)).ItemId);

                    var state = await stateStore.FindFreshAsync("address-1", cancellationToken);

                    Assert.Equal("first", state.ItemId);
                    Assert.Equal(EmailSendingPauseKind.Blocked, state.PauseKind);
                    Assert.Equal(blockedUntil, state.PausedUntilUtc);
                    Assert.Equal(1, state.ConsecutiveThrottles);
                    Assert.Equal(2, state.ConsecutiveBlocks);
                    Assert.Equal(blockedUntil, state.DomainBackoffs["gmail.com"]);

                    await session.SaveChangesAsync(cancellationToken);
                }

                await using var check = store.CreateSession();
                var stored = Assert.Single(await check.Query<EmailSendingState>(collection: Collection).ListAsync(cancellationToken));

                Assert.Equal("first", stored.ItemId);
                Assert.Equal(EmailSendingPauseKind.Blocked, stored.PauseKind);
            },
            uniqueIndex: false);
    }

    [Fact]
    public async Task FindFreshAsync_BuildsOnAChangeAnotherScopeCommittedSince()
    {
        await WithStoreAsync(async store =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var blockedUntil = new DateTime(2026, 10, 9, 13, 0, 0, DateTimeKind.Utc);
            var nextTurn = new DateTime(2026, 10, 9, 12, 30, 0, DateTimeKind.Utc);

            await SeedStatesAsync(store, new EmailSendingState { ItemId = "first", AddressId = "address-1" });

            await using var sending = store.CreateSession();
            var sendingStore = new EmailSendingStateStore(sending);

            // This scope reads the state, then a provider's report blocks the address in another scope.
            Assert.NotNull(await sendingStore.FindAsync("address-1", cancellationToken));
            await sending.SaveChangesAsync(cancellationToken);

            await using (var webhook = store.CreateSession())
            {
                var webhookStore = new EmailSendingStateStore(webhook);
                var blocked = await webhookStore.FindFreshAsync("address-1", cancellationToken);

                blocked.PauseKind = EmailSendingPauseKind.Blocked;
                blocked.PausedUntilUtc = blockedUntil;
                await webhookStore.SaveAsync(blocked, cancellationToken);
                await webhook.SaveChangesAsync(cancellationToken);
            }

            // Act: this scope changes the state it read before the block.
            var state = await sendingStore.FindFreshAsync("address-1", cancellationToken);
            state.NextBulkSlotUtc = nextTurn;
            await sendingStore.SaveAsync(state, cancellationToken);
            await sending.SaveChangesAsync(cancellationToken);

            // Assert: one record, holding both changes.
            await using var check = store.CreateSession();
            var stored = Assert.Single(await check.Query<EmailSendingState>(collection: Collection).ListAsync(cancellationToken));

            Assert.Equal(EmailSendingPauseKind.Blocked, stored.PauseKind);
            Assert.Equal(blockedUntil, stored.PausedUntilUtc);
            Assert.Equal(nextTurn, stored.NextBulkSlotUtc);
        });
    }

    [Fact]
    public async Task PruneAsync_DeletesTheOldestEntriesAndTheirDocumentsInBatches()
    {
        await WithStoreAsync(async store =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var now = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
            var cutoff = now.AddDays(-30);

            await using (var seed = store.CreateSession())
            {
                var seedLog = new EmailDeliveryLog(seed);

                for (var i = 0; i < 3; i++)
                {
                    await seedLog.RecordAsync(Entry($"old-{i}", now.AddDays(-40).AddMinutes(i)), cancellationToken);
                }

                for (var i = 0; i < 2; i++)
                {
                    await seedLog.RecordAsync(Entry($"new-{i}", now.AddDays(-1).AddMinutes(i)), cancellationToken);
                }

                await seed.SaveChangesAsync(cancellationToken);
            }

            // Act
            var deleted = new List<int>();

            await using (var session = store.CreateSession())
            {
                var log = new EmailDeliveryLog(session);

                for (var i = 0; i < 3; i++)
                {
                    deleted.Add(await log.PruneAsync(cutoff, 2, cancellationToken));
                    await session.SaveChangesAsync(cancellationToken);
                }
            }

            // Assert
            Assert.Equal([2, 1, 0], deleted);

            await using var check = store.CreateSession();
            var remaining = (await check.Query<EmailDeliveryLogEntry>(collection: Collection).ListAsync(cancellationToken))
                .Select(entry => entry.ItemId)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var longAgo = cutoff.AddYears(-10);
            var rows = await check.QueryIndex<EmailDeliveryLogIndex>(index => index.OccurredUtc > longAgo, collection: Collection).CountAsync(cancellationToken);

            Assert.Equal(["new-0", "new-1"], remaining);
            Assert.Equal(2, rows);
        });
    }

    [Fact]
    public async Task Counts_LeaveUnconfirmedReportsOutOfSuppressionAndDomainBackoff()
    {
        await WithStoreAsync(async store =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var now = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

            // One confirmed soft bounce and two that were tied to their email only by the recipient.
            await using (var seed = store.CreateSession())
            {
                var seedLog = new EmailDeliveryLog(seed);

                await seedLog.RecordAsync(Bounce("confirmed", "m-1", now.AddMinutes(-3), isUnconfirmed: false), cancellationToken);
                await seedLog.RecordAsync(Bounce("forged-1", null, now.AddMinutes(-2), isUnconfirmed: true), cancellationToken);
                await seedLog.RecordAsync(Bounce("forged-2", null, now.AddMinutes(-1), isUnconfirmed: true), cancellationToken);
                await seed.SaveChangesAsync(cancellationToken);
            }

            // Act
            await using var session = store.CreateSession();
            var log = new EmailDeliveryLog(session);
            var since = now.AddDays(-1);

            var forRecipient = await log.CountMessagesForRecipientAsync("ann@example.com", EmailDeliveryEventKind.SoftBounce, since, cancellationToken);
            var forDomain = await log.CountAsync("address-1", EmailDeliveryEventKind.SoftBounce, since, "example.com", cancellationToken);
            var forAddress = await log.CountAsync("address-1", EmailDeliveryEventKind.SoftBounce, since, cancellationToken: cancellationToken);

            // Assert: only the confirmed one counts toward suppressing Ann or backing off her domain, and all three show
            // in the address's own counts.
            Assert.Equal(1, forRecipient);
            Assert.Equal(1, forDomain);
            Assert.Equal(3, forAddress);
        });

        static EmailDeliveryLogEntry Bounce(string itemId, string messageId, DateTime occurredUtc, bool isUnconfirmed)
            => new()
            {
                ItemId = itemId,
                AddressId = "address-1",
                Kind = EmailDeliveryEventKind.SoftBounce,
                Recipient = "ann@example.com",
                RecipientDomain = "example.com",
                MessageId = messageId,
                OccurredUtc = occurredUtc,
                IsUnconfirmed = isUnconfirmed,
            };
    }

    [Fact]
    public async Task RemoveAsync_TakesEveryRecordOfTheAddressOffTheList()
    {
        await WithStoreAsync(async store =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            // A list kept before the address was made unique holds two records of it.
            await using (var seed = store.CreateSession())
            {
                await seed.SaveAsync(new EmailSuppression { ItemId = "s-1", Address = "ann@example.com", Reason = EmailSuppressionReason.HardBounce, CreatedUtc = DateTime.UtcNow }, collection: Collection, cancellationToken: cancellationToken);
                await seed.SaveAsync(new EmailSuppression { ItemId = "s-2", Address = "ann@example.com", Reason = EmailSuppressionReason.Complaint, CreatedUtc = DateTime.UtcNow }, collection: Collection, cancellationToken: cancellationToken);
                await seed.SaveChangesAsync(cancellationToken);
            }

            // Act
            bool removed;

            await using (var session = store.CreateSession())
            {
                removed = await CreateSuppressionList(session).RemoveAsync("Ann@Example.com", cancellationToken);
                await session.SaveChangesAsync(cancellationToken);
            }

            // Assert
            await using var check = store.CreateSession();

            Assert.True(removed);
            Assert.Null(await CreateSuppressionList(check).FindAsync("ann@example.com", cancellationToken));
            Assert.Empty(await check.Query<EmailSuppression>(collection: Collection).ListAsync(cancellationToken));
        },
        uniqueIndex: false);
    }

    private static EmailSuppression Suppression(string itemId, string address, EmailSuppressionReason reason)
        => new()
        {
            ItemId = itemId,
            Address = address,
            Reason = reason,
            CreatedUtc = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc),
        };

    private static async Task SeedSuppressionsAsync(IStore store, params EmailSuppression[] suppressions)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var session = store.CreateSession();

        foreach (var suppression in suppressions)
        {
            await session.SaveAsync(suppression, collection: Collection, cancellationToken: cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    private static EmailDeliveryLogEntry Entry(string itemId, DateTime occurredUtc)
        => new()
        {
            ItemId = itemId,
            AddressId = "address-1",
            Kind = EmailDeliveryEventKind.Sent,
            Recipient = "ann@example.com",
            RecipientDomain = "example.com",
            OccurredUtc = occurredUtc,
        };

    private static EmailSuppressionList CreateSuppressionList(ISession session)
        => new(session, new DeliverabilityClock(), NullLogger<EmailSuppressionList>.Instance);

    private static EmailDeliverabilityMigrations CreateMigration(IStore store, SchemaBuilder schemaBuilder)
        => new(store)
        {
            SchemaBuilder = schemaBuilder,
        };

    private static async Task SeedStatesAsync(IStore store, params EmailSendingState[] states)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var session = store.CreateSession();

        foreach (var state in states)
        {
            await session.SaveAsync(state, collection: Collection, cancellationToken: cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    // A store with the deliverability tables as the migration creates them; without the unique indexes, as version 1
    // left them, when asked.
    private static async Task WithStoreAsync(Func<IStore, Task> test, bool uniqueIndex = true)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var databasePath = Path.Combine(Path.GetTempPath(), $"email-deliverability-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration
            .UseSqLite($"Data Source={databasePath};Pooling=False")
            .SetTablePrefix(TablePrefix));

        try
        {
            store.RegisterIndexes([new EmailDeliveryLogIndexProvider(), new EmailSuppressionIndexProvider(), new EmailSendingStateIndexProvider()], Collection);
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(Collection, cancellationToken);

            await using (var session = store.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(cancellationToken);
                var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

                Assert.Equal(4, await CreateMigration(store, schemaBuilder).CreateAsync());

                if (!uniqueIndex)
                {
                    await schemaBuilder.AlterIndexTableAsync<EmailSendingStateIndex>(
                        table => table.DropIndex(EmailDeliverabilityMigrations.SendingStateAddressUniqueIndexName),
                        collection: Collection);
                    await schemaBuilder.AlterIndexTableAsync<EmailSuppressionIndex>(
                        table => table.DropIndex(EmailDeliverabilityMigrations.SuppressionAddressUniqueIndexName),
                        collection: Collection);
                }

                await transaction.CommitAsync(cancellationToken);
            }

            await test(store);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }
}
