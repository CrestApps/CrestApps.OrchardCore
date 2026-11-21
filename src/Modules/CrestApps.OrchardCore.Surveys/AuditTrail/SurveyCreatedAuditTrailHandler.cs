using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.AuditTrail;

/// <summary>
/// Records new surveys in the audit trail.
/// </summary>
internal sealed class SurveyCreatedAuditTrailHandler : CatalogEntryHandlerBase<Survey>
{
    private readonly ISurveyAuditTrail _auditTrail;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyCreatedAuditTrailHandler"/> class.
    /// </summary>
    /// <param name="auditTrail">The audit trail.</param>
    public SurveyCreatedAuditTrailHandler(ISurveyAuditTrail auditTrail)
    {
        _auditTrail = auditTrail;
    }

    /// <inheritdoc />
    public override Task CreatedAsync(CreatedContext<Survey> context, CancellationToken cancellationToken = default)
        => context?.Model is not { } survey
            ? Task.CompletedTask
            : _auditTrail.RecordAsync(SurveyAuditEvents.SurveyCreated, new SurveyAuditEntry
            {
                ActorId = survey.OwnerId,
                SurveyId = survey.ItemId,
                SurveyName = survey.Name,
            }, cancellationToken);
}
