namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Tells a user's soft phones that an incoming call that rang on all of them has been answered on one of them. It
/// is pushed through <see cref="ITelephonyClient.IncomingCallAnswered"/> so the others stop ringing at once.
/// </summary>
public sealed class IncomingCallAnsweredNotification
{
    /// <summary>
    /// Gets or sets the identifier of the call that was answered, as the incoming-call offer carried it.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the offer that was answered, when the call was offered through one (the
    /// <c>reservationId</c> property of the incoming-call context).
    /// </summary>
    public string OfferId { get; set; }
}
