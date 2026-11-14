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

    [Fact]
    public async Task Offer_ForgetsAnEntryNobodyFinished()
    {
        // Arrange
        var store = CreateNode();
        await store.StartAsync("act-old", SecureCaptureField.NationalId, TestContext.Current.CancellationToken);
        _now = _start + SecureKeypadCaptures.Expiry + TimeSpan.FromSeconds(1);

        // Act
        var entry = await store.OfferAsync("act-old", "1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(entry.Captured);
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
