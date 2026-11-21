using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Records nothing; used while the Audit Trail feature is off.
/// </summary>
internal sealed class NullSurveyAuditTrail : ISurveyAuditTrail
{
    /// <inheritdoc />
    public Task RecordAsync(string eventName, SurveyAuditEntry entry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
