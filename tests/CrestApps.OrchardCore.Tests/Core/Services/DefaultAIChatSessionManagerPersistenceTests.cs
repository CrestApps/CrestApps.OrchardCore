using System.Security.Claims;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Security;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Core.Services;

/// <summary>
/// Verifies against a real database that re-saving a chat session keeps a single document.
/// </summary>
public sealed class DefaultAIChatSessionManagerPersistenceTests
{
    private static readonly YesSqlStoreOptions _storeOptions = new()
    {
        AICollectionName = null,
    };

    [Fact]
    public async Task SaveAsync_WhenTheSameSessionIsCommittedAtEveryTurn_KeepsOneDocument()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"crestapps-chat-session-resave-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var manager = CreateManager(session);
            var chatSession = new AIChatSession
            {
                SessionId = "session-1",
                ProfileId = "profile-1",
                UserId = "user-1",
            };

            // Act
            // Voice mode saves and commits the same instance at every turn.
            for (var turn = 1; turn <= 3; turn++)
            {
                chatSession.Title = $"Turn {turn}";

                await manager.SaveAsync(chatSession, TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            await using var readSession = store.CreateSession();
            var persisted = await readSession
                .Query<AIChatSession, AIChatSessionIndex>(x => x.SessionId == "session-1")
                .ListAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Turn 3", Assert.Single(persisted).Title);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static DefaultAIChatSessionManager CreateManager(YSession session)
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "test")),
            },
        };

        return new DefaultAIChatSessionManager(
            Mock.Of<IClock>(),
            httpContextAccessor,
            Mock.Of<IAIVisitorIdentityResolver>(),
            session,
            Mock.Of<IAIChatSessionPromptStore>(),
            new YesSqlAIChatSessionStore(session, Options.Create(_storeOptions)),
            [],
            Options.Create(_storeOptions),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<DefaultAIChatSessionManager>>());
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new AIChatSessionIndexProvider(Options.Create(_storeOptions))]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new SchemaBuilder(store.Configuration, transaction).CreateAIChatSessionIndexSchemaAsync(_storeOptions);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
