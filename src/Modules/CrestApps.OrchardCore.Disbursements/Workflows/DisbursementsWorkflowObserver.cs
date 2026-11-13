using System.Globalization;
using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.Disbursements.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Disbursements.Workflows;

/// <summary>
/// Starts the Disbursements workflow events after the change is saved.
/// </summary>
internal sealed class DisbursementsWorkflowObserver : IDisbursementObserver
{
    public Task OnChangedAsync(DisbursementChange change, CancellationToken cancellationToken = default)
    {
        var name = change.Kind switch
        {
            DisbursementChangeKind.Submitted => nameof(DisbursementSubmittedEvent),
            DisbursementChangeKind.Settled => nameof(DisbursementSettledEvent),
            DisbursementChangeKind.Returned => nameof(DisbursementReturnedEvent),
            DisbursementChangeKind.Failed or DisbursementChangeKind.Canceled or DisbursementChangeKind.Reversed => nameof(DisbursementFailedEvent),
            _ => null,
        };

        if (name is null)
        {
            return Task.CompletedTask;
        }

        var disbursement = change.Disbursement;

        // Never an account number: the input names the payee and the record paid.
        var input = new Dictionary<string, object>
        {
            ["DisbursementId"] = disbursement.ItemId,
            ["PayeeName"] = disbursement.PayeeName,
            ["Amount"] = disbursement.Amount,
            ["Currency"] = disbursement.Currency,
            ["Method"] = disbursement.Method,
            ["Status"] = disbursement.Status.ToString(),
            ["Change"] = change.Kind.ToString(),
            ["SourceKind"] = disbursement.SourceKind,
            ["SourceId"] = disbursement.SourceId,
            ["BatchId"] = disbursement.BatchId,
            ["TraceNumber"] = disbursement.TraceNumber,
            ["FailureCode"] = disbursement.FailureCode,
            ["FailureReason"] = disbursement.FailureReason,
            ["IsPrenote"] = disbursement.IsPrenote,
            ["EffectiveDate"] = disbursement.EffectiveDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["Actor"] = change.Actor,
        };

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(name, input, disbursement.ItemId));

        return Task.CompletedTask;
    }
}
