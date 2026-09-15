using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A telephony provider double that implements internal extension calling, recording the last request it
/// received so tests can assert the service resolved and dispatched correctly.
/// </summary>
public sealed class ExtensionDialRecordingProvider : ITelephonyProvider, ITelephonyExtensionDialProvider
{
    public LocalizedString Name => new("Test", "Test");

    public TelephonyCapabilities Capabilities { get; set; }
        = TelephonyCapabilities.ExtensionDial | TelephonyCapabilities.ExtensionConference;

    public ExtensionDialRequest LastDialRequest { get; private set; }

    public ExtensionConferenceRequest LastConferenceRequest { get; private set; }

    public Task<TelephonyResult> DialExtensionAsync(ExtensionDialRequest request, CancellationToken cancellationToken = default)
    {
        LastDialRequest = request;

        return Task.FromResult(TelephonyResult.Success(new TelephonyCall
        {
            CallId = "ext-call-1",
            To = request.TargetDisplayName,
            State = CallState.Connecting,
        }));
    }

    public Task<TelephonyResult> AddExtensionToConferenceAsync(ExtensionConferenceRequest request, CancellationToken cancellationToken = default)
    {
        LastConferenceRequest = request;

        return Task.FromResult(TelephonyResult.Success(new TelephonyCall
        {
            CallId = request.ActiveCall?.CallId,
            State = CallState.Connected,
        }));
    }
}
