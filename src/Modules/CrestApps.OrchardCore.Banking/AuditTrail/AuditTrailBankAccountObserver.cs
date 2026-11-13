using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Banking.AuditTrail;

/// <summary>
/// Writes each bank account change, and each time a full number is shown, to the Audit Trail.
/// </summary>
internal sealed class AuditTrailBankAccountObserver : IBankAccountObserver
{
    private readonly IAuditTrailManager _auditTrailManager;

    public AuditTrailBankAccountObserver(IAuditTrailManager auditTrailManager)
    {
        _auditTrailManager = auditTrailManager;
    }

    public async Task OnChangedAsync(BankAccountChange change, CancellationToken cancellationToken = default)
    {
        var account = change.Account;
        var data = new BankAccountAuditData
        {
            Name = account.Name,
            Owner = account.Owner.ToString(),
            MaskedAccountNumber = BankAccountNumbers.Masked(account),
            RoutingNumber = account.RoutingNumber,
            Status = account.Status.ToString(),
            PreviousStatus = change.PreviousStatus?.ToString(),
            Changes = change.Kinds.Select(kind => kind.ToString()).ToList(),
            Reason = change.Reason,
        };

        foreach (var name in EventNames(change))
        {
            await _auditTrailManager.RecordEventAsync(new AuditTrailContext<BankAccountAuditData>(
                name,
                BankingAuditTrailEvents.CategoryName,
                account.ItemId,
                userId: change.ActorId,
                userName: change.Actor,
                data));
        }
    }

    private static IEnumerable<string> EventNames(BankAccountChange change)
    {
        foreach (var kind in change.Kinds)
        {
            yield return kind switch
            {
                BankAccountChangeKind.Created => BankingAuditTrailEvents.AccountCreated,
                BankAccountChangeKind.NumberChanged => BankingAuditTrailEvents.NumberChanged,
                BankAccountChangeKind.StatusChanged => BankingAuditTrailEvents.StatusChanged,
                BankAccountChangeKind.Deleted => BankingAuditTrailEvents.AccountDeleted,
                BankAccountChangeKind.NumberRevealed => BankingAuditTrailEvents.NumberRevealed,
                _ => BankingAuditTrailEvents.AccountUpdated,
            };
        }
    }
}
