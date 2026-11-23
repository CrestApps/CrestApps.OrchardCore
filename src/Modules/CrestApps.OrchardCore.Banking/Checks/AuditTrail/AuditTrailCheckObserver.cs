using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Banking.Checks.AuditTrail;

/// <summary>
/// Writes each edit of a check waiting to print to the Audit Trail, with what it was and became. The other changes
/// of a check are kept on the check itself and start workflows.
/// </summary>
internal sealed class AuditTrailCheckObserver : ICheckObserver
{
    private readonly IAuditTrailManager _auditTrailManager;

    public AuditTrailCheckObserver(IAuditTrailManager auditTrailManager)
    {
        _auditTrailManager = auditTrailManager;
    }

    public async Task OnChangedAsync(CheckChange change, CancellationToken cancellationToken = default)
    {
        if (change.Kind != CheckChangeKind.Updated)
        {
            return;
        }

        var check = change.Check;
        var previous = change.PreviousDetails ?? CheckDetails.From(check);

        await _auditTrailManager.RecordEventAsync(new AuditTrailContext<CheckAuditData>(
            ChecksAuditTrailEvents.CheckUpdated,
            ChecksAuditTrailEvents.CategoryName,
            check.ItemId,
            userId: change.ActorId,
            userName: change.Actor,
            Data(check, previous, change.Reason)));
    }

    internal static CheckAuditData Data(CheckRecord check, CheckDetails previous, string reason)
        => new()
        {
            BankAccountId = check.BankAccountId,
            Number = check.Number,
            Currency = check.Currency,
            SourceKind = check.SourceKind,
            SourceId = check.SourceId,
            PreviousPayeeName = previous.PayeeName,
            PayeeName = check.PayeeName,
            PreviousAmount = previous.Amount,
            Amount = check.Amount,
            PreviousCheckDate = previous.CheckDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            CheckDate = check.CheckDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            PreviousMemo = previous.Memo,
            Memo = check.Memo,
            AddressChanged = !string.Equals(JsonSerializer.Serialize(previous.PayeeAddress), JsonSerializer.Serialize(check.PayeeAddress), StringComparison.Ordinal),
            Reason = reason,
        };
}
