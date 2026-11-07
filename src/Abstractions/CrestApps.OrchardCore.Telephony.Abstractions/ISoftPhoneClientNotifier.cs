using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Pushes server-initiated events to every soft phone a user has open on this tenant.
/// </summary>
/// <remarks>
/// The Telephony feature implements it over its SignalR hub, so a module that drives calls, such as Contact Center,
/// reaches the soft phones without using the hub itself. Each call goes to the user's tenant-scoped group, so every
/// browser tab, extension and device the user is signed in on hears it.
/// </remarks>
public interface ISoftPhoneClientNotifier
{
    /// <summary>
    /// Tells the user's soft phones that the state of one of their calls has changed.
    /// </summary>
    /// <param name="userId">The user whose soft phones are told.</param>
    /// <param name="call">The call whose state changed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CallStateChangedAsync(string userId, TelephonyCall call);

    /// <summary>
    /// Tells the user's soft phones that an incoming call they were ringing for has been answered, here or on another
    /// of their soft phones, so the others stop ringing.
    /// </summary>
    /// <param name="userId">The user whose soft phones are told.</param>
    /// <param name="notification">The call and offer that were answered.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task IncomingCallAnsweredAsync(string userId, IncomingCallAnsweredNotification notification);

    /// <summary>
    /// Asks the user's soft phones to place an outbound call, the same request the call button beside a phone number
    /// sends.
    /// </summary>
    /// <param name="userId">The user whose soft phones are asked.</param>
    /// <param name="request">The number to call.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DialRequestedAsync(string userId, TelephonyDialRequest request);
}
