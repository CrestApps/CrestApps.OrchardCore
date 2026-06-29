using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A second minimal telephony provider used to assert provider resolution by technical name.
/// </summary>
internal sealed class FakeTelephonyProviderB : ITelephonyProvider
{
    public LocalizedString Name => new("B", "B");

    public TelephonyCapabilities Capabilities => TelephonyCapabilities.Dial;

    public Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> HoldAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> MuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> UnmuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> SendDigitsAsync(SendDigitsRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> AnswerAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> RejectAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> SendToVoicemailAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new TelephonyClientCredentials { ProviderName = "B" });
}
