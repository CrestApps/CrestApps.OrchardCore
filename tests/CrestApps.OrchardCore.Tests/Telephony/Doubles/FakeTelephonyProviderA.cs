using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Localization;
using CrestApps.Core.Telephony;
using CrestApps.Core.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A minimal telephony provider used to assert provider resolution by technical name.
/// </summary>
/// <remarks>
/// This double deliberately implements only <see cref="ITelephonyCallControlProvider"/> to prove that a
/// provider can be written against a single capability contract without supplying the operations it never
/// advertises.
/// </remarks>
internal sealed class FakeTelephonyProviderA : ITelephonyProvider, ITelephonyCallControlProvider
{
    public LocalizedString Name => new("A", "A");

    public TelephonyCapabilities Capabilities => TelephonyCapabilities.Dial;

    public Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());

    public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
        => Task.FromResult(TelephonyResult.Success());
}
