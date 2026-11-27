using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
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

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

public sealed class SecureKeypadCaptureTests
{
    private const string CardDigits = "4111111111111111";

    private static readonly DateTime _start = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly MemoryDistributedCache _cache = new(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly IDataProtectionProvider _keys = new EphemeralDataProtectionProvider();
    private readonly LocalLock _lock = new(NullLogger<LocalLock>.Instance);
    private readonly Dictionary<string, string> _activities = [];
    private DateTime _now = _start;

    [Fact]
    public async Task Offer_CollectsKeysUntilThePoundKey()
    {
        // Arrange
        var store = CreateNode();
        await store.StartAsync("act-pound", SecureCaptureField.CreditCardNumber, TestContext.Current.CancellationToken);

        // Act
        var first = await store.OfferAsync("act-pound", "4111", TestContext.Current.CancellationToken);
        var second = await store.OfferAsync("act-pound", "1111", TestContext.Current.CancellationToken);
        var last = await store.OfferAsync("act-pound", "#", TestContext.Current.CancellationToken);
        var after = await store.OfferAsync("act-pound", "5", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SecureKeypadEntryState.Collecting, first.State);
        Assert.Null(first.Value);
        Assert.Equal(SecureKeypadEntryState.Collecting, second.State);
        Assert.Equal(SecureKeypadEntryState.Completed, last.State);
        Assert.Equal("41111111", last.Value);
        Assert.False(after.Captured);
    }

    [Fact]
    public async Task Offer_EndsAFullEntryWithoutThePoundKey()
    {
        // Arrange
        var store = CreateNode();
        await store.StartAsync("act-full", SecureCaptureField.CardExpiry, TestContext.Current.CancellationToken);

        // Act
        var entry = await store.OfferAsync("act-full", "1228", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SecureKeypadEntryState.Completed, entry.State);
        Assert.Equal("1228", entry.Value);
    }

    [Fact]
    public async Task Offer_CancelsOnTheStarKey()
    {
        // Arrange
        var store = CreateNode();
        await store.StartAsync("act-star", SecureCaptureField.BankAccountNumber, TestContext.Current.CancellationToken);
        await store.OfferAsync("act-star", "123", TestContext.Current.CancellationToken);

        // Act
        var entry = await store.OfferAsync("act-star", "*", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SecureKeypadEntryState.Cancelled, entry.State);
        Assert.Null(entry.Value);
    }

    [Fact]
    public async Task Offer_LeavesKeysAloneWhenTheKeypadIsNotPrivate()
    {
        // Act
        var entry = await CreateNode().OfferAsync("act-none", "1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(entry.Captured);
    }

    /// <summary>
    /// C1 (review 11): an entry nobody finished in time used to end as "not capturing", so the rest of a card number
    /// typed after three minutes became the caller's turn.
    /// </summary>
    [Fact]
    public async Task C1_AnEntryNotFinishedInTime_TimesOut_AndTheKeysThatFollowAreDropped()
    {
        // Arrange
        var store = CreateNode();
        await store.StartAsync("act-old", SecureCaptureField.CreditCardNumber, TestContext.Current.CancellationToken);
        await store.OfferAsync("act-old", "4111", TestContext.Current.CancellationToken);
        _now = _start + SecureKeypadCaptures.Expiry + TimeSpan.FromSeconds(1);

        // Act: the rest of the card, one key per webhook.
        var entries = await OfferOneByOneAsync(store, "act-old", "111111111111");

        // Assert
        Assert.Equal(SecureKeypadEntryState.TimedOut, entries[0].State);
        Assert.Equal(SecureCaptureField.CreditCardNumber, entries[0].Field);
        Assert.All(entries, entry => Assert.True(entry.Captured));
        Assert.All(entries, entry => Assert.Null(entry.Value));
        Assert.Single(entries, entry => entry.Ended);
    }

    /// <summary>
    /// C1 (review 11): the distributed cache dropped the entry (eviction, or the cache's own expiry), and the window on
    /// the activity is all that is left.
    /// </summary>
    [Fact]
    public async Task C1_AnEvictedEntry_TimesOutThroughTheWindow_AndTheKeysAreDropped()
    {
        // Arrange
        var store = CreateNode();
        await store.StartAsync("act-evicted", SecureCaptureField.CreditCardNumber, TestContext.Current.CancellationToken);
        await _cache.RemoveAsync("Default:SecureKeypadCapture:act-evicted", TestContext.Current.CancellationToken);
        _now = _start + SecureKeypadCaptures.Expiry + TimeSpan.FromSeconds(30);

        // Act
        var entries = await OfferOneByOneAsync(store, "act-evicted", CardDigits);

        // Assert
        Assert.Equal(SecureKeypadEntryState.TimedOut, entries[0].State);
        Assert.All(entries, entry => Assert.True(entry.Captured));
        Assert.True(ReadActivity("act-evicted").Has<SecureCaptureWindow>());
    }

    /// <summary>
    /// C1 (review 11): the window was lost (a stale save wrote over it) while this node still holds the entry.
    /// </summary>
    [Fact]
    public async Task C1_AnExpiredEntryWhoseWindowWasLost_StillTimesOut_AndKeepsTheKeypadPrivate()
    {
        // Arrange
        var store = CreateNode();
        await store.StartAsync("act-lost", SecureCaptureField.NationalId, TestContext.Current.CancellationToken);
        _activities.Remove("act-lost");
        _now = _start + SecureKeypadCaptures.Expiry + TimeSpan.FromSeconds(1);

        // Act
        var entries = await OfferOneByOneAsync(store, "act-lost", "123456789");

        // Assert
        Assert.Equal(SecureKeypadEntryState.TimedOut, entries[0].State);
        Assert.All(entries, entry => Assert.True(entry.Captured));
    }

    [Fact]
    public async Task C1_KeysTypedAfterATimeOut_KeepTheKeypadPrivate_UntilTheCallerStops()
    {
        // Arrange
        var store = CreateNode();
        await store.StartAsync("act-slow", SecureCaptureField.CreditCardNumber, TestContext.Current.CancellationToken);
        _now = _start + SecureKeypadCaptures.Expiry + TimeSpan.FromSeconds(1);
        await store.OfferAsync("act-slow", "4", TestContext.Current.CancellationToken);

        // Act: a key every 50 seconds, for longer than the grace period.
        var entries = new List<SecureKeypadEntry>();

        for (var i = 0; i < 6; i++)
        {
            _now += TimeSpan.FromSeconds(50);
            entries.Add(await store.OfferAsync("act-slow", "1", TestContext.Current.CancellationToken));
        }

        // Assert
        Assert.All(entries, entry => Assert.Equal(SecureKeypadEntryState.Collecting, entry.State));
    }

    [Fact]
    public async Task C1_KeysTypedAfterATimeOut_OnNodesWithTheirOwnCaches_KeepTheKeypadPrivate()
    {
        // Arrange: node A held the entry and saw it time out; the keys that follow land on node B, whose cache is its own.
        var nodeA = CreateNode(cache: NewCache());
        var nodeB = CreateNode(cache: NewCache());
        await nodeA.StartAsync("act-split", SecureCaptureField.CreditCardNumber, TestContext.Current.CancellationToken);
        _now = _start + SecureKeypadCaptures.Expiry + TimeSpan.FromSeconds(1);
        var timedOut = await nodeA.OfferAsync("act-split", "4", TestContext.Current.CancellationToken);
        var entries = new List<SecureKeypadEntry>();

        for (var i = 0; i < 3; i++)
        {
            _now += TimeSpan.FromSeconds(40);
            entries.Add(await nodeB.OfferAsync("act-split", "1", TestContext.Current.CancellationToken));
        }

        // Act: node A's own record of the timeout is older than the grace period, but the caller never stopped.
        _now += TimeSpan.FromSeconds(10);
        entries.Add(await nodeA.OfferAsync("act-split", "1", TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(SecureKeypadEntryState.TimedOut, timedOut.State);
        Assert.All(entries, entry => Assert.Equal(SecureKeypadEntryState.Collecting, entry.State));
    }

    [Fact]
    public async Task C1_AfterATimeOut_QuietTimeAlone_NeverGivesTheKeypadBack()
    {
        // Arrange (round 2, #6): the caller went quiet for more than the grace period, found the card and typed it.
        var store = CreateNode();
        await store.StartAsync("act-grace", SecureCaptureField.CreditCardNumber, TestContext.Current.CancellationToken);
        _now = _start + SecureKeypadCaptures.Expiry + TimeSpan.FromSeconds(1);
        var timedOut = await store.OfferAsync("act-grace", "1", TestContext.Current.CancellationToken);
        _now += SecureKeypadCaptures.Grace + TimeSpan.FromMinutes(5);

        // Act
        var entries = await OfferOneByOneAsync(store, "act-grace", "4111111111111111");

        // Assert
        Assert.Equal(SecureKeypadEntryState.TimedOut, timedOut.State);
        Assert.All(entries, entry => Assert.True(entry.Captured));
        Assert.True(ReadActivity("act-grace").Has<SecureCaptureWindow>());
    }

    [Fact]
    public async Task C1_AfterATimeOut_TheStarKey_GivesTheKeypadBack_AndIsDroppedItself()
    {
        // Arrange
        var store = CreateNode();
        await store.StartAsync("act-star", SecureCaptureField.CardSecurityCode, TestContext.Current.CancellationToken);
        _now = _start + SecureKeypadCaptures.Expiry + TimeSpan.FromSeconds(1);
        await store.OfferAsync("act-star", "1", TestContext.Current.CancellationToken);
        _now += TimeSpan.FromSeconds(5);

        // Act
        var star = await store.OfferAsync("act-star", "*", TestContext.Current.CancellationToken);
        var afterwards = await store.OfferAsync("act-star", "1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(star.Captured);
        Assert.False(afterwards.Captured);
        Assert.False(ReadActivity("act-star").Has<SecureCaptureWindow>());
    }

    [Fact]
    public async Task C1_ANewEntryAfterATimeOut_CollectsAsUsual()
    {
        // Arrange
        var store = CreateNode();
        await store.StartAsync("act-again", SecureCaptureField.CardExpiry, TestContext.Current.CancellationToken);
        _now = _start + SecureKeypadCaptures.Expiry + TimeSpan.FromSeconds(1);
        await store.OfferAsync("act-again", "1", TestContext.Current.CancellationToken);
        await store.StartAsync("act-again", SecureCaptureField.CardExpiry, TestContext.Current.CancellationToken);

        // Act
        var entry = await store.OfferAsync("act-again", "1228", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SecureKeypadEntryState.Completed, entry.State);
        Assert.Equal("1228", entry.Value);
        Assert.False(ReadActivity("act-again").Has<SecureCaptureWindow>());
    }

    [Fact]
    public async Task C1_CompleteAsync_TellsTheAITheEntryTimedOut()
    {
        // Arrange
        var entry = new SecureKeypadEntry(SecureKeypadEntryState.TimedOut, SecureCaptureField.CreditCardNumber, null);

        // Act
        var turn = await SecureKeypadEntries.CompleteAsync(new ServiceCollection().BuildServiceProvider(), "act-1", entry, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("ran out", turn, StringComparison.Ordinal);
        Assert.Contains("card number", turn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SEC_P2_KeysThatLandOnAnotherNode_AreCaptured_WhenTheNodesShareTheCache()
    {
        // Arrange: the AI session that asked for the card runs on node A; the provider's keypad events land on node B,
        // and both use the shared (Redis) cache.
        var nodeA = CreateNode();
        var nodeB = CreateNode();
        await nodeA.StartAsync("act-nodes", SecureCaptureField.CreditCardNumber, TestContext.Current.CancellationToken);

        // Act
        var digits = await nodeB.OfferAsync("act-nodes", "4111111111111111", TestContext.Current.CancellationToken);
        var pound = await nodeA.OfferAsync("act-nodes", "#", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(digits.Captured);
        Assert.Null(digits.Value);
        Assert.Equal(SecureKeypadEntryState.Completed, pound.State);
        Assert.Equal("4111111111111111", pound.Value);
    }

    [Fact]
    public async Task SEC_P2_KeysThatLandOnANodeWithItsOwnCache_AreAllDropped_WhileTheWindowIsOpen()
    {
        // Arrange: each node has its own memory cache, as on a site without the Redis cache, so node B never finds the
        // entry node A started. Only the window on the activity, which both read, is shared.
        var nodeA = CreateNode(cache: NewCache());
        var nodeB = CreateNode(cache: NewCache());
        await nodeA.StartAsync("act-own-cache", SecureCaptureField.CreditCardNumber, TestContext.Current.CancellationToken);

        // Act: one key per webhook, the way Telnyx sends them.
        var entries = new List<SecureKeypadEntry>();

        foreach (var key in "4111111111111111")
        {
            entries.Add(await nodeB.OfferAsync("act-own-cache", key.ToString(), TestContext.Current.CancellationToken));
        }

        // Assert
        Assert.All(entries, entry => Assert.True(entry.Captured));
        Assert.All(entries, entry => Assert.Null(entry.Value));
    }

    [Fact]
    public async Task SEC_P2_TheWindowCloses_WhenTheEntryIsFinishedOrCancelled()
    {
        // Arrange
        var store = CreateNode();
        await store.StartAsync("act-close", SecureCaptureField.CardSecurityCode, TestContext.Current.CancellationToken);
        await store.OfferAsync("act-close", "123#", TestContext.Current.CancellationToken);
        await store.StartAsync("act-cancel", SecureCaptureField.CardSecurityCode, TestContext.Current.CancellationToken);
        await store.OfferAsync("act-cancel", "*", TestContext.Current.CancellationToken);

        // Act
        var afterFinished = await CreateNode(cache: NewCache()).OfferAsync("act-close", "1", TestContext.Current.CancellationToken);
        var afterCancelled = await CreateNode(cache: NewCache()).OfferAsync("act-cancel", "1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(afterFinished.Captured);
        Assert.False(afterCancelled.Captured);
        Assert.False(ReadActivity("act-close").Has<SecureCaptureWindow>());
    }

    [Fact]
    public async Task TheDigits_AreNeverStoredInTheClear()
    {
        // Arrange
        var store = CreateNode();
        await store.StartAsync("act-clear", SecureCaptureField.CreditCardNumber, TestContext.Current.CancellationToken);
        await store.OfferAsync("act-clear", "4111111111111111", TestContext.Current.CancellationToken);

        // Act
        var stored = await _cache.GetAsync("Default:SecureKeypadCapture:act-clear", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(stored);
        Assert.DoesNotContain("4111111111111111", System.Text.Encoding.UTF8.GetString(stored), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEntryANodeCantRead_IsCancelled_AndNoLaterKeyIsHandedOn()
    {
        // Arrange: a node whose data protection keys differ can't read the entry.
        await CreateNode().StartAsync("act-keys", SecureCaptureField.CreditCardNumber, TestContext.Current.CancellationToken);
        var stranger = CreateNode(new EphemeralDataProtectionProvider());

        // Act: one key per webhook, the way Telnyx sends them.
        var entries = new List<SecureKeypadEntry>();

        foreach (var key in "4111111111111111")
        {
            entries.Add(await stranger.OfferAsync("act-keys", key.ToString(), TestContext.Current.CancellationToken));
        }

        // Assert
        Assert.Equal(SecureKeypadEntryState.Cancelled, entries[0].State);
        Assert.All(entries, entry => Assert.True(entry.Captured));
        Assert.All(entries, entry => Assert.Null(entry.Value));
    }

    [Fact]
    public async Task ForgetAsync_EndsTheEntry_WhenTheCallEnds()
    {
        // Arrange
        var store = CreateNode();
        await store.StartAsync("act-hangup", SecureCaptureField.CreditCardNumber, TestContext.Current.CancellationToken);

        // Act
        await store.ForgetAsync("act-hangup", TestContext.Current.CancellationToken);

        // Assert
        Assert.False((await store.OfferAsync("act-hangup", "4", TestContext.Current.CancellationToken)).Captured);
    }

    [Fact]
    public async Task CompleteAsync_TellsTheAITheMaskedValueAndNeverTheDigits()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ISecureCaptureTokenSink, MaskingSecureCaptureTokenSink>()
            .BuildServiceProvider();
        var entry = new SecureKeypadEntry(SecureKeypadEntryState.Completed, SecureCaptureField.CreditCardNumber, "4111111111111111");

        // Act
        var turn = await SecureKeypadEntries.CompleteAsync(services, "act-1", entry, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("4111111111111111", turn, StringComparison.Ordinal);
        Assert.Contains("1111", turn, StringComparison.Ordinal);
        Assert.Contains("card number", turn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteAsync_AsksAgainWhenTheEntryIsNotValid()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ISecureCaptureTokenSink, MaskingSecureCaptureTokenSink>()
            .BuildServiceProvider();
        var entry = new SecureKeypadEntry(SecureKeypadEntryState.Completed, SecureCaptureField.CreditCardNumber, "1234");

        // Act
        var turn = await SecureKeypadEntries.CompleteAsync(services, "act-1", entry, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("type it again", turn, StringComparison.Ordinal);
        Assert.DoesNotContain("1234", turn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteAsync_WithoutSecureCapture_SavesNothing()
    {
        // Arrange
        var entry = new SecureKeypadEntry(SecureKeypadEntryState.Completed, SecureCaptureField.CreditCardNumber, "4111111111111111");

        // Act
        var turn = await SecureKeypadEntries.CompleteAsync(new ServiceCollection().BuildServiceProvider(), "act-1", entry, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("not available", turn, StringComparison.Ordinal);
    }

    [Fact]
    public void TheToolIsOffered_OnlyWhereSecureCaptureIsOn()
    {
        // Arrange
        var withSink = new ServiceCollection().AddSingleton<ISecureCaptureTokenSink, MaskingSecureCaptureTokenSink>().BuildServiceProvider();
        var withoutSink = new ServiceCollection().BuildServiceProvider();

        // Assert
        Assert.True(VoiceAgentConversationLoop.OffersSecureKeypad(withSink));
        Assert.False(VoiceAgentConversationLoop.OffersSecureKeypad(withoutSink));
        Assert.False(VoiceAgentConversationLoop.OffersSecureKeypad(null));
    }

    private static async Task<List<SecureKeypadEntry>> OfferOneByOneAsync(DistributedSecureKeypadCaptureStore store, string activityId, string keys)
    {
        var entries = new List<SecureKeypadEntry>();

        foreach (var key in keys)
        {
            entries.Add(await store.OfferAsync(activityId, key.ToString(), TestContext.Current.CancellationToken));
        }

        return entries;
    }

    private static MemoryDistributedCache NewCache()
        => new(Options.Create(new MemoryDistributedCacheOptions()));

    private OmnichannelActivity ReadActivity(string activityId)
        => _activities.TryGetValue(activityId, out var json)
            ? JsonSerializer.Deserialize<OmnichannelActivity>(json, JOptions.Default)
            : new OmnichannelActivity { ItemId = activityId };

    // Every node reads the same activities, each read its own copy of what was last saved, as from the database.
    private IOmnichannelActivityStore CreateActivityStore()
    {
        var store = new Mock<IOmnichannelActivityStore>();
        store.Setup(s => s.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => ReadActivity(id));
        store.Setup(s => s.UpdateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>()))
            .Callback<OmnichannelActivity, CancellationToken>((activity, _) => _activities[activity.ItemId] = JsonSerializer.Serialize(activity, JOptions.Default));

        return store.Object;
    }

    // A scope of its own reads and saves the same activities; its session commits what was saved.
    private async Task InScopeOfItsOwnAsync(Func<IServiceProvider, Task> work)
    {
        await using var services = new ServiceCollection()
            .AddSingleton(CreateActivityStore())
            .AddSingleton(Mock.Of<global::YesSql.ISession>())
            .BuildServiceProvider();

        await work(services);
    }

    private DistributedSecureKeypadCaptureStore CreateNode(IDataProtectionProvider keys = null, IDistributedCache cache = null)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(() => _now);

        return new DistributedSecureKeypadCaptureStore(
            cache ?? _cache,
            CreateActivityStore(),
            Mock.Of<global::YesSql.ISession>(),
            _lock,
            keys ?? _keys,
            InScopeOfItsOwnAsync,
            new ShellSettings { Name = "Default" },
            clock.Object,
            NullLogger<DistributedSecureKeypadCaptureStore>.Instance);
    }
}
