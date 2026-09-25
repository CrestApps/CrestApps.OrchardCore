namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// What the soft phone has said about a call it placed itself, kept on the call's <see cref="TelephonyInteraction"/>.
/// </summary>
/// <remarks>
/// A browser-originated call runs entirely in the provider SDK; the platform never sees it, so the phone's own reports
/// are the only evidence it has. The phone reports each call it still has up every so often, and a call it stops
/// reporting -- a page that crashed, an app closed mid-call, a browser that went away -- is settled by the
/// reconciliation sweep instead of staying "in progress" for good.
/// </remarks>
public sealed class ClientRecordedCallActivity
{
    /// <summary>
    /// Gets or sets when the soft phone last reported the call still up, in UTC.
    /// </summary>
    public DateTime? LastReportedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the soft phone first reported the call connected, in UTC, or <see langword="null"/> while it
    /// has never said so.
    /// </summary>
    public DateTime? ConnectedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the call was settled by the platform because the soft phone stopped
    /// reporting it, rather than by the soft phone reporting its end. Its end time is then the last moment the phone
    /// was heard from.
    /// </summary>
    public bool EndedUnreported { get; set; }
}
