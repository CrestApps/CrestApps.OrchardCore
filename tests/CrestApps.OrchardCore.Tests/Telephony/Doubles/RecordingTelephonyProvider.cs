using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A telephony provider that records the last invoked operation and returns a configurable result.
/// </summary>
internal sealed class RecordingTelephonyProvider : ITelephonyProvider
{
    public string LastOperation { get; private set; }

    public object LastPayload { get; private set; }

    public TelephonyResult ResultToReturn { get; set; } = TelephonyResult.Success();

    public TelephonyCapabilities Capabilities { get; set; } = TelephonyCapabilities.Dial | TelephonyCapabilities.Hangup;

    public LocalizedString Name => new("Recording", "Recording");

    public Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
        => Record("Dial", request);

    public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
        => Record("Hangup", call);

    public Task<TelephonyResult> HoldAsync(CallReference call, CancellationToken cancellationToken = default)
        => Record("Hold", call);

    public Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default)
        => Record("Resume", call);

    public Task<TelephonyResult> MuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => Record("Mute", call);

    public Task<TelephonyResult> UnmuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => Record("Unmute", call);

    public Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
        => Record("Transfer", request);

    public Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default)
        => Record("Merge", request);

    public Task<TelephonyResult> SendDigitsAsync(SendDigitsRequest request, CancellationToken cancellationToken = default)
        => Record("SendDigits", request);

    public Task<TelephonyResult> AnswerAsync(CallReference call, CancellationToken cancellationToken = default)
        => Record("Answer", call);

    public Task<TelephonyResult> RejectAsync(CallReference call, CancellationToken cancellationToken = default)
        => Record("Reject", call);

    public Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default)
    {
        LastOperation = "GetClientCredentials";

        return Task.FromResult(new TelephonyClientCredentials { ProviderName = "Recording" });
    }

    private Task<TelephonyResult> Record(string operation, object payload)
    {
        LastOperation = operation;
        LastPayload = payload;

        return Task.FromResult(ResultToReturn);
    }
}
