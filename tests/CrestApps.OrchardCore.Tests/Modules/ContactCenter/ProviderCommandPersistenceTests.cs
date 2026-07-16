using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using Microsoft.Extensions.Logging.Abstractions;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderCommandPersistenceTests
{
    private static readonly DateTime _now = new(2026, 7, 14, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateAsync_CommitsBeforeReturn_AndIsVisibleToANewSession()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-provider-command-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await CreateCommandAsync(store, "command-1", ProviderCommandStatus.Pending);

            // Act
            await using var verificationSession = store.CreateSession();
            var persisted = await new ProviderCommandStore(verificationSession)
                .FindByCommandIdAsync("command-1", TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(persisted);
            Assert.Equal("command-1", persisted.CommandId);
            Assert.Equal(ProviderCommandStatus.Pending, persisted.Status);
            Assert.Equal(0, persisted.FenceToken);
            Assert.Equal("reservation-1", persisted.ReservationId);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ProviderCommandIndex_DuplicateCommandId_RejectsSecondCommand()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-provider-command-dup-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await CreateCommandAsync(store, "command-1", ProviderCommandStatus.Pending);

            // Act
            var exception = await Record.ExceptionAsync(() =>
                CreateCommandAsync(store, "command-1", ProviderCommandStatus.Pending));

            // Assert
            Assert.IsAssignableFrom<System.Data.Common.DbException>(exception);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task UpdateAsync_TwoSessionsReadSameVersion_OnlyOneCommitsWithOptimisticConcurrency()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-provider-command-concurrency-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await CreateCommandAsync(store, "command-1", ProviderCommandStatus.Pending);

            await using var firstSession = store.CreateSession();
            await using var secondSession = store.CreateSession();
            var firstStore = new ProviderCommandStore(firstSession);
            var secondStore = new ProviderCommandStore(secondSession);
            var firstCommand = await firstStore.FindByCommandIdAsync("command-1", TestContext.Current.CancellationToken);
            var secondCommand = await secondStore.FindByCommandIdAsync("command-1", TestContext.Current.CancellationToken);

            // Act
            firstCommand.Status = ProviderCommandStatus.Claimed;
            await firstStore.UpdateAsync(firstCommand, cancellationToken: TestContext.Current.CancellationToken);
            await firstSession.SaveChangesAsync(TestContext.Current.CancellationToken);

            secondCommand.Status = ProviderCommandStatus.Failed;
            await secondStore.UpdateAsync(secondCommand, cancellationToken: TestContext.Current.CancellationToken);
            var exception = await Record.ExceptionAsync(() =>
                secondSession.SaveChangesAsync(TestContext.Current.CancellationToken));

            // Assert
            Assert.NotNull(exception);
            Assert.True(
                exception is ConcurrencyException or System.Data.Common.DbException,
                $"Expected an optimistic concurrency failure but received {exception.GetType().Name}.");

            await using var verificationSession = store.CreateSession();
            var persisted = await new ProviderCommandStore(verificationSession)
                .FindByCommandIdAsync("command-1", TestContext.Current.CancellationToken);
            Assert.Equal(ProviderCommandStatus.Claimed, persisted.Status);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new ProviderCommandIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);
        await CreateIndexSchemaAsync(store);

        return store;
    }

    private static async Task CreateCommandAsync(IStore store, string commandId, ProviderCommandStatus status)
    {
        await using var session = store.CreateSession();
        var manager = CreateManager(session);
        var command = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        command.CommandId = commandId;
        command.ProviderName = "provider";
        command.CommandType = ProviderCommandType.Dial;
        command.Status = status;
        command.FenceToken = 0;
        command.ReservationId = "reservation-1";
        command.CreatedUtc = _now;
        command.NextAttemptUtc = _now;
        command.LeaseExpiresUtc = _now;
        await manager.CreateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static ProviderCommandManager CreateManager(ISession session)
    {
        return new ProviderCommandManager(
            new ProviderCommandStore(session),
            [],
            NullLogger<CatalogManager<ProviderCommand>>.Instance);
    }

    private static async Task CreateIndexSchemaAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<ProviderCommandIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("CommandId", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(100))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<long>("FenceToken", column => column.NotNull().WithDefault(0L))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull())
            .Column<DateTime>("LeaseExpiresUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }
}
