using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Defines the strongly-typed callbacks the telephony hub uses to push server-initiated events to a
/// connected soft phone client.
/// </summary>
public interface ITelephonyClient
{
    /// <summary>
    /// Notifies the client that the state of a call has changed.
    /// </summary>
    /// <param name="call">The call whose state changed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CallStateChanged(TelephonyCall call);

    /// <summary>
    /// Notifies the client that an inbound call is ringing.
    /// </summary>
    /// <param name="call">The inbound call.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task IncomingCall(TelephonyCall call);

    /// <summary>
    /// Notifies the client that the provider issued new connection credentials.
    /// </summary>
    /// <param name="credentials">The credentials the client uses to connect to the provider.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CredentialsIssued(TelephonyClientCredentials credentials);

    /// <summary>
    /// Notifies the client that an error occurred while processing a request.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveError(string message);
}
