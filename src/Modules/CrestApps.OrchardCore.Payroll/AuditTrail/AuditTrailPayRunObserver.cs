using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Payroll.AuditTrail;

/// <summary>
/// Records who submitted, approved or rejected, processed and cancelled a pay run, and who voided a statement, with the
/// run's number and totals: never a person's pay.
/// </summary>
internal sealed class AuditTrailPayRunObserver : IPayRunObserver, IPayStatementObserver
{
    private readonly IAuditTrailManager _auditTrailManager;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;

    public AuditTrailPayRunObserver(IAuditTrailManager auditTrailManager, Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
    {
        _auditTrailManager = auditTrailManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task ChangedAsync(PayRunChange change, CancellationToken cancellationToken = default)
    {
        var name = change.Kind switch
        {
            PayRunChangeKind.Submitted => PayrollAuditTrailEvents.RunSubmitted,
            PayRunChangeKind.Approved or PayRunChangeKind.Rejected => PayrollAuditTrailEvents.RunDecided,
            PayRunChangeKind.Paid or PayRunChangeKind.PartiallyPaid or PayRunChangeKind.Failed => PayrollAuditTrailEvents.RunProcessed,
            PayRunChangeKind.Canceled => PayrollAuditTrailEvents.RunCanceled,
            _ => null,
        };

        if (name is null)
        {
            return Task.CompletedTask;
        }

        var run = change.Run;
        var user = _httpContextAccessor.HttpContext?.User;

        return _auditTrailManager.RecordEventAsync(new AuditTrailContext<PayrollAuditData>(
            name,
            PayrollAuditTrailEvents.CategoryName,
            run.ItemId,
            userId: change.ActorId ?? user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            userName: change.Actor ?? user?.Identity?.Name,
            new PayrollAuditData
            {
                EmployerId = run.EmployerId,
                RunNumber = run.Number,
                Change = change.Kind.ToString(),
                Statements = run.Totals.Statements,
                Reason = change.Note,
            }));
    }

    public Task IssuedAsync(PayStatement statement, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task VoidedAsync(PayStatement statement, PayStatement reversal, CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return _auditTrailManager.RecordEventAsync(new AuditTrailContext<PayrollAuditData>(
            PayrollAuditTrailEvents.StatementVoided,
            PayrollAuditTrailEvents.CategoryName,
            statement.ItemId,
            userId: user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            userName: user?.Identity?.Name,
            new PayrollAuditData
            {
                StaffMemberId = statement.StaffMemberId,
                EmployerId = statement.EmployerId,
                StatementNumber = statement.Number,
                Change = "Voided",
                Reason = reversal?.Warnings.FirstOrDefault(),
            }));
    }
}
