using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class DefaultTelephonyServiceTests
{
    [Fact]
    public async Task DialAsync_DelegatesToProvider_AndReturnsResult()
    {
        // Arrange
        var call = new TelephonyCall { CallId = "call-1", State = CallState.Connecting };
        var provider = new RecordingTelephonyProvider { ResultToReturn = TelephonyResult.Success(call) };
        var service = new DefaultTelephonyService(
            new StubTelephonyProviderResolver(provider),
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var result = await service.DialAsync(new DialRequest { To = "+15551234567" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("Dial", provider.LastOperation);
        Assert.Same(call, result.Call);
    }

    [Fact]
    public async Task HangupAsync_DelegatesToProvider()
    {
        // Arrange
        var provider = new RecordingTelephonyProvider();
        var service = new DefaultTelephonyService(
            new StubTelephonyProviderResolver(provider),
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        await service.HangupAsync(new CallReference { CallId = "call-1" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Hangup", provider.LastOperation);
    }

    [Fact]
    public async Task DialAsync_WhenNoProviderConfigured_ReturnsFailed()
    {
        // Arrange
        var service = new DefaultTelephonyService(
            new StubTelephonyProviderResolver(null),
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var result = await service.DialAsync(new DialRequest { To = "+15551234567" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Theory]
    [InlineData("Dial", TelephonyCapabilities.Dial)]
    [InlineData("Hangup", TelephonyCapabilities.Hangup)]
    [InlineData("Hold", TelephonyCapabilities.Hold)]
    [InlineData("Resume", TelephonyCapabilities.Resume)]
    [InlineData("Mute", TelephonyCapabilities.Mute)]
    [InlineData("Unmute", TelephonyCapabilities.Mute)]
    [InlineData("Transfer", TelephonyCapabilities.Transfer)]
    [InlineData("Merge", TelephonyCapabilities.Merge)]
    [InlineData("SendDigits", TelephonyCapabilities.SendDigits)]
    [InlineData("Answer", TelephonyCapabilities.ReceiveCalls)]
    [InlineData("Reject", TelephonyCapabilities.ReceiveCalls)]
    [InlineData("SendToVoicemail", TelephonyCapabilities.Voicemail)]
    public async Task OperationAsync_WhenProviderAdvertisesCapability_Delegates(
        string operation,
        TelephonyCapabilities capability)
    {
        // Arrange
        var provider = new RecordingTelephonyProvider { Capabilities = capability };
        var service = new DefaultTelephonyService(
            new StubTelephonyProviderResolver(provider),
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var result = await InvokeAsync(service, operation);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(operation, provider.LastOperation);
    }

    [Theory]
    [InlineData("Dial")]
    [InlineData("Hangup")]
    [InlineData("Hold")]
    [InlineData("Resume")]
    [InlineData("Mute")]
    [InlineData("Unmute")]
    [InlineData("Transfer")]
    [InlineData("Merge")]
    [InlineData("SendDigits")]
    [InlineData("Answer")]
    [InlineData("Reject")]
    [InlineData("SendToVoicemail")]
    public async Task OperationAsync_WhenProviderDoesNotAdvertiseCapability_FailsClosed(string operation)
    {
        // Arrange
        var provider = new RecordingTelephonyProvider { Capabilities = TelephonyCapabilities.None };
        var service = new DefaultTelephonyService(
            new StubTelephonyProviderResolver(provider),
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var result = await InvokeAsync(service, operation);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(provider.LastOperation);
    }

    [Fact]
    public async Task GetCapabilitiesAsync_ReturnsProviderCapabilities()
    {
        // Arrange
        var provider = new RecordingTelephonyProvider { Capabilities = TelephonyCapabilities.Dial | TelephonyCapabilities.Transfer };
        var service = new DefaultTelephonyService(
            new StubTelephonyProviderResolver(provider),
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var capabilities = await service.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelephonyCapabilities.Dial | TelephonyCapabilities.Transfer, capabilities);
    }

    [Fact]
    public async Task GetCapabilitiesAsync_WhenNoProvider_ReturnsNone()
    {
        // Arrange
        var service = new DefaultTelephonyService(
            new StubTelephonyProviderResolver(null),
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var capabilities = await service.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelephonyCapabilities.None, capabilities);
    }

    [Theory]
    [InlineData("browser-adapter", TelephonyAudioMode.Browser)]
    [InlineData(null, TelephonyAudioMode.None)]
    public async Task GetClientCredentialsAsync_BrowserAudioRequiresExecutableAdapter(
        string browserMediaAdapterName,
        TelephonyAudioMode expectedMode)
    {
        // Arrange
        var provider = new RecordingTelephonyProvider
        {
            AudioCapabilities = TelephonyAudioCapabilities.Browser,
            ConfiguredAudioMode = TelephonyAudioMode.Browser,
            BrowserMediaAdapterName = browserMediaAdapterName,
        };
        var service = new DefaultTelephonyService(
            new StubTelephonyProviderResolver(provider),
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var credentials = await service.GetClientCredentialsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedMode, credentials.AudioMode);
        Assert.Equal(browserMediaAdapterName, credentials.BrowserMediaAdapterName);
    }

    [Fact]
    public async Task GetDirectoryAsync_WhenProviderSupportsDirectory_ReturnsEntries()
    {
        // Arrange
        var provider = new RecordingTelephonyProvider
        {
            Capabilities = TelephonyCapabilities.Directory,
        };
        var service = new DefaultTelephonyService(
            new StubTelephonyProviderResolver(provider),
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var result = await service.GetDirectoryAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("GetDirectory", provider.LastOperation);
        Assert.Single(result.Entries);
    }

    [Fact]
    public async Task GetDirectoryAsync_WhenProviderDoesNotSupportDirectory_ReturnsFailed()
    {
        // Arrange
        var provider = new RecordingTelephonyProvider
        {
            Capabilities = TelephonyCapabilities.Dial,
        };
        var service = new DefaultTelephonyService(
            new StubTelephonyProviderResolver(provider),
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var result = await service.GetDirectoryAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Error);
    }

    private static Task<TelephonyResult> InvokeAsync(DefaultTelephonyService service, string operation)
    {
        var call = new CallReference { CallId = "call-1" };

        return operation switch
        {
            "Dial" => service.DialAsync(new DialRequest { To = "+15551234567" }),
            "Hangup" => service.HangupAsync(call),
            "Hold" => service.HoldAsync(call),
            "Resume" => service.ResumeAsync(call),
            "Mute" => service.MuteAsync(call),
            "Unmute" => service.UnmuteAsync(call),
            "Transfer" => service.TransferAsync(new TransferRequest
            {
                CallId = call.CallId,
                To = "+15557654321",
            }),
            "Merge" => service.MergeAsync(new MergeRequest
            {
                CallIds = [call.CallId],
            }),
            "SendDigits" => service.SendDigitsAsync(new SendDigitsRequest
            {
                CallId = call.CallId,
                Digits = "1",
            }),
            "Answer" => service.AnswerAsync(call),
            "Reject" => service.RejectAsync(call),
            "SendToVoicemail" => service.SendToVoicemailAsync(call),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };
    }
}
