using System.Collections.Concurrent;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// An in-memory telephony provider used by the Playwright harness to exercise the soft phone widget
/// and the SignalR hub contract without a real provider.
/// </summary>
public sealed class InMemoryTelephonyProvider : ITelephonyProvider, ITelephonyAudioProvider, ITelephonyCallStateProvider, ITelephonyDirectoryProvider
{
    private readonly ConcurrentDictionary<string, TelephonyCall> _calls = new();
    private readonly ConcurrentDictionary<string, byte> _publishedCallIds = new();
    private TelephonyCall _latestCall;
    private int _counter;
    private int _dialRequestCount;
    private int _hangupRequestCount;
    private int _mergeRequestCount;
    private int _transferRequestCount;
    private int _dialDelayMilliseconds;
    private int _lookupRequestCount;
    private int _lookupDelayMilliseconds;

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
                TelephonyCapabilities.Voicemail |
                TelephonyCapabilities.Directory;
        }
    }

    public TelephonyAudioCapabilities AudioCapabilities => TelephonyAudioCapabilities.Browser;

    public TelephonyAudioMode ConfiguredAudioMode => TelephonyAudioMode.Browser;

    public string BrowserMediaAdapterName => "in-memory";

    public async Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _dialRequestCount);

        var delayMilliseconds = Volatile.Read(ref _dialDelayMilliseconds);

        if (delayMilliseconds > 0)
        {
            await Task.Delay(delayMilliseconds, cancellationToken);
        }

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
        _latestCall = call;

        return TelephonyResult.Success(call);
    }

    public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _hangupRequestCount);

        if (call?.CallId is not null)
        {
            _calls.TryRemove(call.CallId, out _);
            _publishedCallIds.TryRemove(call.CallId, out _);
        }

        _latestCall = new TelephonyCall
        {
            CallId = call?.CallId,
            State = CallState.Disconnected,
            ProviderName = Name.Name,
        };

        return Task.FromResult(TelephonyResult.Success(_latestCall));
    }

    public Task<TelephonyResult> HoldAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        return Update(call?.CallId, c =>
        {
            c.State = CallState.OnHold;
            c.IsOnHold = true;
        });
    }

    public Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        return Update(call?.CallId, c =>
        {
            c.State = CallState.Connected;
            c.IsOnHold = false;
        });
    }

    public Task<TelephonyResult> MuteAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        return Update(call?.CallId, c => c.IsMuted = true);
    }

    public Task<TelephonyResult> UnmuteAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        return Update(call?.CallId, c => c.IsMuted = false);
    }

    public Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _transferRequestCount);

        return Update(request?.CallId, c => c.State = CallState.Connected);
    }

    public Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _mergeRequestCount);

        var callIds = request?.GetCallIds() ?? [];

        foreach (var callId in callIds)
        {
            if (_calls.TryGetValue(callId, out var call))
            {
                call.State = CallState.Connected;
                call.IsOnHold = false;
                call.Metadata["isConference"] = true;
                call.Metadata["participantCount"] = callIds.Count;
            }
        }

        return Task.FromResult(callIds.Count >= 2
            ? TelephonyResult.Success(_calls[callIds[0]])
            : TelephonyResult.Failed("At least two calls are required."));
    }

    public Task<TelephonyResult> SendDigitsAsync(SendDigitsRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(TelephonyResult.Success());
    }

    public Task<TelephonyResult> AnswerAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        return Update(call?.CallId, c =>
        {
            c.State = CallState.Connected;
            c.Direction = CallDirection.Inbound;
        });
    }

    public Task<TelephonyResult> RejectAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        return HangupAsync(call, cancellationToken);
    }

    public Task<TelephonyResult> SendToVoicemailAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        return HangupAsync(call, cancellationToken);
    }

    public Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TelephonyClientCredentials
        {
            ProviderName = "InMemory",
            AudioCapabilities = AudioCapabilities,
            AudioMode = ConfiguredAudioMode,
            BrowserMediaAdapterName = BrowserMediaAdapterName,
        });
    }

    public Task<TelephonyDirectoryResult> GetDirectoryAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TelephonyDirectoryResult
        {
            Succeeded = true,
            Entries =
            [
                new()
                {
                    Id = "user-2001",
                    DisplayName = "Alex Agent",
                    Destination = "2001",
                    Extension = "2001",
                },
                new()
                {
                    Id = "user-2002",
                    DisplayName = "Sam Supervisor",
                    Destination = "2002",
                    Extension = "2002",
                },
            ],
        });
    }

    public async Task<TelephonyCallLookupResult> GetCallStateAsync(
        string callId,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _lookupRequestCount);

        if (string.IsNullOrEmpty(callId) ||
            !_publishedCallIds.ContainsKey(callId) ||
            !_calls.TryGetValue(callId, out var call))
        {
            return new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = false,
            };
        }

        var delayMilliseconds = Volatile.Read(ref _lookupDelayMilliseconds);

        if (delayMilliseconds > 0)
        {
            await Task.Delay(delayMilliseconds, cancellationToken);
        }

        return new TelephonyCallLookupResult
        {
            Succeeded = true,
            Found = true,
            Call = call,
        };
    }

    public TelephonyCall GetLatestCall()
    {
        return _latestCall;
    }

    public TelephonyCall PublishLatestCallState()
    {
        if (_latestCall?.CallId is not null)
        {
            _publishedCallIds[_latestCall.CallId] = 0;
        }

        return _latestCall;
    }

    public async Task<IReadOnlyList<TelephonyCall>> GetActiveCallsAsync()
    {
        var calls = new List<TelephonyCall>();

        foreach (var callId in _calls.Keys)
        {
            var lookup = await GetCallStateAsync(callId);

            if (lookup.Found && lookup.Call is not null)
            {
                calls.Add(lookup.Call);
            }
        }

        return calls
            .OrderByDescending(call => call.StartedUtc)
            .ToList();
    }

    public TelephonyCall DisconnectLatestCall()
    {
        var callId = _latestCall?.CallId;

        if (!string.IsNullOrEmpty(callId))
        {
            _calls.TryRemove(callId, out _);
        }

        _latestCall = new TelephonyCall
        {
            CallId = callId,
            From = _latestCall?.From,
            To = _latestCall?.To,
            Direction = _latestCall?.Direction ?? CallDirection.Outbound,
            State = CallState.Disconnected,
            ProviderName = Name.Name,
            StartedUtc = _latestCall?.StartedUtc,
        };
        if (callId is not null)
        {
            _publishedCallIds[callId] = 0;
        }

        return _latestCall;
    }

    public int GetDialRequestCount()
    {
        return Volatile.Read(ref _dialRequestCount);
    }

    public int GetHangupRequestCount()
    {
        return Volatile.Read(ref _hangupRequestCount);
    }

    public int GetMergeRequestCount()
    {
        return Volatile.Read(ref _mergeRequestCount);
    }

    public int GetTransferRequestCount()
    {
        return Volatile.Read(ref _transferRequestCount);
    }

    public void SetDialDelay(int milliseconds)
    {
        Volatile.Write(ref _dialDelayMilliseconds, Math.Max(0, milliseconds));
    }

    public int GetCallLookupRequestCount()
    {
        return Volatile.Read(ref _lookupRequestCount);
    }

    public void SetCallLookupDelay(int milliseconds)
    {
        Volatile.Write(ref _lookupDelayMilliseconds, Math.Max(0, milliseconds));
    }

    private Task<TelephonyResult> Update(string callId, Action<TelephonyCall> mutate)
    {
        if (callId is null || !_calls.TryGetValue(callId, out var call))
        {
            return Task.FromResult(TelephonyResult.Failed("Call not found."));
        }

        mutate(call);
        _latestCall = call;

        return Task.FromResult(TelephonyResult.Success(call));
    }
}
