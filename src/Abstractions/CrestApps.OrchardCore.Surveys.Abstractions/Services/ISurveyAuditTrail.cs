namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Records a Surveys event in the audit trail when the Audit Trail feature is on; does nothing otherwise. Services that
/// change surveys (the publisher, the access page, grading) call it for acts no observer hears.
/// </summary>
public interface ISurveyAuditTrail
{
    /// <summary>
    /// Records an event.
    /// </summary>
    /// <param name="eventName">The event name, from <see cref="SurveyAuditEvents"/>.</param>
    /// <param name="entry">What happened.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task RecordAsync(string eventName, SurveyAuditEntry entry, CancellationToken cancellationToken = default);
}
