using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// A call is sent to voicemail once, however many requests ask for it.
/// </summary>
/// <remarks>
/// Sending a caller to voicemail answers their leg and plays the greeting. Two requests arrived for one click -- the
/// soft phone asked the telephony hub while the Contact Center's decline routed the same caller to voicemail -- and
/// both answered and greeted, so the caller heard "please leave your message" twice before the beep. Whatever sends
/// the second request, a call already being sent to voicemail is not answered and greeted again.
/// </remarks>
public sealed class SendToVoicemailOnceTests
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task TwoConcurrentRequests_SendTheCallerToVoicemailOnce()
    {
        // Arrange
        var provider = new GatedVoicemailProvider();
        var guard = CreateGuard();
        var first = CreateService(provider, guard);
        var second = CreateService(provider, guard);
        var call = new CallReference { CallId = "v3:caller" };

        // Act
        // Both requests start together. Whichever claims the call first is held inside the provider (answering and
        // greeting), and the other must return without reaching the provider at all.
        var requests = new[]
        {
            Task.Run(() => first.SendToVoicemailAsync(call, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken),
            Task.Run(() => second.SendToVoicemailAsync(call, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken),
        };

        await provider.Entered.Task.WaitAsync(_wait, TestContext.Current.CancellationToken);
        var refused = await Task.WhenAny(requests).WaitAsync(_wait, TestContext.Current.CancellationToken);
        var refusedResult = await refused;

        provider.Gate.SetResult();
        var results = await Task.WhenAll(requests).WaitAsync(_wait, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, provider.Calls);
        Assert.True(refusedResult.Succeeded);
        Assert.All(results, result => Assert.True(result.Succeeded));
    }

    [Fact]
    public async Task ARequestAfterTheCallWasSentToVoicemail_DoesNotGreetTheCallerAgain()
    {
        // Arrange
        var provider = new GatedVoicemailProvider();
        provider.Gate.SetResult();
        var guard = CreateGuard();
        var call = new CallReference { CallId = "v3:caller" };
        await CreateService(provider, guard).SendToVoicemailAsync(call, TestContext.Current.CancellationToken);

        // Act
        var result = await CreateService(provider, guard).SendToVoicemailAsync(call, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task AFailedAttempt_LeavesTheCallFreeToBeSentToVoicemailAgain()
    {
        // Arrange
        // Only a call that actually reached voicemail is held against a later request; a refused attempt must not
        // strand the caller with no way to leave a message.
        var provider = new GatedVoicemailProvider { Result = TelephonyResult.Failed("refused") };
        provider.Gate.SetResult();
        var guard = CreateGuard();
        var call = new CallReference { CallId = "v3:caller" };
        await CreateService(provider, guard).SendToVoicemailAsync(call, TestContext.Current.CancellationToken);
        provider.Result = TelephonyResult.Success();

        // Act
        var retry = await CreateService(provider, guard).SendToVoicemailAsync(call, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(retry.Succeeded);
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task DifferentCalls_AreEachSentToVoicemail()
    {
        // Arrange
        var provider = new GatedVoicemailProvider();
        provider.Gate.SetResult();
        var guard = CreateGuard();

        // Act
        await CreateService(provider, guard).SendToVoicemailAsync(new CallReference { CallId = "v3:first" }, TestContext.Current.CancellationToken);
        await CreateService(provider, guard).SendToVoicemailAsync(new CallReference { CallId = "v3:second" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, provider.Calls);
    }

    private static TelephonyVoicemailSendGuard CreateGuard()
        => new(
            new FakeDistributedLock(),
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            NullLogger<TelephonyVoicemailSendGuard>.Instance);

    private static DefaultTelephonyService CreateService(ITelephonyProvider provider, ITelephonyVoicemailSendGuard guard)
        => new(
            new StubTelephonyProviderResolver(provider),
            new DefaultOutboundCallScreeningService([]),
            new StubTelephonyExtensionResolver(),
            DialDestinationPolicyFactory.Create(),
            new PassThroughStringLocalizer<DefaultTelephonyService>(),
            guard,
            NullLogger<DefaultTelephonyService>.Instance);

    /// <summary>
    /// A voicemail provider that counts the calls it is asked to send to voicemail and holds each one until released.
    /// </summary>
    private sealed class GatedVoicemailProvider : ITelephonyProvider, ITelephonyVoicemailProvider
    {
        private int _calls;

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TelephonyResult Result { get; set; } = TelephonyResult.Success();

        public int Calls => Volatile.Read(ref _calls);

        public LocalizedString Name => new("Gated", "Gated");

        public TelephonyCapabilities Capabilities => TelephonyCapabilities.Voicemail;

        public async Task<TelephonyResult> SendToVoicemailAsync(CallReference call, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            Entered.TrySetResult();

            await Gate.Task.WaitAsync(cancellationToken);

            return Result;
        }
    }
}
