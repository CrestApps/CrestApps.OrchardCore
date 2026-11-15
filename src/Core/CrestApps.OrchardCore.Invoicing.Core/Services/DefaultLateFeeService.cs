using System.Globalization;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Settings;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// The default <see cref="ILateFeeService"/>. Each invoice is assessed under a lock of its own; each fee is a debit note
/// made, issued (and sent when the policy says so) in a unit of work of its own, found again by its source.
/// </summary>
public sealed class DefaultLateFeeService : ILateFeeService
{
    /// <summary>
    /// The prefix of the lock an invoice's assessment takes.
    /// </summary>
    public const string LockPrefix = "Invoicing:LateFee:";

    private const int InvoicesPerSweep = 200;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(5);

    private readonly IInvoiceManager _invoices;
    private readonly ILateFeePolicyManager _policies;
    private readonly ITransactionManager _transactions;
    private readonly IDocumentCalculator _calculator;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public DefaultLateFeeService(
        IInvoiceManager invoices,
        ILateFeePolicyManager policies,
        ITransactionManager transactions,
        IDocumentCalculator calculator,
        IDistributedLock distributedLock,
        ISession session,
        ISiteService siteService,
        IServiceProvider serviceProvider,
        ILogger<DefaultLateFeeService> logger,
        IStringLocalizer<DefaultLateFeeService> stringLocalizer)
    {
        _invoices = invoices;
        _policies = policies;
        _transactions = transactions;
        _calculator = calculator;
        _distributedLock = distributedLock;
        _session = session;
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<int> AssessDueAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        if ((await _policies.GetEnabledAsync(cancellationToken)).Count == 0)
        {
            return 0;
        }

        var overdue = await _invoices.PageAsync(1, InvoicesPerSweep, new InvoiceQuery { OverdueOn = today, IssuedOnly = true }, cancellationToken);
        var issued = 0;

        foreach (var invoice in overdue.Entries.Where(invoice => invoice.Kind != InvoiceKind.DebitNote))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                issued += (await AssessAsync(invoice.ItemId, today, cancellationToken)).DebitNotes.Count;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "The late fees of invoice '{InvoiceId}' could not be assessed.", invoice.ItemId);
            }
        }

        return issued;
    }

    /// <inheritdoc/>
    public async Task<LateFeeResult> AssessAsync(string invoiceId, DateOnly today, CancellationToken cancellationToken = default)
    {
        var result = new LateFeeResult();

        if (string.IsNullOrEmpty(invoiceId))
        {
            return result;
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockPrefix + invoiceId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            result.Errors.Add(S["The late fees of this invoice are being worked out already."]);

            return result;
        }

        await using var _ = locker;

        var invoice = await _invoices.FindByIdAsync(invoiceId, cancellationToken);

        if (invoice is not { Status: InvoiceStatus.Open, LateFeesExempt: false, DueDate: { } dueDate } ||
            invoice.Kind == InvoiceKind.DebitNote ||
            string.IsNullOrEmpty(invoice.TransactionId))
        {
            return result;
        }

        var daysLate = today.DayNumber - dueDate.DayNumber;

        if (daysLate <= 0)
        {
            return result;
        }

        var policies = await _policies.GetEnabledAsync(cancellationToken);
        var charged = (await ListDebitNotesAsync(invoice.ItemId, cancellationToken)).ToList();
        var settings = await _siteService.GetSettingsAsync<InvoicingSettings>();

        foreach (var policy in policies)
        {
            if (daysLate <= policy.GraceDays)
            {
                continue;
            }

            // The balance is read from the ledger right before each fee: a payment recorded meanwhile counts.
            var owed = (await _transactions.FindByIdAsync(invoice.TransactionId, cancellationToken))?.OutstandingAmount ?? invoice.AmountDue;

            if (owed <= 0m || owed < policy.MinimumBalance)
            {
                continue;
            }

            var stepsDue = policy.RepeatEveryDays > 0 ? 1 + ((daysLate - policy.GraceDays - 1) / policy.RepeatEveryDays) : 1;

            if (policy.MaxFees is { } max)
            {
                stepsDue = Math.Min(stepsDue, max);
            }

            var prefix = $"{invoice.ItemId}:{policy.ItemId}:";
            var mine = charged.Where(note => note.SourceId?.StartsWith(prefix, StringComparison.Ordinal) == true).ToList();
            var lastStep = mine.Select(note => int.TryParse(note.SourceId[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var step) ? step : 0).DefaultIfEmpty(0).Max();
            var nextStep = lastStep + 1;

            if (nextStep > stepsDue)
            {
                continue;
            }

            var amount = policy.Kind == LateFeeKind.Flat
                ? policy.Amount
                : Math.Round(owed * policy.Percent / 100m, 2, MidpointRounding.AwayFromZero);

            if (policy.CapAmount is { } cap)
            {
                var chargedSoFar = mine.Where(note => note.Status != InvoiceStatus.Void).Sum(note => note.Totals?.Total ?? 0m);

                amount = Math.Min(amount, cap - chargedSoFar);
            }

            if (amount <= 0m)
            {
                continue;
            }

            var debitNote = await IssueDebitNoteAsync(invoice, policy, prefix + nextStep.ToString(CultureInfo.InvariantCulture), nextStep, amount, owed, today, settings, result, cancellationToken);

            if (debitNote is not null)
            {
                result.DebitNotes.Add(debitNote);
                charged.Add(debitNote);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Invoice>> ListDebitNotesAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(invoiceId))
        {
            return [];
        }

        var page = await _invoices.PageAsync(1, 500, new InvoiceQuery { RelatedInvoiceId = invoiceId, SourceType = InvoicingConstants.LateFeeReferenceType }, cancellationToken);

        return [.. page.Entries.OrderBy(note => note.CreatedUtc).ThenBy(StepOf)];
    }

    // The step a debit note charged: the number at the end of its source.
    private static int StepOf(Invoice debitNote)
    {
        var source = debitNote.SourceId ?? string.Empty;
        var separator = source.LastIndexOf(':');

        return separator >= 0 && int.TryParse(source[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var step) ? step : 0;
    }

    private async Task<Invoice> IssueDebitNoteAsync(
        Invoice invoice,
        LateFeePolicy policy,
        string sourceId,
        int step,
        decimal amount,
        decimal owed,
        DateOnly today,
        InvoicingSettings settings,
        LateFeeResult result,
        CancellationToken cancellationToken)
    {
        var debitNote = await _invoices.NewAsync(cancellationToken: cancellationToken);

        debitNote.Kind = InvoiceKind.DebitNote;
        debitNote.RelatedInvoiceId = invoice.ItemId;
        debitNote.SourceType = InvoicingConstants.LateFeeReferenceType;
        debitNote.SourceId = sourceId;
        debitNote.BillTo = new DocumentParty
        {
            Party = invoice.BillTo?.Party ?? default,
            Contact = invoice.BillTo?.Contact,
            ShippingAddress = invoice.BillTo?.ShippingAddress,
            TaxId = invoice.BillTo?.TaxId,
            TaxTreatment = invoice.BillTo?.TaxTreatment ?? default,
            ExemptionReason = invoice.BillTo?.ExemptionReason,
            CopyTo = [.. invoice.BillTo?.CopyTo ?? []],
        };
        debitNote.Currency = invoice.Currency;
        debitNote.PricesIncludeTax = false;
        debitNote.DueDate = today;
        debitNote.AllowPartialPayment = invoice.AllowPartialPayment;
        debitNote.Reference = invoice.Number;
        debitNote.PurchaseOrderNumber = invoice.PurchaseOrderNumber;
        debitNote.TemplateId = invoice.TemplateId;
        debitNote.Notes = S["A late fee on invoice {0}, which was due on {1} and still owes {2} {3}.", invoice.Number, invoice.DueDate?.ToString("d", CultureInfo.CurrentCulture), owed.ToString("0.00", CultureInfo.CurrentCulture), invoice.Currency].Value;
        debitNote.Lines.Add(new DocumentLine
        {
            Id = IdGenerator.GenerateId(),
            Kind = DocumentLineKind.Fee,
            Name = step == 1
                ? S["Late fee on invoice {0}", invoice.Number].Value
                : S["Late fee {1} on invoice {0}", invoice.Number, step].Value,
            Description = policy.Kind == LateFeeKind.Percent
                ? S["{0}% of {1} {2} still due", policy.Percent.ToString("0.##", CultureInfo.CurrentCulture), owed.ToString("0.00", CultureInfo.CurrentCulture), invoice.Currency].Value
                : null,
            Quantity = 1m,
            UnitPrice = amount,
            TaxTreatment = policy.Taxable ? null : TaxTreatment.OutOfScope,
            TaxTreatmentReason = policy.Taxable ? null : S["Late fee"].Value,
            SourceType = InvoicingConstants.LateFeeReferenceType,
            SourceId = policy.ItemId,
        });

        await InvoiceCalculation.ApplyAsync(_calculator, debitNote, settings, issueDate: null, cancellationToken);

        var validation = await _invoices.ValidateAsync(debitNote, cancellationToken);

        if (!validation.Succeeded)
        {
            result.Errors.Add(string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));

            return null;
        }

        await _invoices.CreateAsync(debitNote, cancellationToken);

        // Issuing reads the draft under its lock in a unit of work of its own, so the draft is written first; a refused
        // issue cancels only its own session.
        await _session.SaveChangesAsync(cancellationToken);

        await using var scope = _serviceProvider.CreateAsyncScope();

        var issued = await scope.ServiceProvider.GetRequiredService<IInvoiceCommandService>().IssueAsync(debitNote.ItemId, cancellationToken);

        if (!issued.Succeeded)
        {
            var reason = string.Join(" ", issued.Errors);

            result.Errors.Add(reason);

            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("The late fee debit note '{DebitNoteId}' of invoice '{InvoiceId}' was made but not issued: {Reason}", debitNote.ItemId, invoice.ItemId, reason);
            }

            return debitNote;
        }

        if (policy.SendDebitNote && scope.ServiceProvider.GetService<IInvoiceSender>() is { } sender)
        {
            var sent = await sender.SendAsync(new InvoiceSendRequest { InvoiceId = debitNote.ItemId }, cancellationToken);

            if (!sent.Succeeded && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("The late fee debit note '{DebitNoteId}' could not be sent: {Reason}", debitNote.ItemId, string.Join(" ", sent.Errors));
            }
        }

        return issued.Invoice;
    }
}
