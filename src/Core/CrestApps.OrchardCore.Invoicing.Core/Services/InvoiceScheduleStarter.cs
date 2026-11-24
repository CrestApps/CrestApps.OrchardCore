using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Splits an issued invoice's debt into its payment schedule: the installment plan service makes a payment of its own on
/// the ledger for each part, due on its date and invoiced to the customer then or charged to their saved card when the
/// invoice is charged automatically, and replaces the invoice's debt. The invoice then follows those payments.
/// </summary>
public sealed class InvoiceScheduleStarter : IInvoiceObserver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public InvoiceScheduleStarter(IServiceProvider serviceProvider, ILogger<InvoiceScheduleStarter> logger, IStringLocalizer<InvoiceScheduleStarter> stringLocalizer)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task IssuedAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (invoice?.Schedule is not { Count: > 0 } || !string.IsNullOrEmpty(invoice.InstallmentPlanId) || string.IsNullOrEmpty(invoice.TransactionId))
        {
            return;
        }

        var invoices = _serviceProvider.GetRequiredService<IInvoiceStore>();
        var plans = _serviceProvider.GetService<IInstallmentPlanService>();
        var (amounts, errors) = InvoiceSchedule.Resolve(invoice, invoice.IssueDate ?? DateOnly.MinValue, S);
        string failure = null;
        InstallmentPlanResult created = null;

        if (plans is null)
        {
            failure = S["Installment plans are not enabled."].Value;
        }
        else if (errors.Count > 0)
        {
            failure = string.Join(" ", errors);
        }
        else
        {
            var localClock = _serviceProvider.GetRequiredService<ILocalClock>();
            var payments = new List<InstallmentScheduleEntry>();

            for (var i = 0; i < amounts.Count; i++)
            {
                payments.Add(new InstallmentScheduleEntry
                {
                    DueUtc = DateTime.SpecifyKind(await localClock.ToUtcLenientAsync(invoice.Schedule[i].DueDate.ToDateTime(TimeOnly.MinValue)), DateTimeKind.Utc),
                    Amount = amounts[i],
                });
            }

            created = await plans.CreateScheduleAsync(new CreateInstallmentScheduleRequest
            {
                TransactionId = invoice.TransactionId,
                Title = S["Invoice {0}", invoice.Number].Value,
                Payments = payments,
                CollectionMethod = invoice.AutoCharge ? InstallmentCollectionMethod.AutoCharge : InstallmentCollectionMethod.Invoice,
            }, cancellationToken);

            if (!created.Succeeded)
            {
                failure = string.Join(" ", created.Errors.Select(error => error.Value));
            }
        }

        // The invoice is read again: the plan service saved, which detaches what this scope read before.
        var saved = await invoices.FindByIdAsync(invoice.ItemId, cancellationToken);

        if (saved is null)
        {
            return;
        }

        var clock = _serviceProvider.GetRequiredService<IClock>();

        if (failure is null)
        {
            var plan = created.Plan;

            saved.InstallmentPlanId = plan.ItemId;

            for (var i = 0; i < saved.Schedule.Count && i < plan.Payments.Count; i++)
            {
                saved.Schedule[i].Number = plan.Payments[i].Number;
                saved.Schedule[i].TransactionId = plan.Payments[i].TransactionId;
                saved.Schedule[i].IssuedAmount = plan.Payments[i].Amount;
            }

            saved.History.Add(new DocumentEvent
            {
                Id = IdGenerator.GenerateId(),
                Kind = DocumentEventKinds.Note,
                CreatedUtc = clock.UtcNow,
                Message = S["The amount due was split into {0} payments, each due on its date.", plan.Payments.Count].Value,
            });
        }
        else
        {
            saved.History.Add(new DocumentEvent
            {
                Id = IdGenerator.GenerateId(),
                Kind = DocumentEventKinds.Note,
                CreatedUtc = clock.UtcNow,
                Message = S["The payment schedule could not be set, so the whole amount is due on the due date: {0}", failure].Value,
            });

            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("The payment schedule of invoice '{InvoiceId}' could not be set: {Failure}", invoice.ItemId, failure);
            }
        }

        saved.ModifiedUtc = clock.UtcNow;
        await invoices.UpdateAsync(saved, cancellationToken);
        await _serviceProvider.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

        // What the invoice owes now comes from its payments.
        await _serviceProvider.GetRequiredService<InvoiceLedgerSync>().SyncAsync(saved, cancellationToken);
        await _serviceProvider.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);
    }
}
