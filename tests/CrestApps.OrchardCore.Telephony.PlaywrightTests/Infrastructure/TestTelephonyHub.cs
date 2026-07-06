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

    public TestTelephonyHub(InMemoryTelephonyProvider provider)
    {
        _provider = provider;
    }

    public Task<TelephonyResult> Dial(DialRequest request) => _provider.DialAsync(request);

    public Task<TelephonyResult> Hangup(CallReference call) => _provider.HangupAsync(call);

    public Task<TelephonyResult> Hold(CallReference call) => _provider.HoldAsync(call);

    public Task<TelephonyResult> Resume(CallReference call) => _provider.ResumeAsync(call);

    public Task<TelephonyResult> Mute(CallReference call) => _provider.MuteAsync(call);

    public Task<TelephonyResult> Unmute(CallReference call) => _provider.UnmuteAsync(call);

    public Task<TelephonyResult> Transfer(TransferRequest request) => _provider.TransferAsync(request);

    public Task<TelephonyResult> Merge(MergeRequest request) => _provider.MergeAsync(request);

    public Task<TelephonyResult> SendDigits(SendDigitsRequest request) => _provider.SendDigitsAsync(request);

    public Task<TelephonyResult> Answer(CallReference call) => _provider.AnswerAsync(call);

    public Task<TelephonyResult> Reject(CallReference call) => _provider.RejectAsync(call);

    public Task<TelephonyClientCredentials> GetCredentials() => _provider.GetClientCredentialsAsync();

    public Task<TelephonyConnectionStatus> GetConnectionStatus()
        => Task.FromResult(new TelephonyConnectionStatus { ProviderName = _provider.Name.Name, IsAvailable = true, RequiresAuthentication = false, IsConnected = true });

    public Task<int> GetCapabilities() => Task.FromResult((int)_provider.Capabilities);

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

        return Task.FromResult<IEnumerable<TelephonyInteraction>>(interactions.Take(count));
    }
}
