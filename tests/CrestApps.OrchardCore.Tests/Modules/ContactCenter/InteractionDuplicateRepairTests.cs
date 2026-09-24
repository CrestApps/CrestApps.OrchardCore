using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Services;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An earlier defect stored most accepted calls as two interaction documents with the same id, each holding part of
/// the call: the copy the accept inserted had when the call was answered and the answer command, the original had
/// how it ended and its wrap-up. The migration step merges them into one record that keeps everything either copy
/// knew, and then makes a second document with the same id impossible. These run the real step against a real SQLite
/// store, because what is under test is the SQL it runs on the migration's own transaction.
/// </summary>
public sealed class InteractionDuplicateRepairTests
{
    private static readonly DateTime _createdUtc = new(2026, 9, 24, 14, 4, 40, DateTimeKind.Utc);

    [Fact]
    public async Task UpdateFrom6_MergesTheCopiesOfAnInteractionIntoOneRecordHoldingBoth()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(cancellationToken);

        try
        {
            // The original: the rest of the call updated it last.
            var original = CreateInteraction("interaction-1");
            original.RestorePersistedStatus(InteractionStatus.Ended);
            original.ModifiedUtc = _createdUtc.AddMinutes(5);
            original.EndedUtc = _createdUtc.AddMinutes(2);
            original.WrapUpCompletedUtc = _createdUtc.AddMinutes(5);
            original.TechnicalMetadata["aiConversationEnded"] = true;
            original.QueueHistory.Add(new InteractionQueueHistoryEntry { QueueId = "queue-1", EnteredUtc = _createdUtc });

            // The copy the accept inserted: it has what the accept and the answered call wrote.
            var inserted = CreateInteraction("interaction-1");
            inserted.RestorePersistedStatus(InteractionStatus.Failed);
            inserted.ModifiedUtc = _createdUtc.AddMinutes(2);
            inserted.AgentId = "agent-1";
            inserted.StartedUtc = _createdUtc.AddSeconds(6);
            inserted.AnsweredUtc = _createdUtc.AddSeconds(8);
            inserted.EndedUtc = _createdUtc.AddMinutes(3);
            inserted.WrapUpStartedUtc = _createdUtc.AddMinutes(2);
            inserted.RecordingLegalHold = true;
            inserted.TechnicalMetadata["providerCommandId"] = "command-1";
            inserted.QueueHistory.Add(new InteractionQueueHistoryEntry { QueueId = "queue-1", EnteredUtc = _createdUtc });
            inserted.QueueHistory.Add(new InteractionQueueHistoryEntry { QueueId = "queue-2", EnteredUtc = _createdUtc.AddSeconds(1) });

            var untouched = CreateInteraction("interaction-2");
            untouched.AgentId = "agent-2";

            await SeedAsync(store, cancellationToken, original, inserted, untouched);

            // Act
            await RunRepairStepAsync(store, cancellationToken);

            // Assert
            var merged = Assert.Single(await ReadAllAsync(store, "interaction-1", cancellationToken));

            Assert.Equal(InteractionStatus.Ended, merged.Status);
            Assert.Equal(_createdUtc.AddMinutes(5), merged.ModifiedUtc);
            Assert.Equal(_createdUtc.AddMinutes(2), merged.EndedUtc);
            Assert.Equal(_createdUtc.AddMinutes(5), merged.WrapUpCompletedUtc);
            Assert.Equal("agent-1", merged.AgentId);
            Assert.Equal(_createdUtc.AddSeconds(6), merged.StartedUtc);
            Assert.Equal(_createdUtc.AddSeconds(8), merged.AnsweredUtc);
            Assert.Equal(_createdUtc.AddMinutes(2), merged.WrapUpStartedUtc);
            Assert.True(merged.RecordingLegalHold);
            Assert.True(merged.TechnicalMetadata.ContainsKey("aiConversationEnded"));
            Assert.Equal("command-1", merged.TechnicalMetadata["providerCommandId"]?.ToString());
            Assert.Equal(["queue-1", "queue-2"], merged.QueueHistory.Select(entry => entry.QueueId));

            // The index row describes the merged record, so the queries that filter on it find what it now holds.
            await using (var session = store.CreateSession())
            {
                var byIndex = await session.Query<Interaction, InteractionIndex>(
                    index => index.ItemId == "interaction-1" &&
                        index.AgentId == "agent-1" &&
                        index.WrapUpStartedUtc != null &&
                        index.RecordingLegalHold,
                    collection: ContactCenterStorage.CollectionName)
                    .ListAsync(cancellationToken);

                Assert.Single(byIndex);
            }

            var other = Assert.Single(await ReadAllAsync(store, "interaction-2", cancellationToken));

            Assert.Equal("agent-2", other.AgentId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpdateFrom6_WhenNoInteractionHasCopies_LeavesEveryRecordAsItWas()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(cancellationToken);

        try
        {
            var first = CreateInteraction("interaction-1");
            first.AgentId = "agent-1";
            var second = CreateInteraction("interaction-2");
            second.AgentId = "agent-2";

            await SeedAsync(store, cancellationToken, first, second);

            // Act
            await RunRepairStepAsync(store, cancellationToken);

            // Assert
            Assert.Equal("agent-1", Assert.Single(await ReadAllAsync(store, "interaction-1", cancellationToken)).AgentId);
            Assert.Equal("agent-2", Assert.Single(await ReadAllAsync(store, "interaction-2", cancellationToken)).AgentId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpdateFrom6_AfterTheMerge_RefusesASecondDocumentWithTheSameId()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, databasePath) = await CreateStoreAsync(cancellationToken);

        try
        {
            await SeedAsync(store, cancellationToken, CreateInteraction("interaction-1"), CreateInteraction("interaction-1"));
            await RunRepairStepAsync(store, cancellationToken);

            // Act & Assert
            await using var session = store.CreateSession();
            await session.SaveAsync(CreateInteraction("interaction-1"), collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);

            await Assert.ThrowsAnyAsync<Exception>(async () => await session.SaveChangesAsync(cancellationToken));
            Assert.Single(await ReadAllAsync(store, "interaction-1", cancellationToken));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public void Conflicts_NameTheFieldsBothCopiesHoldDifferently()
    {
        // Arrange
        var kept = CreateInteraction("interaction-1").RestorePersistedStatus(InteractionStatus.Ended);
        kept.AgentId = "agent-1";
        kept.EndedUtc = _createdUtc.AddMinutes(2);
        kept.TechnicalMetadata["unansweredOfferAction"] = "Requeue";

        var older = CreateInteraction("interaction-1").RestorePersistedStatus(InteractionStatus.Failed);
        older.AgentId = "agent-1";
        older.EndedUtc = _createdUtc.AddMinutes(3);
        older.AnsweredUtc = _createdUtc.AddSeconds(8);
        older.TechnicalMetadata["unansweredOfferAction"] = "Voicemail";

        // Act
        var conflicts = InteractionDuplicateMerge.Conflicts(kept, older);

        // Assert
        Assert.Equal(
            [nameof(Interaction.EndedUtc), nameof(Interaction.Status), "TechnicalMetadata.unansweredOfferAction"],
            conflicts.Order(StringComparer.Ordinal));
    }

    private static Interaction CreateInteraction(string itemId)
        => new Interaction
        {
            ItemId = itemId,
            ProviderName = "Telnyx",
            ProviderInteractionId = $"call-{itemId}",
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Inbound,
            CreatedUtc = _createdUtc,
            ModifiedUtc = _createdUtc,
        }.RestorePersistedStatus(InteractionStatus.Connected);

    private static async Task RunRepairStepAsync(IStore store, CancellationToken cancellationToken)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);
        var migration = new InteractionIndexMigrations(store)
        {
            SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
        };

        Assert.Equal(7, await migration.UpdateFrom6Async());

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task SeedAsync(IStore store, CancellationToken cancellationToken, params Interaction[] interactions)
    {
        // Each copy is written by its own session, which is how the defect produced them.
        foreach (var interaction in interactions)
        {
            await using var session = store.CreateSession();
            await session.SaveAsync(interaction, collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<Interaction>> ReadAllAsync(IStore store, string itemId, CancellationToken cancellationToken)
    {
        await using var session = store.CreateSession();

        return [.. await session.Query<Interaction, InteractionIndex>(
            index => index.ItemId == itemId,
            collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken)];
    }

    private static async Task<(IStore Store, string DatabasePath)> CreateStoreAsync(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "InteractionDuplicateRepairData");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, $"interaction-repair-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration =>
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new InteractionIndexProvider()]);

        await store.InitializeAsync(cancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, cancellationToken);

        // The schema stops before the repair step, as a tenant that still holds the copies does.
        await using var migrationSession = store.CreateSession();
        var transaction = await migrationSession.BeginTransactionAsync(cancellationToken);
        await InteractionQueryPlanFixture.MigrateAsync(store, transaction, throughItemIdUniqueness: false);
        await transaction.CommitAsync(cancellationToken);

        return (store, databasePath);
    }
}
