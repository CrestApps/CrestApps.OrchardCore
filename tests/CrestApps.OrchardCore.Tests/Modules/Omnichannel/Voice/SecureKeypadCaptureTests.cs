using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

public sealed class SecureKeypadCaptureTests
{
    private static readonly DateTime _now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Offer_CollectsKeysUntilThePoundKey()
    {
        // Arrange
        SecureKeypadCaptures.Start("act-pound", SecureCaptureField.CreditCardNumber, _now);

        // Act
        var first = SecureKeypadCaptures.Offer("act-pound", "4111", _now);
        var second = SecureKeypadCaptures.Offer("act-pound", "1111", _now);
        var last = SecureKeypadCaptures.Offer("act-pound", "#", _now);

        // Assert
        Assert.Equal(SecureKeypadEntryState.Collecting, first.State);
        Assert.Null(first.Value);
        Assert.Equal(SecureKeypadEntryState.Collecting, second.State);
        Assert.Equal(SecureKeypadEntryState.Completed, last.State);
        Assert.Equal("41111111", last.Value);
        Assert.False(SecureKeypadCaptures.IsCapturing("act-pound", _now));
    }

    [Fact]
    public void Offer_EndsAFullEntryWithoutThePoundKey()
    {
        // Arrange
        SecureKeypadCaptures.Start("act-full", SecureCaptureField.CardExpiry, _now);

        // Act
        var entry = SecureKeypadCaptures.Offer("act-full", "1228", _now);

        // Assert
        Assert.Equal(SecureKeypadEntryState.Completed, entry.State);
        Assert.Equal("1228", entry.Value);
    }

    [Fact]
    public void Offer_CancelsOnTheStarKey()
    {
        // Arrange
        SecureKeypadCaptures.Start("act-star", SecureCaptureField.BankAccountNumber, _now);
        SecureKeypadCaptures.Offer("act-star", "123", _now);

        // Act
        var entry = SecureKeypadCaptures.Offer("act-star", "*", _now);

        // Assert
        Assert.Equal(SecureKeypadEntryState.Cancelled, entry.State);
        Assert.Null(entry.Value);
    }

    [Fact]
    public void Offer_LeavesKeysAloneWhenTheKeypadIsNotPrivate()
    {
        // Act
        var entry = SecureKeypadCaptures.Offer("act-none", "1", _now);

        // Assert
        Assert.False(entry.Captured);
    }

    [Fact]
    public void Offer_ForgetsAnEntryNobodyFinished()
    {
        // Arrange
        SecureKeypadCaptures.Start("act-old", SecureCaptureField.NationalId, _now);

        // Act
        var entry = SecureKeypadCaptures.Offer("act-old", "1", _now + SecureKeypadCaptures.Expiry + TimeSpan.FromSeconds(1));

        // Assert
        Assert.False(entry.Captured);
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
}
