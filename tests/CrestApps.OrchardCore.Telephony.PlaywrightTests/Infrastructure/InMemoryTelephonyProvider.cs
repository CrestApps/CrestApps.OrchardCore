using System.Collections.Concurrent;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// An in-memory telephony provider used by the Playwright harness to exercise the soft phone widget
/// and the SignalR hub contract without a real provider.
/// </summary>
public sealed class InMemoryTelephonyProvider : ITelephonyProvider
{
    private readonly ConcurrentDictionary<string, TelephonyCall> _calls = new();
    private int _counter;

    public LocalizedString Name => new("InMemory", "InMemory");

    public TelephonyCapabilities Capabilities
    {
        get
        {
            return TelephonyCapabilities.Dial |
                TelephonyCapabilities.Hangup |
                TelephonyCapabilities.Hold |
                TelephonyCapabilities.Resume |
                TelephonyCapabilities.Mute |
                TelephonyCapabilities.Transfer |
                TelephonyCapabilities.Merge |
                TelephonyCapabilities.SendDigits |
                TelephonyCapabilities.ReceiveCalls |
                TelephonyCapabilities.Voicemail;
        }
    }

    public Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
    {
        var call = new TelephonyCall
        {
            CallId = $"call-{Interlocked.Increment(ref _counter)}",
            To = request?.To,
            From = request?.From,
            State = CallState.Connected,
            Direction = CallDirection.Outbound,
            ProviderName = "InMemory",
            StartedUtc = DateTimeOffset.UtcNow,
        };

        _calls[call.CallId] = call;

        return Task.FromResult(TelephonyResult.Success(call));
    }

    public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        if (call?.CallId is not null)
        {
            _calls.TryRemove(call.CallId, out _);
        }

        return Task.FromResult(TelephonyResult.Success(new TelephonyCall { CallId = call?.CallId, State = CallState.Disconnected }));
    }

    public Task<TelephonyResult> HoldAsync(CallReference call, CancellationToken cancellationToken = default)
        => Update(call?.CallId, c => { c.State = CallState.OnHold; c.IsOnHold = true; });

    public Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default)
        => Update(call?.CallId, c => { c.State = CallState.Connected; c.IsOnHold = false; });

    public Task<TelephonyResult> MuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => Update(call?.CallId, c => c.IsMuted = true);

    public Task<TelephonyResult> UnmuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => Update(call?.CallId, c => c.IsMuted = false);

    public Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
        => Update(request?.CallId, c => c.State = CallState.Connected);

    public Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default)
        => Update(request?.PrimaryCallId, c => c.State = CallState.Connected);

    public Task<TelephonyResult> SendDigitsAsync(SendDigitsRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> AnswerAsync(CallReference call, CancellationToken cancellationToken = default)
        => Update(call?.CallId, c => { c.State = CallState.Connected; c.Direction = CallDirection.Inbound; });

    public Task<TelephonyResult> RejectAsync(CallReference call, CancellationToken cancellationToken = default)
        => HangupAsync(call, cancellationToken);

    public Task<TelephonyResult> SendToVoicemailAsync(CallReference call, CancellationToken cancellationToken = default)
        => HangupAsync(call, cancellationToken);

    public Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new TelephonyClientCredentials { ProviderName = "InMemory" });

    private Task<TelephonyResult> Update(string callId, Action<TelephonyCall> mutate)
    {
        if (callId is null || !_calls.TryGetValue(callId, out var call))
        {
            return Task.FromResult(TelephonyResult.Failed("Call not found."));
        }

        mutate(call);

        return Task.FromResult(TelephonyResult.Success(call));
    }
}
