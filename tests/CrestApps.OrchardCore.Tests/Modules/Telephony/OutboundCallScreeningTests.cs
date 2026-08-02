using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Telephony;

public sealed class OutboundCallScreeningTests
{
    [Fact]
    public async Task DialAsync_WhenAScreenerDenies_DoesNotReachTheProvider()
    {
        // Arrange
        var provider = new RecordingCallControlProvider();
        var resolver = new Mock<ITelephonyProviderResolver>();
        resolver
            .Setup(r => r.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(provider);

        var screener = new StubScreener(OutboundCallScreeningResult.Deny("do-not-call", "The destination is on a do-not-call list."));
        var screeningService = new DefaultOutboundCallScreeningService([screener]);

        var service = new DefaultTelephonyService(
            resolver.Object,
            screeningService,
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var result = await service.DialAsync(new DialRequest { To = "+14255551212" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("The destination is on a do-not-call list.", result.Error);
        Assert.False(provider.DialWasCalled);
    }

    [Fact]
    public async Task DialAsync_WhenEveryScreenerAllows_ReachesTheProvider()
    {
        // Arrange
        var provider = new RecordingCallControlProvider();
        var resolver = new Mock<ITelephonyProviderResolver>();
        resolver
            .Setup(r => r.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(provider);

        var screeningService = new DefaultOutboundCallScreeningService([new StubScreener(OutboundCallScreeningResult.Allow())]);

        var service = new DefaultTelephonyService(
            resolver.Object,
            screeningService,
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var result = await service.DialAsync(new DialRequest { To = "+14255551212" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(provider.DialWasCalled);
    }

    [Fact]
    public async Task ScreeningService_WithNoScreeners_Allows()
    {
        // Arrange
        var screeningService = new DefaultOutboundCallScreeningService([]);

        // Act
        var result = await screeningService.ScreenAsync(
            new OutboundCallScreeningContext { Request = new DialRequest { To = "+14255551212" } },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ScreeningService_ReturnsTheFirstDenial()
    {
        // Arrange
        var allowing = new StubScreener(OutboundCallScreeningResult.Allow());
        var denying = new StubScreener(OutboundCallScreeningResult.Deny("calling-window", "Outside the calling window."));
        var screeningService = new DefaultOutboundCallScreeningService([allowing, denying]);

        // Act
        var result = await screeningService.ScreenAsync(
            new OutboundCallScreeningContext { Request = new DialRequest { To = "+14255551212" } },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal("calling-window", result.Reason);
    }

    [Fact]
    public async Task ScreeningService_WhenAScreenerReturnsNoVerdict_FailsClosed()
    {
        // Arrange
        var screeningService = new DefaultOutboundCallScreeningService([new NullScreener()]);

        // Act
        var result = await screeningService.ScreenAsync(
            new OutboundCallScreeningContext { Request = new DialRequest { To = "+14255551212" } },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task DialAsync_WhenAScreenerReturnsNoVerdict_DoesNotReachTheProvider()
    {
        // Arrange
        var provider = new RecordingCallControlProvider();
        var resolver = new Mock<ITelephonyProviderResolver>();
        resolver
            .Setup(r => r.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(provider);

        var screeningService = new DefaultOutboundCallScreeningService([new NullScreener()]);

        var service = new DefaultTelephonyService(
            resolver.Object,
            screeningService,
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var result = await service.DialAsync(new DialRequest { To = "+14255551212" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(provider.DialWasCalled);
    }

    private sealed class StubScreener : IOutboundCallScreener
    {
        private readonly OutboundCallScreeningResult _result;

        public StubScreener(OutboundCallScreeningResult result)
        {
            _result = result;
        }

        public Task<OutboundCallScreeningResult> ScreenAsync(OutboundCallScreeningContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class NullScreener : IOutboundCallScreener
    {
        public Task<OutboundCallScreeningResult> ScreenAsync(OutboundCallScreeningContext context, CancellationToken cancellationToken = default)
            => Task.FromResult<OutboundCallScreeningResult>(null);
    }

    private sealed class RecordingCallControlProvider : ITelephonyProvider, ITelephonyCallControlProvider
    {
        public bool DialWasCalled { get; private set; }

        public Microsoft.Extensions.Localization.LocalizedString Name => new("Recording", "Recording");

        public TelephonyCapabilities Capabilities => TelephonyCapabilities.Dial;

        public Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
        {
            DialWasCalled = true;

            return Task.FromResult(TelephonyResult.Success());
        }

        public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
            => Task.FromResult(TelephonyResult.Success());
    }
}
