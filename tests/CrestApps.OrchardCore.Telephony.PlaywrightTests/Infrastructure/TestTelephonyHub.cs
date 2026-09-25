using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// A test SignalR hub that mirrors the production telephony hub contract (the same method names and
/// strongly-typed client) and routes requests to an <see cref="InMemoryTelephonyProvider"/>.
/// </summary>
public sealed class TestTelephonyHub : Hub<ITelephonyClient>
{
    private readonly InMemoryTelephonyProvider _provider;
    private readonly TestVoicemailInbox _voicemailInbox;
    private readonly BrowserCallLog _browserCalls;

    public TestTelephonyHub(InMemoryTelephonyProvider provider, TestVoicemailInbox voicemailInbox, BrowserCallLog browserCalls)
    {
        _provider = provider;
        _voicemailInbox = voicemailInbox;
        _browserCalls = browserCalls;
    }
    public Task RecordBrowserCall(string callId, string to, string from)
    {
        _browserCalls.Started(callId);

        return Task.CompletedTask;
    }

    public Task RecordBrowserCallEnded(string callId, bool connected)
    {
        _browserCalls.Ended(callId);

        return Task.CompletedTask;
    }

    public Task ReportBrowserCallsAlive(string[] callIds, string[] connectedCallIds)
    {
        _browserCalls.Alive(callIds);

        return Task.CompletedTask;
    }

    public Task ReportClientDiagnostic(string level, string code, string message, string context)
    {
        _browserCalls.Diagnostic(code, context);

        return Task.CompletedTask;
    }

    public Task<BrowserCallLogSnapshot> GetBrowserCallLog()
    {
        return Task.FromResult(_browserCalls.Snapshot());
    }

    public Task<TelephonyResult> Dial(DialRequest request)
    {
        return _provider.DialAsync(request);
    }

    public Task<TelephonyResult> Hangup(CallReference call)
    {
        return _provider.HangupAsync(call);
    }

    public Task<TelephonyResult> Hold(CallReference call)
    {
        return _provider.HoldAsync(call);
    }

    public Task<TelephonyResult> Resume(CallReference call)
    {
        return _provider.ResumeAsync(call);
    }

    public Task<TelephonyResult> Mute(CallReference call)
    {
        return _provider.MuteAsync(call);
    }

    public Task<TelephonyResult> Unmute(CallReference call)
    {
        return _provider.UnmuteAsync(call);
    }

    public Task<TelephonyResult> Transfer(TransferRequest request)
    {
        return _provider.TransferAsync(request);
    }

    public Task<TelephonyResult> Merge(MergeRequest request)
    {
        return _provider.MergeAsync(request);
    }

    public Task<TelephonyResult> SendDigits(SendDigitsRequest request)
    {
        return _provider.SendDigitsAsync(request);
    }

    public Task<TelephonyResult> Answer(CallReference call)
    {
        return _provider.AnswerAsync(call);
    }

    public Task<TelephonyResult> Reject(CallReference call)
    {
        return _provider.RejectAsync(call);
    }

    public async Task<TelephonyClientCredentials> GetCredentials()
    {
        var credentials = await _provider.GetClientCredentialsAsync();

        // A page opened with ?mediaAdapter= runs one of the phone's own media adapters (against a stand-in provider
        // SDK) instead of the in-memory one; the credentials have to name the adapter the page was configured with.
        var mediaAdapter = Context.GetHttpContext()?.Request.Query[SoftPhoneTestServer.MediaAdapterQueryKey].ToString();

        if (!string.IsNullOrEmpty(mediaAdapter))
        {
            credentials.BrowserMediaAdapterName = mediaAdapter;
        }

        return credentials;
    }

    public Task<TelephonyConnectionStatus> GetConnectionStatus()
    {
        return Task.FromResult(new TelephonyConnectionStatus
        {
            ProviderName = _provider.Name.Name,
            IsAvailable = true,
            RequiresAuthentication = false,
            IsConnected = true,
        });
    }

    public Task<int> GetCapabilities()
    {
        return Task.FromResult((int)_provider.Capabilities);
    }

    public Task<TelephonyDirectoryResult> GetDirectory()
    {
        return _provider.GetDirectoryAsync();
    }

    public Task<int> GetDialRequestCount()
    {
        return Task.FromResult(_provider.GetDialRequestCount());
    }

    public Task<int> GetHangupRequestCount()
    {
        return Task.FromResult(_provider.GetHangupRequestCount());
    }

    public Task<int> GetMergeRequestCount()
    {
        return Task.FromResult(_provider.GetMergeRequestCount());
    }

    public Task<int> GetTransferRequestCount()
    {
        return Task.FromResult(_provider.GetTransferRequestCount());
    }

    public Task<TransferRequest> GetLastTransfer()
    {
        return Task.FromResult(_provider.GetLastTransfer());
    }

    public Task SetDialDelay(int milliseconds)
    {
        _provider.SetDialDelay(milliseconds);

        return Task.CompletedTask;
    }

    public Task<int> GetCallLookupRequestCount()
    {
        return Task.FromResult(_provider.GetCallLookupRequestCount());
    }

    public Task SetCallLookupDelay(int milliseconds)
    {
        _provider.SetCallLookupDelay(milliseconds);

        return Task.CompletedTask;
    }

    public Task<TelephonyCallLookupResult> GetActiveCall()
    {
        var call = _provider.GetLatestCall();

        if (call is null)
        {
            return Task.FromResult(new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = false,
            });
        }

        return _provider.GetCallStateAsync(call.CallId);
    }

    public async Task<TelephonyCallListLookupResult> GetActiveCalls()
    {
        return new TelephonyCallListLookupResult
        {
            Succeeded = true,
            Calls = await _provider.GetActiveCallsAsync(),
        };
    }

    public async Task<bool> PublishLatestCallState()
    {
        var call = _provider.PublishLatestCallState();

        if (call is null)
        {
            return false;
        }

        await Clients.Caller.CallStateChanged(call);

        return true;
    }

    public async Task DisconnectLatestCall()
    {
        var call = _provider.DisconnectLatestCall();
        await Clients.Caller.CallStateChanged(call);
    }

    public Task PublishCallState(TelephonyCall call)
    {
        return Clients.Caller.CallStateChanged(call);
    }

    public Task<IEnumerable<TelephonyInteraction>> GetInteractions(int count)
    {
        var interactions = new List<TelephonyInteraction>
        {
            new()
            {
                InteractionId = "int-out-1",
                CallId = "call-out-1",
                ProviderName = _provider.Name.Name,
                To = "+15551234567",
                Direction = CallDirection.Outbound,
                Outcome = CallOutcome.Completed,
                StartedUtc = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                InteractionId = "int-in-1",
                CallId = "call-in-1",
                ProviderName = _provider.Name.Name,
                From = "+15559876543",
                Direction = CallDirection.Inbound,
                Outcome = CallOutcome.Missed,
                StartedUtc = new DateTime(2024, 1, 1, 9, 30, 0, DateTimeKind.Utc),
            },
        };

        interactions.AddRange(_voicemailInbox.List());

        return Task.FromResult<IEnumerable<TelephonyInteraction>>(interactions.Take(count));
    }
}
