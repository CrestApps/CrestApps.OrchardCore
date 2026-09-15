using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A telephony provider that implements nothing beyond call control, used to prove both that a narrow
/// provider is expressible and that advertising a capability it cannot execute does not make it executable.
/// </summary>
internal sealed class CallControlOnlyTelephonyProvider : ITelephonyProvider, ITelephonyCallControlProvider
{
    /// <summary>
    /// Gets the name of the last operation the provider actually executed, or <see langword="null"/> when
    /// no operation reached it.
    /// </summary>
    public string LastOperation { get; private set; }

    /// <summary>
    /// Gets or sets the capabilities the provider advertises, independently of what it implements.
    /// </summary>
    public TelephonyCapabilities Capabilities { get; set; } = TelephonyCapabilities.Dial | TelephonyCapabilities.Hangup;

    /// <inheritdoc/>
    public LocalizedString Name => new("CallControlOnly", "CallControlOnly");

    /// <inheritdoc/>
    public Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
    {
        LastOperation = "Dial";

        return Task.FromResult(TelephonyResult.Success(new TelephonyCall
        {
            CallId = "call-1",
            State = CallState.Connecting,
        }));
    }

    /// <inheritdoc/>
    public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        LastOperation = "Hangup";

        return Task.FromResult(TelephonyResult.Success());
    }
}
