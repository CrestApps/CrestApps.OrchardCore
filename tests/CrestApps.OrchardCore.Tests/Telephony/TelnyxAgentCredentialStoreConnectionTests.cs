using CrestApps.OrchardCore.Telnyx.Indexes;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using OrchardCore.Environment.Shell;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The credential store records which soft-phone connection registered on each credential, and marks only that
/// connection's credentials when it closes, so the endpoint resolver prefers a window that is still open.
/// </summary>
public sealed class TelnyxAgentCredentialStoreConnectionTests
{
    private static readonly DateTime _now = new(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AClosingConnection_MarksOnlyTheCredentialsItRegisteredOn_AndTheOpenWindowIsResolvedFirst()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        await harness.SeedAsync(Credential("window-a"), Credential("window-b"), Credential("someone-else", userId: "user-2"));

        await harness.WithStoreAsync(store => store.MarkRegisteredAsync("user-1", "window-a", "connection-a", _now.AddSeconds(5)));
        await harness.WithStoreAsync(store => store.MarkRegisteredAsync("user-1", "window-b", "connection-b", _now.AddSeconds(10)));
        await harness.WithStoreAsync(store => store.MarkRegisteredAsync("user-2", "someone-else", "connection-b", _now.AddSeconds(10)));

        // Act
        var marked = await harness.WithStoreAsync(store => store.MarkConnectionClosedAsync("user-1", "connection-b", _now.AddSeconds(30)));

        // Assert
        Assert.Equal(1, marked);

        var live = await harness.WithStoreAsync(store => store.ListLiveByUserAsync("user-1", _now.AddSeconds(31)));
        Assert.Equal("window-a", live[0].CredentialId);
        Assert.Equal(_now.AddSeconds(30), live.Single(credential => credential.CredentialId == "window-b").ConnectionClosedUtc);

        var otherUser = await harness.WithStoreAsync(store => store.ListLiveByUserAsync("user-2", _now.AddSeconds(31)));
        Assert.Null(Assert.Single(otherUser).ConnectionClosedUtc);
    }

    [Fact]
    public async Task RegisteringAgain_OnACredentialWhoseConnectionClosed_MakesItLiveAgain()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        await harness.SeedAsync(Credential("window-a"));
        await harness.WithStoreAsync(store => store.MarkRegisteredAsync("user-1", "window-a", "connection-a", _now.AddSeconds(5)));
        await harness.WithStoreAsync(store => store.MarkConnectionClosedAsync("user-1", "connection-a", _now.AddSeconds(30)));

        // Act
        await harness.WithStoreAsync(store => store.MarkRegisteredAsync("user-1", "window-a", "connection-c", _now.AddSeconds(40)));

