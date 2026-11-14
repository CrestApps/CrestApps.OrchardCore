using CrestApps.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// M6 (final security review): the secure keypad window on a turn-based call, on the real activity store over SQLite.
/// The window is written in a scope of its own, but a turn-based call asks for an entry from the request of the caller's
/// turn: the keypad event that finished the card number closed its window in that request, the next read flushed it,
/// and the request's transaction held the activity's row (on SQLite, the database's write lock) while the model asked
/// for the expiry date. The window's own write waited on the request that was waiting for it, until the command timed
/// out with a provider error the tool did not catch, so no window was open and the model was free to ask anyway.
/// </summary>
public sealed class SecureKeypadCaptureTurnBasedTests
{
    private static readonly DateTime _now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly IDataProtectionProvider _keys = new EphemeralDataProtectionProvider();
    private readonly LocalLock _lock = new(NullLogger<LocalLock>.Instance);

    [Fact]
    public async Task TheRequestThatFinishedAnEntry_AsksForTheNext_WithoutWaitingOnItself()
    {
        // Arrange: a blocked write gives up after two seconds instead of thirty.
        var databasePath = OmnichannelActivityStoreTests.DatabasePath("secure-keypad-turn");
        var connectionString = $"Data Source={databasePath};Pooling=False;Default Timeout=2";
        var store = await OmnichannelActivityStoreTests.CreateStoreAsync(connectionString);
        var cancellationToken = TestContext.Current.CancellationToken;
        var nodeCache = NewCache();

        try
        {
            var activityId = await SeedTurnBasedCallAsync(store, connectionString);

            // Entry 1, the card number, asked for on an earlier turn and committed with it.
            await using (var earlierTurn = store.CreateSession())
            {
                await CreateNode(store, connectionString, nodeCache, earlierTurn).StartAsync(activityId, SecureCaptureField.CreditCardNumber, cancellationToken);
                await earlierTurn.SaveChangesAsync(cancellationToken);
            }

            // The keypad event's request: the pound key finishes entry 1 and closes its window.
            await using var request = store.CreateSession();
            var requestActivities = OmnichannelActivityStoreTests.CreateActivityStore(request, store, connectionString);
            var node = CreateNode(store, connectionString, nodeCache, request, requestActivities);
            var finished = await node.OfferAsync(activityId, "4111111111111111#", cancellationToken);

            Assert.Equal(SecureKeypadEntryState.Completed, finished.State);

            // The turn goes on in the same request: its next read flushes the closed window, so the request's transaction
            // now holds the write.
            Assert.NotNull(await requestActivities.FindByIdAsync(activityId, cancellationToken));

            // Act: the model, answering the turn, asks for the expiry date from the same request.
            await node.StartAsync(activityId, SecureCaptureField.CardExpiry, cancellationToken);
            await request.SaveChangesAsync(cancellationToken);

            // Assert: the expiry date's window is stored, so a key that lands on another node is dropped.
            var key = await OfferInARequestAsync(store, connectionString, NewCache(), activityId, "1");

            Assert.True(key.Captured, "A key typed while the keypad is private must never be handed to the AI.");

            await using var check = store.CreateSession();
            var activity = await OmnichannelActivityStoreTests.CreateActivityStore(check, store, connectionString).FindByIdAsync(activityId, cancellationToken);

            Assert.True(activity.TryGet<SecureCaptureWindow>(out var window));
            Assert.Equal(SecureCaptureField.CardExpiry, window.Field);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task WhenTheKeypadCantBeMadePrivate_ForAnyReason_TheToolTellsTheModelNotToAsk()
    {
        // Arrange: the database refused the window's write with its own error, not a concurrency conflict.
        var activityId = IdGenerator.GenerateId();
        var activity = new OmnichannelActivity { ItemId = activityId };
        activity.Put(new AILiveCall { ProviderName = "Twilio", ProviderCallId = "call-1" });

        var activities = new Mock<IOmnichannelActivityStore>();
        activities.Setup(s => s.FindByIdAsync(activityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);

        var captures = new Mock<ISecureKeypadCaptureStore>();
        captures.Setup(s => s.StartAsync(activityId, It.IsAny<SecureCaptureField>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SqliteException("SQLite Error 5: 'database is locked'.", 5));

        var services = new ServiceCollection()
            .AddSingleton(activities.Object)
            .AddSingleton(captures.Object)
            .AddSingleton(Mock.Of<ISecureCaptureTokenSink>())
            .BuildServiceProvider();
        var arguments = new AIFunctionArguments(new Dictionary<string, object> { ["field"] = "CardExpiry" }) { Services = services };

        // Act
        object answer;

        using (AIConversationScope.Begin(new AIConversationContext { ActivityId = activityId }))
        {
            answer = await new CollectSecureDigitsTool().InvokeAsync(arguments, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Contains("Do not ask for the number", answer?.ToString(), StringComparison.Ordinal);
    }

    private static async Task<string> SeedTurnBasedCallAsync(IStore store, string connectionString)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        string activityId;

        await using (var seed = store.CreateSession())
        {
            activityId = await OmnichannelActivityStoreTests.SaveActivityAsync(seed, ActivityStatus.InProgress);
            await seed.SaveChangesAsync(cancellationToken);
        }

        await using var answer = store.CreateSession();
        var activities = OmnichannelActivityStoreTests.CreateActivityStore(answer, store, connectionString);
        var activity = await activities.FindByIdAsync(activityId, cancellationToken);

        activity.Put(new AILiveCall { ProviderName = "Twilio", ProviderCallId = "call-1", AnsweredUtc = _now, IsRealtime = false });
        await activities.UpdateAsync(activity, cancellationToken);
        await answer.SaveChangesAsync(cancellationToken);

        return activityId;
    }

    private static MemoryDistributedCache NewCache()
        => new(Options.Create(new MemoryDistributedCacheOptions()));

    // A keypad webhook: a request scope of its own, committed when the request ends.
    private async Task<SecureKeypadEntry> OfferInARequestAsync(IStore store, string connectionString, IDistributedCache nodeCache, string activityId, string digits)
    {
        await using var requestSession = store.CreateSession();
        var entry = await CreateNode(store, connectionString, nodeCache, requestSession).OfferAsync(activityId, digits, TestContext.Current.CancellationToken);

        await requestSession.SaveChangesAsync(TestContext.Current.CancellationToken);

        return entry;
    }

    private DistributedSecureKeypadCaptureStore CreateNode(IStore store, string connectionString, IDistributedCache nodeCache, ISession session, IOmnichannelActivityStore activities = null)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(_now);

        return new DistributedSecureKeypadCaptureStore(
            nodeCache,
            activities ?? OmnichannelActivityStoreTests.CreateActivityStore(session, store, connectionString),
            session,
            _lock,
            _keys,
            work => InScopeOfItsOwnAsync(store, connectionString, work),
            new ShellSettings { Name = "Default" },
            clock.Object,
            NullLogger<DistributedSecureKeypadCaptureStore>.Instance);
    }

    // A tenant scope of its own: a new session, committed when the scope ends.
    private static async Task InScopeOfItsOwnAsync(IStore store, string connectionString, Func<IServiceProvider, Task> work)
    {
        await using var session = store.CreateSession();
        await using var services = new ServiceCollection()
            .AddSingleton(session)
            .AddSingleton<IOmnichannelActivityStore>(OmnichannelActivityStoreTests.CreateActivityStore(session, store, connectionString))
            .BuildServiceProvider();

        await work(services);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
