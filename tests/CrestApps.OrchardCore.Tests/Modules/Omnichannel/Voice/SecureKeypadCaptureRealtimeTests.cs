using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// SEC-P2 / CQ-1, second review: the secure keypad window on a live (realtime) call, on the real activity store over
/// SQLite, which checks concurrency. The window was saved through the call's scope, which commits only when the response
/// that asked for the entry completes: keys the caller typed while the AI was still saying "type it now" found no window
/// on a node without the entry, and went to the AI; and any other write to the activity in those seconds made the call's
/// commit fail, which the call swallowed, so the window was never stored. (A second entry on its own keeps its window:
/// YesSql forgets what a session read each time it commits, and the call commits every turn.)
/// </summary>
public sealed class SecureKeypadCaptureRealtimeTests
{
    private static readonly DateTime _now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly IDataProtectionProvider _keys = new EphemeralDataProtectionProvider();
    private readonly LocalLock _lock = new(NullLogger<LocalLock>.Instance);

    [Fact]
    public async Task ASecondEntryOnALiveCall_KeepsItsWindow_SoKeysOnANodeWithoutTheEntryAreDropped()
    {
        // Arrange
        var databasePath = OmnichannelActivityStoreTests.DatabasePath("secure-keypad");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await OmnichannelActivityStoreTests.CreateStoreAsync(connectionString);
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            string activityId;

            await using (var seed = store.CreateSession())
            {
                activityId = await OmnichannelActivityStoreTests.SaveActivityAsync(seed, ActivityStatus.InProgress);
                await seed.SaveChangesAsync(cancellationToken);
            }

            // The call's scope lasts as long as the call, and its node keeps the entries in its own memory cache.
            var callCache = NewCache();
            await using var callSession = store.CreateSession();
            var callActivities = OmnichannelActivityStoreTests.CreateActivityStore(callSession, store, connectionString);
            var callNode = CreateNode(store, connectionString, callCache, callActivities);

            // Entry 1, the card number: the tool reads the activity through the call's scope, then opens the keypad.
            Assert.NotNull(await callActivities.FindByIdAsync(activityId, cancellationToken));
            await callNode.StartAsync(activityId, SecureCaptureField.CreditCardNumber, cancellationToken);
            await CommitAsTheCallDoesAsync(callSession);

            // The keypad events of entry 1 reach the call's node, each in a request of its own; the pound key closes it.
            var finished = await OfferInARequestAsync(store, connectionString, callCache, activityId, "4111111111111111#");

            Assert.Equal(SecureKeypadEntryState.Completed, finished.State);

            // Entry 2, the expiry date, asked for through the same call scope.
            Assert.NotNull(await callActivities.FindByIdAsync(activityId, cancellationToken));
            await callNode.StartAsync(activityId, SecureCaptureField.CardExpiry, cancellationToken);
            await CommitAsTheCallDoesAsync(callSession);

            // Act: the first key of entry 2 lands on another node, whose memory cache has no entry.
            var key = await OfferInARequestAsync(store, connectionString, NewCache(), activityId, "1");

            // Assert
            Assert.True(key.Captured, "A key typed while the keypad is private must never be handed to the AI.");
            Assert.Null(key.Value);

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
    public async Task KeysTypedBeforeTheCallCommits_AreDroppedOnANodeWithoutTheEntry()
    {
        // Arrange
        var databasePath = OmnichannelActivityStoreTests.DatabasePath("secure-keypad-early");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await OmnichannelActivityStoreTests.CreateStoreAsync(connectionString);
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            var activityId = await SeedAsync(store);
            await using var callSession = store.CreateSession();
            var callActivities = OmnichannelActivityStoreTests.CreateActivityStore(callSession, store, connectionString);

            Assert.NotNull(await callActivities.FindByIdAsync(activityId, cancellationToken));
            await CreateNode(store, connectionString, NewCache(), callActivities).StartAsync(activityId, SecureCaptureField.CreditCardNumber, cancellationToken);

            // Act: the response that asked for the card is still running; the first key reaches another node.
            var key = await OfferInARequestAsync(store, connectionString, NewCache(), activityId, "4");

            // Assert
            Assert.True(key.Captured, "A key typed while the keypad is private must never be handed to the AI.");
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task AnotherWriteWhileTheResponseRuns_DoesntLoseTheWindow()
    {
        // Arrange
        var databasePath = OmnichannelActivityStoreTests.DatabasePath("secure-keypad-race");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await OmnichannelActivityStoreTests.CreateStoreAsync(connectionString);
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            var activityId = await SeedAsync(store);
            await using var callSession = store.CreateSession();
            var callActivities = OmnichannelActivityStoreTests.CreateActivityStore(callSession, store, connectionString);

            Assert.NotNull(await callActivities.FindByIdAsync(activityId, cancellationToken));
            await CreateNode(store, connectionString, NewCache(), callActivities).StartAsync(activityId, SecureCaptureField.CreditCardNumber, cancellationToken);

            // Act: a provider webhook writes the activity before the response completes, then the call commits.
            await using (var webhook = store.CreateSession())
            {
                var activities = OmnichannelActivityStoreTests.CreateActivityStore(webhook, store, connectionString);
                var activity = await activities.FindByIdAsync(activityId, cancellationToken);

                activity.Notes = "Recording started.";
                await activities.UpdateAsync(activity, cancellationToken);
                await webhook.SaveChangesAsync(cancellationToken);
            }

            await CommitAsTheCallDoesAsync(callSession);
            var key = await OfferInARequestAsync(store, connectionString, NewCache(), activityId, "4");

            // Assert
            Assert.True(key.Captured, "A key typed while the keypad is private must never be handed to the AI.");
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task<string> SeedAsync(IStore store)
    {
        await using var seed = store.CreateSession();
        var activityId = await OmnichannelActivityStoreTests.SaveActivityAsync(seed, ActivityStatus.InProgress);

        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);

        return activityId;
    }

    private static MemoryDistributedCache NewCache()
        => new(Options.Create(new MemoryDistributedCacheOptions()));

    // The live call commits its scope after each turn and response, and goes on when the database refuses the commit
    // (RealtimeVoiceConversationRunner.Transcript.cs).
    private static async Task CommitAsTheCallDoesAsync(ISession callSession)
    {
        try
        {
            await callSession.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        catch (ConcurrencyException)
        {
            // Swallowed by the call, as in production.
        }
    }

    // A keypad webhook: a request scope of its own, committed when the request ends.
    private async Task<SecureKeypadEntry> OfferInARequestAsync(IStore store, string connectionString, IDistributedCache nodeCache, string activityId, string digits)
    {
        await using var requestSession = store.CreateSession();
        var node = CreateNode(store, connectionString, nodeCache, OmnichannelActivityStoreTests.CreateActivityStore(requestSession, store, connectionString));
        var entry = await node.OfferAsync(activityId, digits, TestContext.Current.CancellationToken);

        await requestSession.SaveChangesAsync(TestContext.Current.CancellationToken);

        return entry;
    }

    private DistributedSecureKeypadCaptureStore CreateNode(IStore store, string connectionString, IDistributedCache nodeCache, IOmnichannelActivityStore activities)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(_now);

        return new DistributedSecureKeypadCaptureStore(
            nodeCache,
            activities,
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
