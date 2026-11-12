using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
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
    public async Task SEC_P2_KeysThatLandOnAnotherNode_AreCaptured_NotHandedOnAsTheCallersTurn()
    {
        // Arrange: the AI session that asked for the card runs on node A; the provider's keypad events land on node B.
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
    public async Task AnEntryANodeCantRead_IsCancelled_NotHandedOn()
    {
        // Arrange: a node whose data protection keys differ can't read the entry.
        await CreateNode().StartAsync("act-keys", SecureCaptureField.CreditCardNumber, TestContext.Current.CancellationToken);
        var stranger = CreateNode(new EphemeralDataProtectionProvider());

        // Act
        var entry = await stranger.OfferAsync("act-keys", "4111", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(entry.Captured);
        Assert.Equal(SecureKeypadEntryState.Cancelled, entry.State);
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

    private DistributedSecureKeypadCaptureStore CreateNode(IDataProtectionProvider keys = null)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(() => _now);

        return new DistributedSecureKeypadCaptureStore(
            _cache,
            _lock,
            keys ?? _keys,
            new ShellSettings { Name = "Default" },
            clock.Object,
            NullLogger<DistributedSecureKeypadCaptureStore>.Instance);
    }
}
