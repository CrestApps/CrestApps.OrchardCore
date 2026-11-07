namespace CrestApps.OrchardCore.Outreach.Core.Services;

/// <summary>
/// What became of an outreach batch that was processed.
/// </summary>
public enum OutreachBatchOutcome
{
    /// <summary>
    /// It was not due, or another worker owns it.
    /// </summary>
    NotDue,

    /// <summary>
    /// It was sent, or its run had ended.
    /// </summary>
    Completed,

    /// <summary>
    /// It waits, without using up an attempt: its run is in quiet hours, or something it needs is not enabled.
    /// </summary>
    Deferred,

    /// <summary>
    /// It failed and will be retried after its back-off.
    /// </summary>
    Retrying,

    /// <summary>
    /// It was given up on.
    /// </summary>
    Abandoned,
}
