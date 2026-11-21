using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Surveys.AuditTrail;

/// <summary>
/// Records Surveys events in the audit trail.
/// </summary>
internal sealed class AuditTrailSurveyAuditTrail : ISurveyAuditTrail
{
    private readonly IAuditTrailManager _auditTrailManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditTrailSurveyAuditTrail"/> class.
    /// </summary>
    /// <param name="auditTrailManager">The audit trail manager.</param>
    public AuditTrailSurveyAuditTrail(IAuditTrailManager auditTrailManager)
    {
        _auditTrailManager = auditTrailManager;
    }

    /// <inheritdoc />
    public Task RecordAsync(string eventName, SurveyAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        ArgumentNullException.ThrowIfNull(entry);

        var data = new SurveyAuditData
        {
            SurveyId = entry.SurveyId,
            SurveyName = entry.SurveyName,
            ResponseId = entry.ResponseId,
            ResponseNumber = entry.ResponseNumber,
            VersionNumber = entry.VersionNumber,
            From = entry.From,
            To = entry.To,
            Reason = entry.Reason,
            Detail = entry.Detail,
        };

        var correlationId = entry.ResponseId ?? entry.SurveyId ?? eventName;

        return _auditTrailManager.RecordEventAsync(new AuditTrailContext<SurveyAuditData>(eventName, SurveyAuditEvents.Category, correlationId, entry.ActorId, entry.ActorId, data));
    }
}
