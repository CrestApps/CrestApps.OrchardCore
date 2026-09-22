using System.Security.Claims;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Security;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
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
        var databasePath = DatabasePath("chat-session-resave");
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
                Title = "Untitled",
                CreatedUtc = new DateTime(2026, 9, 22, 21, 0, 0, DateTimeKind.Utc),
            };

            // Act
            for (var turn = 1; turn <= 3; turn++)
            {
                chatSession.Title = $"Turn {turn}";
                chatSession.LastActivityUtc = chatSession.CreatedUtc.AddMinutes(turn);

                await manager.SaveAsync(chatSession, TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            var persisted = await ReadAllAsync(store, "session-1");
            var single = Assert.Single(persisted);
            Assert.Equal("Turn 3", single.Title);
            Assert.Equal(chatSession.CreatedUtc.AddMinutes(3), single.LastActivityUtc);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task SaveAsync_WhenADifferentInstanceCarriesAStoredSessionId_UpdatesTheStoredDocument()
    {
        // Arrange
        var databasePath = DatabasePath("chat-session-detached");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await CreateManager(seedSession).SaveAsync(new AIChatSession
                {
                    SessionId = "session-1",
                    ProfileId = "profile-1",
                    UserId = "user-1",
                    Title = "Original",
                }, TestContext.Current.CancellationToken);
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var session = store.CreateSession();

            // Act
            await CreateManager(session).SaveAsync(new AIChatSession
            {
                SessionId = "session-1",
                ProfileId = "profile-1",
                UserId = "user-1",
                Title = "Renamed",
                RemoteAddressHash = "hash-1",
            }, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            var single = Assert.Single(await ReadAllAsync(store, "session-1"));
            Assert.Equal("Renamed", single.Title);
            Assert.Equal("hash-1", single.RemoteAddressHash);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static DefaultAIChatSessionManager CreateManager(YSession session)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 9, 22, 21, 0, 0, DateTimeKind.Utc));

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "test")),
            },
        };

        return new DefaultAIChatSessionManager(
            clock.Object,
            httpContextAccessor,
            Mock.Of<IAIVisitorIdentityResolver>(),
            session,
            Mock.Of<IAIChatSessionPromptStore>(),
            [],
            Options.Create(_storeOptions),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<DefaultAIChatSessionManager>>());
    }

    private static string DatabasePath(string prefix)
        => Path.Combine(Path.GetTempPath(), $"crestapps-{prefix}-{Guid.NewGuid():N}.db");

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

    private static async Task<IReadOnlyList<AIChatSession>> ReadAllAsync(IStore store, string sessionId)
    {
        await using var session = store.CreateSession();

        return (await session
            .Query<AIChatSession, AIChatSessionIndex>(x => x.SessionId == sessionId)
            .ListAsync(TestContext.Current.CancellationToken))
            .ToList();
    }
}
