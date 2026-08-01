using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Exercises retention against a real database rather than a mocked store, because the property that matters
/// is which rows survive. A policy whose predicate is subtly wrong still deletes a plausible number of rows,
/// so counting deletions proves nothing; only inspecting the survivors does.
/// </summary>
public sealed class ContactCenterRetentionPersistenceTests
{
    private static readonly DateTime _nowUtc = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PurgeAsync_DeletesSettledExpiredRecords_AndLeavesLiveAndRecentOnesAlone()
    {
        // Arrange
        var databasePath = DatabasePath("survivors");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var session = store.CreateSession())
            {
                await SeedAsync(session);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using (var session = store.CreateSession())
            {
                var report = await CreateService(session).PurgeAsync(TestContext.Current.CancellationToken);

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);

                Assert.False(report.WorkRemains);
            }

            // Assert
            await using (var session = store.CreateSession())
            {
                var queueItems = await session.Query<QueueItem, QueueItemIndex>(collection: ContactCenterConstants.CollectionName).ListAsync(TestContext.Current.CancellationToken);
                var commands = await session.Query<ProviderCommand, ProviderCommandIndex>(collection: ContactCenterConstants.CollectionName).ListAsync(TestContext.Current.CancellationToken);
                var interactions = await session.Query<Interaction, InteractionIndex>(collection: ContactCenterConstants.CollectionName).ListAsync(TestContext.Current.CancellationToken);

                Assert.Equal(
                    ["queue-live", "queue-recent"],
                    queueItems.Select(item => item.ItemId).OrderBy(id => id, StringComparer.Ordinal));

                Assert.Equal(
                    ["command-live", "command-recent"],
                    commands.Select(command => command.ItemId).OrderBy(id => id, StringComparer.Ordinal));

                Assert.Equal(
                    ["interaction-live", "interaction-recent"],
                    interactions.Select(interaction => interaction.ItemId).OrderBy(id => id, StringComparer.Ordinal));
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task PurgeAsync_DrainsABacklogLargerThanOneBatch_InASingleCycle()
    {
        // Arrange
        var databasePath = DatabasePath("drain");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var session = store.CreateSession())
            {
                for (var i = 0; i < 250; i++)
                {
                    await session.SaveAsync(
                        ExpiredQueueItem($"queue-{i.ToString("D4", CultureInfo.InvariantCulture)}"),
                        collection: ContactCenterConstants.CollectionName,
                        cancellationToken: TestContext.Current.CancellationToken);
                }

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using (var session = store.CreateSession())
            {
                var report = await CreateService(session, batchSize: 25).PurgeAsync(TestContext.Current.CancellationToken);

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(250, report.Entities.Single(entity => entity.EntityName == "QueueItem").PurgedCount);
                Assert.False(report.WorkRemains);
            }

            await using (var verification = store.CreateSession())
            {
                var remaining = await verification.Query<QueueItem, QueueItemIndex>(collection: ContactCenterConstants.CollectionName).CountAsync(TestContext.Current.CancellationToken);

                Assert.Equal(0, remaining);
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task PurgeAsync_WhenTheBudgetIsTooSmallForTheBacklog_SaysSoInsteadOfLookingSuccessful()
    {
        // Arrange
        var databasePath = DatabasePath("budget");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var session = store.CreateSession())
            {
                for (var i = 0; i < 120; i++)
                {
                    await session.SaveAsync(
                        ExpiredQueueItem($"queue-{i.ToString("D4", CultureInfo.InvariantCulture)}"),
                        collection: ContactCenterConstants.CollectionName,
                        cancellationToken: TestContext.Current.CancellationToken);
                }

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using (var session = store.CreateSession())
            {
                var report = await CreateService(session, batchSize: 25, maxBatches: 2).PurgeAsync(TestContext.Current.CancellationToken);

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(50, report.TotalPurged);
                Assert.True(report.WorkRemains);
            }

            await using (var verification = store.CreateSession())
            {
                var remaining = await verification.Query<QueueItem, QueueItemIndex>(collection: ContactCenterConstants.CollectionName).CountAsync(TestContext.Current.CancellationToken);

                Assert.Equal(70, remaining);
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task PurgeAsync_WhenTheBudgetIsTight_SpendsItPerEntity_SoALaterEntityIsNotStarved()
    {
        // Arrange
        var databasePath = DatabasePath("starvation");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var session = store.CreateSession())
            {
                for (var i = 0; i < 60; i++)
                {
                    var suffix = i.ToString("D4", CultureInfo.InvariantCulture);

                    await session.SaveAsync(
                        ExpiredQueueItem($"queue-{suffix}"),
                        collection: ContactCenterConstants.CollectionName,
                        cancellationToken: TestContext.Current.CancellationToken);

                    await session.SaveAsync(
                        new ProviderCommand
                        {
                            ItemId = $"command-{suffix}",
                            CommandId = $"command-{suffix}",
                            ProviderName = "test",
                            Status = ProviderCommandStatus.Confirmed,
                            NextAttemptUtc = _nowUtc.AddDays(-90),
                            LeaseExpiresUtc = _nowUtc.AddDays(-90),
                            CompletedUtc = _nowUtc.AddDays(-89),
                        },
                        collection: ContactCenterConstants.CollectionName,
                        cancellationToken: TestContext.Current.CancellationToken);
                }

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using (var session = store.CreateSession())
            {
                var report = await CreateService(session, batchSize: 25, maxBatches: 2).PurgeAsync(TestContext.Current.CancellationToken);

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);

                // Assert
                // A budget spent globally would let the first policy consume the whole cycle and leave every
                // later entity permanently untouched, which grows forever while the cycle looks successful.
                var starved = report.Entities
                    .Where(entity => entity.PurgedCount == 0 && entity.WorkRemains)
                    .Select(entity => entity.EntityName);

                Assert.True(!starved.Any(), $"These entities had expired records but were given no budget: {string.Join(", ", starved)}.");
                Assert.Equal(50, report.Entities.Single(entity => entity.EntityName == "QueueItem").PurgedCount);
                Assert.Equal(50, report.Entities.Single(entity => entity.EntityName == "ProviderCommand").PurgedCount);
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task ASettledReservation_IsPurgedByItsSettlementTime_SoTheTableDoesNotGrowForever()
    {
        // Arrange
        // The reservation policy ages from the modification time. If that column does not round-trip through the
        // index, the predicate's null guard rejects every row and the table grows forever while retention reports
        // the entity as drained.
        var databasePath = DatabasePath("reservation-stamp");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();

            var reservationManager = new ActivityReservationManager(
                new ActivityReservationStore(session),
                [],
                NullLogger<CatalogManager<ActivityReservation>>.Instance);

            var settled = new ActivityReservation
            {
                ItemId = "reservation-1",
                ActivityItemId = "activity-1",
                AgentId = "agent-1",
                ModifiedUtc = _nowUtc.AddDays(-40),
            }.RestorePersistedStatus(ReservationStatus.Canceled);

            var unsettled = new ActivityReservation
            {
                ItemId = "reservation-2",
                ActivityItemId = "activity-2",
                AgentId = "agent-1",
                ExpiresUtc = _nowUtc.AddMinutes(1),
            }.RestorePersistedStatus(ReservationStatus.Pending);

            await reservationManager.CreateAsync(settled, cancellationToken: TestContext.Current.CancellationToken);
            await reservationManager.CreateAsync(unsettled, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            var report = await CreateService(session).PurgeAsync(TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, report.Entities.Single(entity => entity.EntityName == nameof(ActivityReservation)).PurgedCount);

            var remaining = await session
                .Query<ActivityReservation, ActivityReservationIndex>(collection: ContactCenterConstants.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken);

            Assert.Equal("reservation-2", Assert.Single(remaining).ItemId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task PurgeAsync_WhenRecordingUnderLegalHold_KeepsRecordAndDoesNotEnqueueMediaDeletion()
    {
        // Arrange
        var databasePath = DatabasePath("recording-hold");
        var store = await CreateStoreAsync(databasePath);
        var published = new List<InteractionEvent>();
        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((e, _) => published.Add(e))
            .Returns(Task.CompletedTask);
        var recordedCallSession = new CallSession
        {
            InteractionId = "interaction-recorded",
            RecordingReference = "storage/recorded",
        };
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(value => value.FindByInteractionIdAsync(
                "interaction-recorded",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(recordedCallSession);

        try
        {
            await using (var session = store.CreateSession())
            {
                await session.SaveAsync(
                    new Interaction
                    {
                        ItemId = "interaction-held",
                        Channel = InteractionChannel.Voice,
                        Direction = InteractionDirection.Inbound,
                        CreatedUtc = _nowUtc.AddDays(-95),
                        EndedUtc = _nowUtc.AddDays(-89),
                        RecordingReference = "storage/held",
                        RecordingLegalHold = true,
                    }.RestorePersistedStatus(InteractionStatus.Ended),
                    collection: ContactCenterConstants.CollectionName,
                    cancellationToken: TestContext.Current.CancellationToken);

                await session.SaveAsync(
                    new Interaction
                    {
                        ItemId = "interaction-recorded",
                        Channel = InteractionChannel.Voice,
                        Direction = InteractionDirection.Inbound,
                        CreatedUtc = _nowUtc.AddDays(-95),
                        EndedUtc = _nowUtc.AddDays(-89),
                        RecordingReference = "storage/recorded",
                    }.RestorePersistedStatus(InteractionStatus.Ended),
                    collection: ContactCenterConstants.CollectionName,
                    cancellationToken: TestContext.Current.CancellationToken);

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using (var session = store.CreateSession())
            {
                await CreateService(
                    session,
                    publisher.Object,
                    callSessionManager.Object).PurgeAsync(TestContext.Current.CancellationToken);

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            await using (var session = store.CreateSession())
            {
                var interactions = await session.Query<Interaction, InteractionIndex>(collection: ContactCenterConstants.CollectionName).ListAsync(TestContext.Current.CancellationToken);

                Assert.Equal(["interaction-held"], interactions.Select(interaction => interaction.ItemId));
            }

            var erased = Assert.Single(published);

            Assert.Equal(ContactCenterConstants.Events.RecordingErased, erased.EventType);

            var data = erased.GetData<RecordingErasedEventData>();

            Assert.Equal("storage/recorded", data.RecordingReference);
            Assert.Equal(ContactCenterConstants.RecordingErasureReason.Retention, data.Reason);
            Assert.Null(recordedCallSession.RecordingReference);
            callSessionManager.Verify(
                value => value.UpdateAsync(
                    recordedCallSession,
                    null,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static ContactCenterRetentionService CreateService(ISession session, int batchSize = 100, int maxBatches = 10_000)
        => CreateService(
            session,
            Mock.Of<IContactCenterEventPublisher>(),
            CreateCallSessionManager().Object,
            batchSize,
            maxBatches);

    private static ContactCenterRetentionService CreateService(
        ISession session,
        IContactCenterEventPublisher publisher,
        ICallSessionManager callSessionManager,
        int batchSize = 100,
        int maxBatches = 10_000)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_nowUtc);

        var options = new ContactCenterRetentionOptions
        {
            QueueItemRetentionDays = 30,
            ProviderCommandRetentionDays = 30,
            InteractionRetentionDays = 30,
            ActivityReservationRetentionDays = 30,
            PurgeBatchSize = batchSize,
            MaxPurgeBatchesPerCycle = maxBatches,
        };

        IEnumerable<IContactCenterRetentionPolicy> policies =
        [
            new QueueItemRetentionPolicy(session, new QueueItemStore(session)),
            new ProviderCommandRetentionPolicy(session, new ProviderCommandStore(session)),
            new InteractionRetentionPolicy(
                session,
                new InteractionStore(session),
                callSessionManager,
                publisher),
            new ActivityReservationRetentionPolicy(session, new ActivityReservationStore(session)),
        ];

        return new ContactCenterRetentionService(
            policies,
            session,
            clock.Object,
            Options.Create(options),
            NullLogger<ContactCenterRetentionService>.Instance);
    }

    private static Mock<ICallSessionManager> CreateCallSessionManager()
    {
        var manager = new Mock<ICallSessionManager>();
        manager
            .Setup(value => value.FindByInteractionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CallSession)null);

        return manager;
    }

    private static QueueItem ExpiredQueueItem(string itemId)
        => new QueueItem()
        {
            ItemId = itemId,
            QueueId = "queue-1",
            ActivityItemId = itemId,
            Priority = InteractionPriority.Normal,
            EnqueuedUtc = _nowUtc.AddDays(-90),
            DequeuedUtc = _nowUtc.AddDays(-89),
        }.RestorePersistedStatus(QueueItemStatus.Completed);

    private static async Task SeedAsync(ISession session)
    {
        await session.SaveAsync(ExpiredQueueItem("queue-expired"), collection: ContactCenterConstants.CollectionName);

        // Settled long ago by the clock that matters, but still waiting, so it must survive.
        await session.SaveAsync(
            new QueueItem
            {
                ItemId = "queue-live",
                QueueId = "queue-1",
                ActivityItemId = "queue-live",
                Priority = InteractionPriority.Normal,
                EnqueuedUtc = _nowUtc.AddDays(-90),
            }.RestorePersistedStatus(QueueItemStatus.Waiting),
            collection: ContactCenterConstants.CollectionName);

        await session.SaveAsync(
            new QueueItem
            {
                ItemId = "queue-recent",
                QueueId = "queue-1",
                ActivityItemId = "queue-recent",
                Priority = InteractionPriority.Normal,
                EnqueuedUtc = _nowUtc.AddDays(-2),
                DequeuedUtc = _nowUtc.AddDays(-1),
            }.RestorePersistedStatus(QueueItemStatus.Completed),
            collection: ContactCenterConstants.CollectionName);

        await session.SaveAsync(
            new ProviderCommand
            {
                ItemId = "command-expired",
                CommandId = "command-expired",
                ProviderName = "test",
                Status = ProviderCommandStatus.Confirmed,
                NextAttemptUtc = _nowUtc.AddDays(-90),
                LeaseExpiresUtc = _nowUtc.AddDays(-90),
                CompletedUtc = _nowUtc.AddDays(-89),
            },
            collection: ContactCenterConstants.CollectionName);

        // Old but never completed: a command still awaiting an outcome is not safe to delete at any age.
        await session.SaveAsync(
            new ProviderCommand
            {
                ItemId = "command-live",
                CommandId = "command-live",
                ProviderName = "test",
                Status = ProviderCommandStatus.OutcomeUnknown,
                NextAttemptUtc = _nowUtc.AddDays(-90),
                LeaseExpiresUtc = _nowUtc.AddDays(-90),
            },
            collection: ContactCenterConstants.CollectionName);

        await session.SaveAsync(
            new ProviderCommand
            {
                ItemId = "command-recent",
                CommandId = "command-recent",
                ProviderName = "test",
                Status = ProviderCommandStatus.Confirmed,
                NextAttemptUtc = _nowUtc.AddDays(-2),
                LeaseExpiresUtc = _nowUtc.AddDays(-2),
                CompletedUtc = _nowUtc.AddDays(-1),
            },
            collection: ContactCenterConstants.CollectionName);

        await session.SaveAsync(
            new Interaction
            {
                ItemId = "interaction-expired",
                Channel = InteractionChannel.Voice,
                Direction = InteractionDirection.Inbound,
                CreatedUtc = _nowUtc.AddDays(-95),
                EndedUtc = _nowUtc.AddDays(-89),
            }.RestorePersistedStatus(InteractionStatus.Ended),
            collection: ContactCenterConstants.CollectionName);

        // A conversation that started three months ago and has not ended is still live.
        await session.SaveAsync(
            new Interaction
            {
                ItemId = "interaction-live",
                Channel = InteractionChannel.Voice,
                Direction = InteractionDirection.Inbound,
                CreatedUtc = _nowUtc.AddDays(-95),
            }.RestorePersistedStatus(InteractionStatus.Connected),
            collection: ContactCenterConstants.CollectionName);

        await session.SaveAsync(
            new Interaction
            {
                ItemId = "interaction-recent",
                Channel = InteractionChannel.Voice,
                Direction = InteractionDirection.Inbound,
                CreatedUtc = _nowUtc.AddDays(-3),
                EndedUtc = _nowUtc.AddDays(-1),
            }.RestorePersistedStatus(InteractionStatus.Ended),
            collection: ContactCenterConstants.CollectionName);
    }

    private static string DatabasePath(string suffix)
        => Path.Combine(Path.GetTempPath(), $"contact-center-retention-{suffix}-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

        store.RegisterIndexes(
        [
            new QueueItemIndexProvider(),
            new ProviderCommandIndexProvider(),
            new InteractionIndexProvider(),
            new ActivityReservationIndexProvider(),
        ]);

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);

        var providerIdentityResolver = new Mock<IProviderIdentityResolver>();
        providerIdentityResolver.Setup(resolver => resolver.Canonicalize(It.IsAny<string>())).Returns<string>(value => value);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        var queueItemMigration = new QueueItemIndexMigrations(store, new StubClock())
        {
            SchemaBuilder = schemaBuilder,
        };

        var providerCommandMigration = new ProviderCommandIndexMigrations(store, new StubClock())
        {
            SchemaBuilder = schemaBuilder,
        };

        var reservationMigration = new ActivityReservationIndexMigrations(store, new StubClock())
        {
            SchemaBuilder = schemaBuilder,
        };

        var interactionMigration = new InteractionIndexMigrations
        {
            SchemaBuilder = schemaBuilder,
        };

        await queueItemMigration.CreateAsync();
        await providerCommandMigration.CreateAsync();

        // The provider command create step stops at its shipped version, exactly as it does for a real tenant,
        // so the retention column arrives through the update step here too.
        await providerCommandMigration.UpdateFrom1Async();
        await reservationMigration.CreateAsync();
        await interactionMigration.CreateAsync();
        await interactionMigration.UpdateFrom1Async();
        await interactionMigration.UpdateFrom2Async();
        await interactionMigration.UpdateFrom3Async();
        await interactionMigration.UpdateFrom4Async();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
