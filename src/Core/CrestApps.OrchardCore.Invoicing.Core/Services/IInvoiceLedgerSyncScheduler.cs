using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Mirrors a transaction onto its invoice after the work that changed it is saved: for the ledger events raised while a
/// command still has the transaction open (a status change, a cancel, a refund), when saving the invoice then would
/// commit the command half-way.
/// </summary>
public interface IInvoiceLedgerSyncScheduler
{
    /// <summary>
    /// Mirrors the transaction onto its invoice once the current unit of work has ended.
    /// </summary>
    /// <param name="transactionId">The transaction.</param>
    void Schedule(string transactionId);
}

/// <summary>
/// Runs the mirror as deferred work, after the shell scope (the request) has ended and saved, in a scope of its own that
/// saves only the invoices it changes. Outside a shell scope there is nothing to defer to: the invoice is mirrored the
/// next time it is opened or its transaction records a payment.
/// </summary>
public sealed class DeferredInvoiceLedgerSyncScheduler : IInvoiceLedgerSyncScheduler
{
    /// <summary>
    /// The name of the deferred work, for telemetry.
    /// </summary>
    public const string WorkName = "invoicing.ledger_sync";

    private readonly HashSet<string> _pending = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public void Schedule(string transactionId)
    {
        if (string.IsNullOrEmpty(transactionId) || ShellScope.Current is null)
        {
            return;
        }

        if (_pending.Count == 0)
        {
            DeferredWork.Add(WorkName, async scope =>
            {
                var transactionIds = _pending.ToArray();

                _pending.Clear();

                var sync = scope.ServiceProvider.GetRequiredService<InvoiceLedgerSync>();

                foreach (var id in transactionIds)
                {
                    await sync.SyncTransactionAsync(id);
                }

                // Only the invoices mirrored here are pending in this scope's session; a conflict is not swallowed.
                await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync();
            });
        }

        _pending.Add(transactionId);
    }
}
