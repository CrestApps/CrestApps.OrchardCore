using CrestApps.OrchardCore.Payroll.Services;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Payroll.AuditTrail;

/// <summary>
/// Writes each tax id reveal, tax id, election and net pay change of a pay profile to the Audit Trail.
/// </summary>
internal sealed class AuditTrailPayProfileObserver : IPayProfileObserver
{
    private readonly IAuditTrailManager _auditTrailManager;

    public AuditTrailPayProfileObserver(IAuditTrailManager auditTrailManager)
    {
        _auditTrailManager = auditTrailManager;
    }

    public Task ChangedAsync(PayProfileChange change, CancellationToken cancellationToken = default)
    {
        var profile = change.Profile;
        var name = change.Kind switch
        {
            PayProfileChangeKind.TaxIdRevealed => PayrollAuditTrailEvents.TaxIdRevealed,
            PayProfileChangeKind.TaxIdChanged => PayrollAuditTrailEvents.TaxIdChanged,
            PayProfileChangeKind.ElectionAdded => PayrollAuditTrailEvents.ElectionAdded,
            PayProfileChangeKind.DistributionChanged => PayrollAuditTrailEvents.DistributionChanged,
            _ => PayrollAuditTrailEvents.ProfileChanged,
        };

        return _auditTrailManager.RecordEventAsync(new AuditTrailContext<PayrollAuditData>(
            name,
            PayrollAuditTrailEvents.CategoryName,
            profile.ItemId,
            userId: change.ActorId,
            userName: change.Actor,
            new PayrollAuditData
            {
                StaffMemberId = profile.StaffMemberId,
                EmployerId = profile.EmployerId,
                TaxIdHint = profile.TaxId?.Hint,
                Reason = change.Reason,
            }));
    }
}
