namespace CrestApps.OrchardCore.Sms.Workspace.Models;

/// <summary>
/// The lifecycle state of an SMS broadcast (a one-composer, many-recipient send that fans out to individual
/// 1:1 threads — the compliant default, with no cross-recipient visibility).
/// </summary>
public enum SmsBroadcastStatus
{
    /// <summary>
    /// The broadcast is being composed and has not been queued.
    /// </summary>
    Draft,

    /// <summary>
    /// The broadcast has been queued and is awaiting the background fan-out.
    /// </summary>
    Queued,

    /// <summary>
    /// The background fan-out is in progress.
    /// </summary>
    Running,

    /// <summary>
    /// Every recipient has been processed.
    /// </summary>
    Completed,

    /// <summary>
    /// The broadcast failed before completing (for example, an unresolvable sending number).
    /// </summary>
    Failed,
}