        // Assert
        var credential = Assert.Single(await harness.WithStoreAsync(store => store.ListLiveByUserAsync("user-1", _now.AddSeconds(41))));
        Assert.Null(credential.ConnectionClosedUtc);
        Assert.Equal("connection-c", credential.RegisteredConnectionId);
    }

    [Fact]
    public async Task MarkUnreachable_MarksTheUsersCredentialBySipUsername_AndItIsResolvedLast()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        await harness.SeedAsync(Credential("window-a"), Credential("window-b"));
        await harness.WithStoreAsync(store => store.MarkRegisteredAsync("user-1", "window-a", "connection-a", _now.AddSeconds(5)));

        // Act
        var marked = await harness.WithStoreAsync(store => store.MarkUnreachableAsync("user-1", "sip-window-a", _now.AddSeconds(30)));
        var otherUsers = await harness.WithStoreAsync(store => store.MarkUnreachableAsync("user-2", "sip-window-b", _now.AddSeconds(30)));

        // Assert
        Assert.Equal("window-a", marked?.CredentialId);
        Assert.Null(otherUsers);

        var live = await harness.WithStoreAsync(store => store.ListLiveByUserAsync("user-1", _now.AddSeconds(31)));
        Assert.Equal(["window-b", "window-a"], live.Select(credential => credential.CredentialId));
        Assert.Equal(_now.AddSeconds(30), live[1].UnreachableUtc);
        Assert.Null(live[0].UnreachableUtc);
    }

    [Fact]
    public async Task RegisteringOnANewCredential_FromTheSameConnection_MarksTheCredentialItLeftUnreachable()
    {
        // Arrange
        // The phone in one window moved to a fresh credential; the one it left no longer receives anything, although
        // it still reads as registered by a connection that is open.
        await using var harness = await Harness.CreateAsync();
        await harness.SeedAsync(Credential("left-behind"), Credential("moved-to"), Credential("other-window"));
        await harness.WithStoreAsync(store => store.MarkRegisteredAsync("user-1", "left-behind", "connection-a", _now.AddSeconds(5)));
        await harness.WithStoreAsync(store => store.MarkRegisteredAsync("user-1", "other-window", "connection-b", _now.AddSeconds(6)));

        // Act
        await harness.WithStoreAsync(store => store.MarkRegisteredAsync("user-1", "moved-to", "connection-a", _now.AddSeconds(40)));

        // Assert
        var live = await harness.WithStoreAsync(store => store.ListLiveByUserAsync("user-1", _now.AddSeconds(41)));
        Assert.Equal(_now.AddSeconds(40), live.Single(credential => credential.CredentialId == "left-behind").UnreachableUtc);
        Assert.Null(live.Single(credential => credential.CredentialId == "other-window").UnreachableUtc);
        Assert.Equal("left-behind", live[^1].CredentialId);
    }

    [Fact]
    public async Task RegisteringAgain_OnACredentialFoundUnreachable_MakesItReachableAgain()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();
        await harness.SeedAsync(Credential("window-a"));
        await harness.WithStoreAsync(store => store.MarkRegisteredAsync("user-1", "window-a", "connection-a", _now.AddSeconds(5)));
        await harness.WithStoreAsync(store => store.MarkUnreachableAsync("user-1", "sip-window-a", _now.AddSeconds(30)));

        // Act
        await harness.WithStoreAsync(store => store.MarkRegisteredAsync("user-1", "window-a", "connection-c", _now.AddSeconds(40)));

        // Assert
        var credential = Assert.Single(await harness.WithStoreAsync(store => store.ListLiveByUserAsync("user-1", _now.AddSeconds(41))));
        Assert.Null(credential.UnreachableUtc);
    }

    private static TelnyxAgentCredential Credential(string credentialId, string userId = "user-1")
        => new()
        {
            UserId = userId,
            CredentialId = credentialId,
            SipUsername = "sip-" + credentialId,
            IssuedUtc = _now,
            ExpiresUtc = _now.AddHours(1),
        };

    private sealed class Harness : IAsyncDisposable
    {
        private readonly IStore _store;
        private readonly string _databasePath;
        private readonly ShellSettings _shellSettings = new() { Name = "Default" };

        private Harness(IStore store, string databasePath)
        {
            _store = store;
            _databasePath = databasePath;
        }

        public static async Task<Harness> CreateAsync()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var databasePath = Path.Combine(Path.GetTempPath(), $"telnyx-credentials-{Guid.NewGuid():N}.db");
            var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

            store.RegisterIndexes([new TelnyxAgentCredentialIndexProvider()]);
            await store.InitializeAsync(cancellationToken);

            await using (var migrationSession = store.CreateSession())
            {
                var transaction = await migrationSession.BeginTransactionAsync(cancellationToken);
                var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

                await schemaBuilder.CreateMapIndexTableAsync<TelnyxAgentCredentialIndex>(table => table
                    .Column<string>("TenantName", column => column.WithLength(255))
                    .Column<string>("UserId", column => column.WithLength(26))
                    .Column<string>("CredentialId", column => column.WithLength(128))
                    .Column<string>("SipUsername", column => column.WithLength(128))
                    .Column<DateTime>("ExpiresUtc")
                    .Column<bool>("Revoked"));

                await transaction.CommitAsync(cancellationToken);
            }

            return new Harness(store, databasePath);
        }

        public async Task SeedAsync(params TelnyxAgentCredential[] credentials)
        {
            foreach (var credential in credentials)
            {
                await WithStoreAsync(async store =>
                {
                    await store.CreateAsync(credential);

                    return true;
                });
            }
        }

        // Each call on a session of its own, committed, the way each hub call and each dial reads the store.
        public async Task<T> WithStoreAsync<T>(Func<TelnyxAgentCredentialStore, Task<T>> action)
        {
            await using var session = _store.CreateSession();
            var result = await action(new TelnyxAgentCredentialStore(session, _shellSettings));
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            return result;
        }

        public ValueTask DisposeAsync()
        {
            TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

            return ValueTask.CompletedTask;
        }
    }
}
