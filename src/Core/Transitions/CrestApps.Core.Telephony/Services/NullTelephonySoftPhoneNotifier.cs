using CrestApps.Core.Telephony.Models;

namespace CrestApps.Core.Telephony.Services;

/// <summary>
/// The default <see cref="ITelephonySoftPhoneNotifier"/>: it pushes nowhere.
/// </summary>
/// <remarks>
/// A soft phone is one way to answer a call and not the only one, but the services that record what a call
/// did - the incoming-call dispatcher, the call-history projection, the reconciliation sweep - all announce
/// what they saw whether or not anybody is listening. Without a default they would fail to resolve, so a host
/// that wanted call history and no soft phone could not have it. Registering this one with <c>TryAdd</c> means
/// a host that does want soft phones replaces it by calling
/// <c>AddCoreTelephonySoftPhoneNotifier&lt;THub&gt;</c>, and a host that does not gets silence rather than a
/// missing service.
/// </remarks>
public sealed class NullTelephonySoftPhoneNotifier : ITelephonySoftPhoneNotifier
{
    /// <inheritdoc />
    public Task NotifyIncomingCallAsync(string userId, TelephonyCall call, IncomingCallContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task NotifyCallStateChangedAsync(string userId, TelephonyCall call, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task RequestDialAsync(string userId, TelephonyDialRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
