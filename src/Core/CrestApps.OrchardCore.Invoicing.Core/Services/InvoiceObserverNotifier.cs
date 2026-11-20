using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Tells the <see cref="IInvoiceObserver"/> implementations what happened to an invoice. A failing observer is logged and
/// the others still hear it.
/// </summary>
public sealed class InvoiceObserverNotifier
{
    /// <summary>
    /// The name of the deferred work that notifies after the request has saved, for telemetry.
    /// </summary>
    public const string WorkName = "invoicing_observers";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public InvoiceObserverNotifier(IServiceProvider serviceProvider, ILogger<InvoiceObserverNotifier> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Notifies the observers now: for a change that is already saved.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="notify">What to tell each observer.</param>
    public async Task NotifyAsync(Invoice invoice, Func<IInvoiceObserver, Task> notify)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(notify);

        foreach (var observer in _serviceProvider.GetServices<IInvoiceObserver>())
        {
            try
            {
                await notify(observer);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "An invoice observer '{Observer}' failed on invoice '{InvoiceId}'.", observer.GetType().FullName, invoice.ItemId);
            }
        }
    }

    /// <summary>
    /// Notifies the observers once the current unit of work is saved: for a change queued in the session that someone
    /// else saves (the request, or the command that called). The invoice is read again in the scope that notifies. Outside
    /// a shell scope there is nothing to wait for, so the observers hear it at once.
    /// </summary>
    /// <param name="invoice">The invoice as it is now.</param>
    /// <param name="notify">What to tell each observer, given the invoice.</param>
    public Task NotifyAfterSaveAsync(Invoice invoice, Func<IInvoiceObserver, Invoice, Task> notify)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(notify);

        if (ShellScope.Current is null)
        {
            return NotifyAsync(invoice, observer => notify(observer, invoice));
        }

        var invoiceId = invoice.ItemId;

        DeferredWork.Add(WorkName, async scope =>
        {
            var saved = await scope.ServiceProvider.GetRequiredService<IInvoiceStore>().FindByIdAsync(invoiceId);

            if (saved is not null)
            {
                await scope.ServiceProvider.GetRequiredService<InvoiceObserverNotifier>().NotifyAsync(saved, observer => notify(observer, saved));
            }
        });

        return Task.CompletedTask;
    }
}
