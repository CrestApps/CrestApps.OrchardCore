using System.Collections.Concurrent;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// An in-memory telephony provider used by the Playwright harness to exercise the soft phone widget
/// and the SignalR hub contract without a real provider.
/// </summary>
public sealed class InMemoryTelephonyProvider :
    ITelephonyProvider,
    ITelephonyCallControlProvider,
    ITelephonyInboundCallProvider,
    ITelephonyHoldProvider,
    ITelephonyMuteProvider,
    ITelephonyTransferProvider,
    ITelephonyConsultTransferProvider,
    ITelephonyConferenceProvider,
    ITelephonyDtmfProvider,
    ITelephonyVoicemailProvider,
    ITelephonySoftPhoneCredentialsProvider,
    ITelephonyAudioProvider,
    ITelephonyCallStateProvider,
    ITelephonyDirectoryProvider
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
    private TransferRequest _lastTransfer;
    private MergeRequest _lastMerge;
    private volatile bool _attendedTransfer;
    private volatile bool _bridgedDial;
    private volatile bool _bridgeUnavailable;
    private volatile bool _muteIsLocal;
    private volatile bool _noDirectory;
    private int _extensionDirectoryRequestCount;
    private int _extensionDialCount;
    private DialRequest _lastDial;
    private SendDigitsRequest _lastDigits;
    private volatile bool _consultTransfer;
    private readonly ConcurrentDictionary<string, (string Status, bool CallEnded)> _consultStatuses = new();

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
                (_noDirectory ? TelephonyCapabilities.None : TelephonyCapabilities.Directory) |
                (_attendedTransfer ? TelephonyCapabilities.AttendedTransfer : TelephonyCapabilities.None) |
                (_bridgedDial ? TelephonyCapabilities.BridgedDial : TelephonyCapabilities.None);
        }
    }

    /// <summary>
    /// Has the provider connect keypad dials itself, as Telnyx does: the phone asks it to, naming its credential, and
    /// answers the leg the provider rings back to it. With <paramref name="unavailable"/> the provider says it cannot,
    /// and the phone dials from the browser.
    /// </summary>
    public void EnableBridgedDial(bool unavailable = false)
    {
        _bridgedDial = true;
        _bridgeUnavailable = unavailable;
    }

    /// <summary>
    /// Has the provider mute the way Telnyx does for a call whose audio is in the browser: the Mute and Unmute commands
    /// answer with the call muted or not, but the provider keeps nothing, so every later report of the call -- a state
    /// push, the phone's periodic refresh -- says it is unmuted.
    /// </summary>
    public void KeepNoMuteState()
    {
        _muteIsLocal = true;
    }

    /// <summary>
    /// Has the provider keep no directory of its own, as Telnyx keeps none: the phone offers the phone system's
    /// extensions instead.
    /// </summary>
    public void RemoveDirectory()
    {
        _noDirectory = true;
    }

    /// <summary>
    /// Gets the phone system's extensions and who each rings, as the server names them.
    /// </summary>
    public IReadOnlyList<TelephonyExtensionDirectoryEntry> Extensions { get; private set; } =
    [
        new() { Extension = "2", DisplayName = "Jane Doe", UserName = "jdoe" },
        new() { Extension = "3", DisplayName = "Sam Lee", UserName = "slee" },
    ];

    /// <summary>
    /// Gets the extensions of the signed-in user, as the server names them to the phone.
    /// </summary>
    public IReadOnlyList<string> OwnExtensions { get; private set; } = [];

    /// <summary>
    /// Replaces the phone system's extensions and names the signed-in user's own. The own extensions are listed too, as
    /// a server that did not leave them out would list them, so the phone is seen to leave them out itself.
    /// </summary>
    public void UseExtensions(IReadOnlyList<string> ownExtensions, params TelephonyExtensionDirectoryEntry[] extensions)
    {
        Extensions = extensions;
        OwnExtensions = ownExtensions;
    }

    /// <summary>
    /// Records a read of the extensions and returns them.
    /// </summary>
    public TelephonyExtensionDirectoryResult GetExtensionDirectory()
    {
        Interlocked.Increment(ref _extensionDirectoryRequestCount);

        return new TelephonyExtensionDirectoryResult { Succeeded = true, Entries = Extensions, OwnExtensions = OwnExtensions };
    }

    /// <summary>
    /// Records an extension call the phone asked for; the harness places none.
    /// </summary>
    public TelephonyResult DialExtension(ExtensionDialRequest request)
    {
        Interlocked.Increment(ref _extensionDialCount);

        return TelephonyResult.Failed("The test harness places no extension calls.");
    }

    /// <summary>
    /// Gets how many extension calls the phone asked for.
    /// </summary>
    public int GetExtensionDialCount()
    {
        return Volatile.Read(ref _extensionDialCount);
    }

    /// <summary>
    /// Gets how many times the phone read the extensions.
    /// </summary>
    public int GetExtensionDirectoryRequestCount()
    {
        return Volatile.Read(ref _extensionDirectoryRequestCount);
    }

    /// <summary>
    /// Gets the last dial the phone asked for.
    /// </summary>
    public DialRequest GetLastDial()
    {
        return Volatile.Read(ref _lastDial);
    }

    /// <summary>
    /// Gets the last digits the phone sent.
    /// </summary>
    public SendDigitsRequest GetLastDigits()
    {
        return Volatile.Read(ref _lastDigits);
    }

    /// <summary>
    /// Has the provider also advertise attended (warm) transfer, so the phone offers a choice of transfer mode.
    /// </summary>
    public void EnableAttendedTransfer()
    {
        _attendedTransfer = true;
    }

    public TelephonyAudioCapabilities AudioCapabilities => TelephonyAudioCapabilities.Browser;

    public TelephonyAudioMode ConfiguredAudioMode => TelephonyAudioMode.Browser;

    /// <summary>
    /// Gets or sets the browser media adapter the phone registers with: the stand-in a test registers itself
    /// (<c>in-memory</c>), or the soft phone's own Telnyx adapter (<c>telnyx-webrtc</c>) over <see cref="FakeTelnyxSdk"/>.
    /// </summary>
    public string BrowserMediaAdapterName { get; set; } = "in-memory";

    /// <summary>
    /// Gets the diagnostic codes the phone reported, in order.
    /// </summary>
    public ConcurrentQueue<string> ClientDiagnosticCodes { get; } = new();

    /// <summary>
    /// Gets the call quality reports the phone sent, in order.
    /// </summary>
    public ConcurrentQueue<CallQualityReport> CallQualityReports { get; } = new();

    public async Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _dialRequestCount);
        Volatile.Write(ref _lastDial, request);

        if (_bridgeUnavailable &&
            request?.Metadata?.ContainsKey(TelephonyConstants.RequestMetadata.SoftPhoneCredentialId) == true)
        {
            return TelephonyResult.Failed("This call cannot be connected through the soft phone right now.", TelephonyConstants.ErrorCodes.BridgeUnavailable);
        }

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

            // Like every real provider, a dial is acknowledged as connecting; the call is connected by the provider's
            // own report (PublishLatestCallState), not by the command's answer.
            State = CallState.Connecting,
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
        HoldCommands.Enqueue($"Hold:{call?.CallId}");

        return Update(call?.CallId, c =>
        {
            c.State = CallState.OnHold;
            c.IsOnHold = true;
        });
    }

    public Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        HoldCommands.Enqueue($"Resume:{call?.CallId}");

        return Update(call?.CallId, c =>
        {
            c.State = CallState.Connected;
            c.IsOnHold = false;
        });
    }

    public Task<TelephonyResult> MuteAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        return _muteIsLocal ? Echo(call?.CallId, isMuted: true) : Update(call?.CallId, c => c.IsMuted = true);
    }

    public Task<TelephonyResult> UnmuteAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        return _muteIsLocal ? Echo(call?.CallId, isMuted: false) : Update(call?.CallId, c => c.IsMuted = false);
    }

    // Answers a mute command with the call as the command leaves it, keeping nothing (see KeepNoMuteState).
    private Task<TelephonyResult> Echo(string callId, bool isMuted)
    {
        if (callId is null || !_calls.TryGetValue(callId, out var call))
        {
            return Task.FromResult(TelephonyResult.Failed("Call not found."));
        }

        _latestCall = call;

        return Task.FromResult(TelephonyResult.Success(new TelephonyCall
        {
            CallId = call.CallId,
            From = call.From,
            To = call.To,
            State = call.State,
            Direction = call.Direction,
            IsMuted = isMuted,
            IsOnHold = call.IsOnHold,
            ProviderName = call.ProviderName,
            StartedUtc = call.StartedUtc,
            Metadata = new Dictionary<string, object>(call.Metadata ?? new Dictionary<string, object>()),
        }));
    }

    public Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _transferRequestCount);
        Volatile.Write(ref _lastTransfer, request);

        if (_consultTransfer && request?.CallId is not null && _calls.ContainsKey(request.CallId))
        {
            return Task.FromResult(StartConsultTransfer(request));
        }

        return Update(request?.CallId, c => c.State = CallState.Connected);
    }

    /// <summary>
    /// Has the provider carry transfers out the way Telnyx does for a call it connected: a blind transfer rings its
    /// destination and leaves the call held here until it answers; a warm one places a consult call back to the phone.
    /// Either is followed through GetConsult, and finished through CompleteConsult or CancelConsult.
    /// </summary>
    public void EnableConsultTransfer()
    {
        _consultTransfer = true;
    }

    /// <summary>
    /// Sets where a transfer stands, as the provider would report it once its destination answers or leaves.
    /// </summary>
    public void SetConsultStatus(string consultId, string status, bool callEnded = false)
    {
        _consultStatuses[consultId] = (status, callEnded);
    }

    /// <summary>
    /// Gets the consult commands the phone sent, in order, as "Method:callId:consultCallId".
    /// </summary>
    public ConcurrentQueue<string> ConsultCommands { get; } = new();

    /// <summary>
    /// Gets the hold and resume commands the phone sent, in order, as "Hold:callId" or "Resume:callId".
    /// </summary>
    public ConcurrentQueue<string> HoldCommands { get; } = new();

    /// <summary>
    /// Gets the leg the last transfer rang: a blind transfer's leg, or a warm transfer's consult call.
    /// </summary>
    public string LastConsultId { get; private set; }

    public Task<TelephonyResult> GetConsultAsync(ConsultTransferRequest request, CancellationToken cancellationToken = default)
    {
        ConsultCommands.Enqueue($"GetConsult:{request?.CallId}:{request?.ConsultCallId}");

        return Task.FromResult(ConsultResult(request, null));
    }

    public Task<TelephonyResult> CompleteConsultAsync(ConsultTransferRequest request, CancellationToken cancellationToken = default)
    {
        ConsultCommands.Enqueue($"CompleteConsult:{request?.CallId}:{request?.ConsultCallId}");

        return Task.FromResult(ConsultResult(request, TelephonyConstants.ConsultStatuses.Completed));
    }

    public Task<TelephonyResult> CancelConsultAsync(ConsultTransferRequest request, CancellationToken cancellationToken = default)
    {
        ConsultCommands.Enqueue($"CancelConsult:{request?.CallId}:{request?.ConsultCallId}");

        if (request?.ConsultCallId is not null)
        {
            _calls.TryRemove(request.ConsultCallId, out _);
        }

        return Task.FromResult(ConsultResult(request, TelephonyConstants.ConsultStatuses.Cancelled));
    }

    private TelephonyResult StartConsultTransfer(TransferRequest request)
    {
        var call = _calls[request.CallId];

        if (request.Mode != TransferMode.Warm)
        {
            var legId = $"xfer-{Interlocked.Increment(ref _counter)}";

            _consultStatuses[legId] = (TelephonyConstants.ConsultStatuses.Ringing, false);
            LastConsultId = legId;
            call.State = CallState.OnHold;
            call.IsOnHold = true;

            return TelephonyResult.Success(ConsultCall(call.CallId, legId, CallState.OnHold, TelephonyConstants.ConsultStatuses.Ringing, false));
        }

        var consult = ConsultCall($"consult-{Interlocked.Increment(ref _counter)}", null, CallState.Connecting, TelephonyConstants.ConsultStatuses.Ringing, false);

        consult.Metadata[TelephonyConstants.CallMetadata.ConsultId] = consult.CallId;
        consult.Metadata[TelephonyConstants.CallMetadata.ConsultOf] = call.CallId;
        consult.Direction = CallDirection.Outbound;
        consult.To = request.To;
        consult.StartedUtc = DateTimeOffset.UtcNow;
        _consultStatuses[consult.CallId] = (TelephonyConstants.ConsultStatuses.Ringing, false);
        LastConsultId = consult.CallId;
        _calls[consult.CallId] = consult;
        _latestCall = consult;

        return TelephonyResult.Success(consult);
    }

    private TelephonyResult ConsultResult(ConsultTransferRequest request, string settled)
    {
        if (request?.ConsultCallId is null || !_consultStatuses.TryGetValue(request.ConsultCallId, out var current))
        {
            return TelephonyResult.Failed("The transfer could not be found.");
        }

        if (settled is not null)
        {
            current = (settled, false);
            _consultStatuses[request.ConsultCallId] = current;
        }

        return TelephonyResult.Success(ConsultCall(request.CallId, request.ConsultCallId, CallState.Connected, current.Status, current.CallEnded));
    }

    private static TelephonyCall ConsultCall(string callId, string consultId, CallState state, string status, bool callEnded)
        => new()
        {
            CallId = callId,
            State = state,
            IsOnHold = state == CallState.OnHold,
            ProviderName = "InMemory",
            Metadata = new Dictionary<string, object>
            {
                [TelephonyConstants.CallMetadata.ConsultId] = consultId,
                [TelephonyConstants.CallMetadata.ConsultStatus] = status,
                [TelephonyConstants.CallMetadata.ConsultLive] = status is TelephonyConstants.ConsultStatuses.Ringing or TelephonyConstants.ConsultStatuses.Connected,
                [TelephonyConstants.CallMetadata.ConsultCallEnded] = callEnded,
            },
        };

    public Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _mergeRequestCount);
        Volatile.Write(ref _lastMerge, request);

        var callIds = request?.GetCallIds() ?? [];

        foreach (var callId in callIds)
        {
            if (_calls.TryGetValue(callId, out var call))
            {
                call.State = CallState.Connected;
                call.IsOnHold = false;
                call.Metadata["isConference"] = true;
                call.Metadata["participantCount"] = callIds.Count;

                // Named like a real provider's conference, so adding a call to it later can name it back.
                call.Metadata["conferenceName"] = string.IsNullOrWhiteSpace(request.ConferenceName)
                    ? $"conf-{callIds[0]}"
                    : request.ConferenceName;
            }
        }

        return Task.FromResult(callIds.Count >= 2
            ? TelephonyResult.Success(_calls[callIds[0]])
            : TelephonyResult.Failed("At least two calls are required."));
    }

    public Task<TelephonyResult> SendDigitsAsync(SendDigitsRequest request, CancellationToken cancellationToken = default)
    {
        Volatile.Write(ref _lastDigits, request);

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

        // The far end answers the call that was dialed.
        if (_latestCall?.State == CallState.Connecting)
        {
            _latestCall.State = CallState.Connected;
        }

        return _latestCall;
    }

    /// <summary>
    /// Records a call a test reports, the way a real provider knows the calls it carries, so the phone's periodic
    /// refresh of its active calls keeps it instead of dropping it. An ended call is forgotten.
    /// </summary>
    public void TrackCall(TelephonyCall call)
    {
        if (string.IsNullOrEmpty(call?.CallId))
        {
            return;
        }

        if (call.State is CallState.Disconnected or CallState.Failed)
        {
            _calls.TryRemove(call.CallId, out _);
            _publishedCallIds.TryRemove(call.CallId, out _);

            return;
        }

        _calls[call.CallId] = call;
        _publishedCallIds[call.CallId] = 0;
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

    public TransferRequest GetLastTransfer()
    {
        return Volatile.Read(ref _lastTransfer);
    }

    public MergeRequest GetLastMerge()
    {
        return Volatile.Read(ref _lastMerge);
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
