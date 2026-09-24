using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Every call an agent accepted was stored as two interaction documents with the same id. A YesSql session stops
/// tracking what it loaded once it commits, and saving an instance the session does not track inserts it as a new
/// document. Accepting an offer commits the unit of work part-way through, so the interaction read before the accept
/// and saved after it became a second copy; the reads that find an interaction by id or by provider call then picked
/// either copy, and the reconciliation sweep updated one while the rest of the call updated the other. These run
/// against a real SQLite store because the defect lives in the session's tracking, which a mocked store cannot show.
/// </summary>
public sealed class InteractionSingleDocumentTests
{
    private static readonly DateTime _now = new(2026, 9, 24, 14, 4, 46, DateTimeKind.Utc);

    [Fact]
    public async Task AcceptInboundOfferAsync_WhenTheAcceptCommits_UpdatesTheInteractionInsteadOfStoringASecondCopy()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(
            cancellationToken,
            CreateInteraction("interaction-1", "call-1", InteractionStatus.Created, activityItemId: "act1"));

        try
        {
            await using (var session = store.CreateSession())
            {
                var service = CreateCallCommandService(session);

                // Act
                var result = await service.AcceptInboundOfferAsync("r1", "u1", cancellationToken);
                await session.SaveChangesAsync(cancellationToken);

                Assert.True(result.Succeeded);
            }

            // Assert
            var stored = Assert.Single(await ReadAllAsync(store, "interaction-1", cancellationToken));

            Assert.Equal("a1", stored.AgentId);
            Assert.Equal(InteractionStatus.Ringing, stored.Status);
            Assert.True(stored.TechnicalMetadata.ContainsKey(ContactCenterConstants.CommandMetadata.CommandId));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task ReconcileActiveInteractionsAsync_WhenAnEarlierCallCommits_RepairsALaterCallInPlace()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(
            cancellationToken,
            CreateInteraction("interaction-1", "call-1", InteractionStatus.Connected, createdUtc: _now.AddMinutes(-2)),
            CreateInteraction("interaction-2", "call-2", InteractionStatus.Connected, createdUtc: _now.AddMinutes(-1)));

        try
        {
            await using (var session = store.CreateSession())
            {
                var service = CreateSynchronizationService(session);

                // Act
                await service.ReconcileActiveInteractionsAsync(cancellationToken);
                await session.SaveChangesAsync(cancellationToken);
            }

            // Assert
            Assert.Single(await ReadAllAsync(store, "interaction-1", cancellationToken));

            var repaired = Assert.Single(await ReadAllAsync(store, "interaction-2", cancellationToken));

            Assert.Equal(InteractionStatus.Ended, repaired.Status);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpdateAsync_WhenTheInstanceWasReadBeforeTheSessionCommitted_RefusesToStoreASecondCopy()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(
            cancellationToken,
            CreateInteraction("interaction-1", "call-1", InteractionStatus.Connected));

        try
        {
            await using (var session = store.CreateSession())
            {
                var interactionStore = new InteractionStore(session);
                var interaction = await interactionStore.FindByIdAsync("interaction-1", cancellationToken);

                await session.SaveChangesAsync(cancellationToken);
                interaction.AgentId = "a1";

                // Act & Assert
                await Assert.ThrowsAsync<ConcurrencyException>(
                    async () => await interactionStore.UpdateAsync(interaction, cancellationToken));
            }

            Assert.Single(await ReadAllAsync(store, "interaction-1", cancellationToken));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpdateAsync_WhenTheInstanceIsReadAgainAfterTheCommit_UpdatesTheOnlyCopy()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(
            cancellationToken,
            CreateInteraction("interaction-1", "call-1", InteractionStatus.Connected));

        try
        {
            await using (var session = store.CreateSession())
            {
                var interactionStore = new InteractionStore(session);
                var interaction = await interactionStore.FindByIdAsync("interaction-1", cancellationToken);
                interaction.AgentId = "a1";
                await interactionStore.UpdateAsync(interaction, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);

                // Act
                interaction = await interactionStore.FindByIdAsync("interaction-1", cancellationToken);
                interaction.AgentId = "a2";
                await interactionStore.UpdateAsync(interaction, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);
            }

            // Assert
            var stored = Assert.Single(await ReadAllAsync(store, "interaction-1", cancellationToken));

            Assert.Equal("a2", stored.AgentId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpdateAsync_WhenAnEarlierDefectLeftTwoCopies_StillUpdatesTheCopyTheSessionTracks()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(
            cancellationToken,
            CreateInteraction("interaction-1", "call-1", InteractionStatus.Ended),
            CreateInteraction("interaction-1", "call-1", InteractionStatus.Ended));

        try
        {
            await using (var session = store.CreateSession())
            {
                var interactionStore = new InteractionStore(session);
                var copies = await session.Query<Interaction, InteractionIndex>(
                    index => index.ItemId == "interaction-1",
                    collection: ContactCenterStorage.CollectionName)
                    .ListAsync(cancellationToken);

                // Act
                foreach (var copy in copies)
                {
                    copy.WrapUpCompletedUtc = _now;
                    await interactionStore.UpdateAsync(copy, cancellationToken);
                }

                await session.SaveChangesAsync(cancellationToken);
            }

            // Assert
            var stored = await ReadAllAsync(store, "interaction-1", cancellationToken);

            Assert.Equal(2, stored.Count);
            Assert.All(stored, copy => Assert.Equal(_now, copy.WrapUpCompletedUtc));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task CreateAsync_WhenTheInteractionIsNew_StoresIt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(cancellationToken);

        try
        {
            await using (var session = store.CreateSession())
            {
                var interactionStore = new InteractionStore(session);

                // Act
                await interactionStore.CreateAsync(
                    CreateInteraction("interaction-1", "call-1", InteractionStatus.Created),
                    cancellationToken);
                await session.SaveChangesAsync(cancellationToken);
            }

            // Assert
            Assert.Single(await ReadAllAsync(store, "interaction-1", cancellationToken));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static ContactCenterCallCommandService CreateCallCommandService(ISession session)
    {
        var reservation = new ActivityReservation
        {
            ItemId = "r1",
            AgentId = "a1",
            ActivityItemId = "act1",
            QueueId = "q1",
            CreatedUtc = _now.AddSeconds(-7),
        }.RestorePersistedStatus(ReservationStatus.Pending);
        var agent = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            UserName = "agent",
        };

        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager
            .Setup(manager => manager.FindByIdAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        agentManager
            .Setup(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        // Accepting commits the unit of work, as ActivityReservationService does once it has moved the reservation,
        // the queue item and the agent.
        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await session.SaveChangesAsync(CancellationToken.None);

                return reservation.RestorePersistedStatus(ReservationStatus.Accepted);
            });

        var provider = new Mock<IContactCenterVoiceProvider>();
        provider.SetupGet(value => value.TechnicalName).Returns("dp");
        provider.SetupGet(value => value.DeliveryModel).Returns(VoiceProviderDeliveryModel.ServerSideAcd);

        var voiceProviderResolver = new Mock<IContactCenterVoiceProviderResolver>();
        voiceProviderResolver
            .Setup(resolver => resolver.Get("dp"))
            .Returns(provider.Object);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new CallSession { ItemId = "session-1" });

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new ContactCenterCallCommandService(
            reservationService.Object,
            reservationManager.Object,
            CreateInteractionManager(session),
            new Mock<IOmnichannelActivityManager>().Object,
            new Mock<IDialerProfileReader>().Object,
            [],
            agentManager.Object,
            voiceProviderResolver.Object,
            callSessionManager.Object,
            new Mock<IProviderCommandStateService>().Object,
            new Mock<IContactCenterScopeExecutor>().Object,
            new Mock<IContactCenterEventPublisher>().Object,
            new Mock<IQueueItemManager>().Object,
            new Mock<IActivityQueueService>().Object,
            [],
            new Mock<IAgentPreDialCoordinator>().Object,
            clock.Object,
            NullLogger<ContactCenterCallCommandService>.Instance);
    }

    private static ProviderCallStateSynchronizationService CreateSynchronizationService(ISession session)
    {
        // The first call is no longer on the provider, so reconciliation ingests its ending; ingestion commits the
        // unit of work, as ProviderVoiceEventService does. The second call's session has already ended, so
        // reconciliation repairs its interaction directly -- after that commit.
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync("interaction-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallSession
            {
                ItemId = "session-2",
                InteractionId = "interaction-2",
                ProviderCallId = "call-2",
                StartedUtc = _now.AddMinutes(-1),
                EndedUtc = _now,
            }.RestorePersistedState(VoiceCallState.Ended));

        var eventService = new Mock<IProviderVoiceEventService>();
        eventService
            .Setup(service => service.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await session.SaveChangesAsync(CancellationToken.None);

                return new CallSession();
            });

        var provider = new Mock<ITelephonyProvider>();
        provider
            .As<ITelephonyCallStateProvider>()
            .Setup(value => value.GetCallStateAsync("call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = false,
            });

        var resolver = new Mock<ITelephonyProviderResolver>();
        resolver.Setup(value => value.GetAsync("provider-1")).ReturnsAsync(provider.Object);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new ProviderCallStateSynchronizationService(
            CreateInteractionManager(session),
            callSessionManager.Object,
            eventService.Object,
            new Mock<IProviderVoiceOfferSynchronizationService>().Object,
            resolver.Object,
            distributedLock.Object,
            clock.Object,
            NullLogger<ProviderCallStateSynchronizationService>.Instance);
    }

    private static InteractionManager CreateInteractionManager(ISession session)
        => new(new InteractionStore(session), [], NullLogger<CatalogManager<Interaction>>.Instance);

    private static Interaction CreateInteraction(
        string itemId,
        string providerCallId,
        InteractionStatus status,
        string activityItemId = null,
        DateTime? createdUtc = null)
    {
        var isAcceptTarget = activityItemId is not null;

        return new Interaction
        {
            ItemId = itemId,
            ActivityItemId = activityItemId,
            ProviderName = isAcceptTarget ? "dp" : "provider-1",
            ProviderInteractionId = providerCallId,
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Inbound,
            CreatedUtc = createdUtc ?? _now.AddMinutes(-3),
        }.RestorePersistedStatus(status);
    }

    private static async Task<IReadOnlyList<Interaction>> ReadAllAsync(
        IStore store,
        string itemId,
        CancellationToken cancellationToken)
    {
        await using var session = store.CreateSession();

        return [.. await session.Query<Interaction, InteractionIndex>(
            index => index.ItemId == itemId,
            collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken)];
    }

    private static async Task<(IStore Store, string DatabasePath)> CreateStoreAsync(
        CancellationToken cancellationToken,
        params Interaction[] interactions)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "InteractionSingleDocumentData");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, $"interaction-single-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration =>
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new InteractionIndexProvider()]);

        await store.InitializeAsync(cancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, cancellationToken);

        await using (var migrationSession = store.CreateSession())
        {
            var transaction = await migrationSession.BeginTransactionAsync(cancellationToken);
            await InteractionQueryPlanFixture.MigrateAsync(store.Configuration, transaction);
            await transaction.CommitAsync(cancellationToken);
        }

        // Each seed row is written by its own session so two rows with the same id can stand for the copies an
        // earlier defect left behind.
        foreach (var interaction in interactions)
        {
            await using var session = store.CreateSession();
            await session.SaveAsync(interaction, collection: ContactCenterStorage.CollectionName);
            await session.SaveChangesAsync(cancellationToken);
        }

        return (store, databasePath);
    }
}
