using System.Security.Claims;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Transactions.FinancialDocuments;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Drafts and issues credit notes under the lock of the invoice they credit. Issuing one numbers it and, as its
/// disposition says, lowers what the invoice's transaction owes with a credit adjustment naming the credit note, refunds
/// what was paid, or keeps the amount as the payer's customer credit, keyed by the credit note so it is kept once.
/// </summary>
public sealed class DefaultCreditNoteCommandService : ICreditNoteCommandService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private readonly IInvoiceStore _invoices;
    private readonly ICreditNoteStore _creditNotes;
    private readonly ITransactionManager _transactions;
    private readonly ITransactionCommandService _transactionCommands;
    private readonly IFinancialDocumentNumberGenerator _numbers;
    private readonly IDocumentCalculator _calculator;
    private readonly ISiteService _siteService;
    private readonly ILocalClock _localClock;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public DefaultCreditNoteCommandService(
        IInvoiceStore invoices,
        ICreditNoteStore creditNotes,
        ITransactionManager transactions,
        ITransactionCommandService transactionCommands,
        IFinancialDocumentNumberGenerator numbers,
        IDocumentCalculator calculator,
        ISiteService siteService,
        ILocalClock localClock,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        ILogger<DefaultCreditNoteCommandService> logger,
        IStringLocalizer<DefaultCreditNoteCommandService> stringLocalizer)
    {
        _invoices = invoices;
        _creditNotes = creditNotes;
        _transactions = transactions;
        _transactionCommands = transactionCommands;
        _numbers = numbers;
        _calculator = calculator;
        _siteService = siteService;
        _localClock = localClock;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// The reference a customer credit change kept for a credit note carries.
    /// </summary>
    public static string GetCreditReference(string creditNoteId)
        => $"{InvoicingConstants.CreditNoteDocumentKind}:{creditNoteId}";

    /// <inheritdoc/>
    public async Task<CreditNoteCommandResult> CreateDraftAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        var result = new CreditNoteCommandResult();

        if (string.IsNullOrEmpty(invoiceId))
        {
            return result.Fail(S["The invoice could not be found."]);
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(DefaultInvoiceCommandService.LockPrefix + invoiceId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return result.Fail(S["This invoice is being changed by someone else. Try again in a moment."]);
        }

        await using var _ = locker;
        using var held = _serviceProvider.GetRequiredService<InvoiceCommandLocks>().Hold(invoiceId);

        var invoice = await _invoices.FindByIdAsync(invoiceId, cancellationToken);

        if (invoice is null)
        {
            return result.Fail(S["The invoice could not be found."]);
        }

        result.Invoice = invoice;

        if (!CanCredit(invoice))
        {
            return result.Fail(S["Only an issued invoice that is not void can be credited."]);
        }

        var existing = await _creditNotes.ListByInvoiceAsync(invoice.ItemId, cancellationToken);

        // One draft at a time: a second request opens the draft already started.
        if (existing.FirstOrDefault(note => note.Status == CreditNoteStatus.Draft) is { } draft)
        {
            result.CreditNote = draft;
            result.AlreadyDone = true;

            return result;
        }

        var creditable = CreditNoteRules.GetCreditable(invoice, existing);

        if (creditable.Total <= 0m)
        {
            return result.Fail(S["Everything on this invoice has been credited already."]);
        }

        var creditNote = new CreditNote
        {
            ItemId = IdGenerator.GenerateId(),
            InvoiceId = invoice.ItemId,
            InvoiceNumber = invoice.Number,
            BillTo = invoice.BillTo,
            Issuer = invoice.Issuer,
            Currency = invoice.Currency,
            ExchangeRate = invoice.ExchangeRate,
            BaseCurrency = invoice.BaseCurrency,
            PricesIncludeTax = invoice.PricesIncludeTax,
            Culture = invoice.Culture,
            TemplateId = invoice.TemplateId,
            Reference = invoice.Number,
            Disposition = await GetDefaultDispositionAsync(invoice, cancellationToken),
            CreatedUtc = _clock.UtcNow,
        };

        foreach (var line in invoice.Lines.Where(line => line.HasAmount && !string.IsNullOrEmpty(line.Id)))
        {
            if (!creditable.Lines.TryGetValue(line.Id, out var left) || left <= 0m)
            {
                continue;
            }

            creditNote.Lines.Add(CreditLine(line, left, invoice.Currency));
        }

        await InvoiceCalculation.ApplyAsync(_calculator, creditNote, await GetSettingsAsync(), invoice.IssueDate, cancellationToken);

        AddHistory(creditNote, DocumentEventKinds.Created, S["Drafted to credit invoice {0}.", invoice.Number].Value, null);

        await _creditNotes.CreateAsync(creditNote, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        result.CreditNote = creditNote;

        return result;
    }

    /// <inheritdoc/>
    public async Task<CreditNoteCommandResult> IssueAsync(string creditNoteId, CancellationToken cancellationToken = default)
    {
        var result = new CreditNoteCommandResult();
        var found = string.IsNullOrEmpty(creditNoteId) ? null : await _creditNotes.FindByIdAsync(creditNoteId, cancellationToken);

        if (found is null)
        {
            return result.Fail(S["The credit note could not be found."]);
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(DefaultInvoiceCommandService.LockPrefix + found.InvoiceId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return result.Fail(S["This invoice is being changed by someone else. Try again in a moment."]);
        }

        await using var _ = locker;
        using var held = _serviceProvider.GetRequiredService<InvoiceCommandLocks>().Hold(found.InvoiceId);

        // Read again under the lock.
        var creditNote = await _creditNotes.FindByIdAsync(creditNoteId, cancellationToken);
        var invoice = await _invoices.FindByIdAsync(found.InvoiceId, cancellationToken);

        result.CreditNote = creditNote;
        result.Invoice = invoice;

        if (creditNote.Status == CreditNoteStatus.Issued)
        {
            result.AlreadyDone = true;

            return result;
        }

        if (creditNote.Status != CreditNoteStatus.Draft)
        {
            return result.Fail(S["Only a draft credit note can be issued."]);
        }

        if (invoice is null || !CanCredit(invoice))
        {
            return result.Fail(S["Only an issued invoice that is not void can be credited."]);
        }

        var issueDate = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

        await InvoiceCalculation.ApplyAsync(_calculator, creditNote, await GetSettingsAsync(), issueDate, cancellationToken);

        foreach (var error in InvoiceIssueRules.ValidateLines(creditNote))
        {
            result.Fail(InvoiceIssueMessages.Describe(error, creditNote, S));
        }

        var others = await _creditNotes.ListByInvoiceAsync(invoice.ItemId, cancellationToken);

        foreach (var error in CreditNoteRules.Validate(invoice, others, creditNote))
        {
            result.Fail(Describe(error, invoice, creditNote));
        }

        if (!result.Succeeded)
        {
            return result;
        }

        var total = creditNote.Totals.Total;
        var currency = creditNote.Currency;
        var transaction = string.IsNullOrEmpty(invoice.TransactionId) ? null : await _transactions.FindByIdAsync(invoice.TransactionId, cancellationToken);

        // Decided before anything is numbered or moved, so a credit note that can't go through takes no number.
        switch (creditNote.Disposition)
        {
            case CreditNoteDisposition.ReduceBalance when transaction is null || transaction.OutstandingAmount < total:
                return result.Fail(S["Only {0} is still owed on this invoice, so the credit note can't lower the balance by {1}. Refund it or keep it as the customer's credit instead.", Money(transaction?.OutstandingAmount ?? 0m, currency), Money(total, currency)]);

            case CreditNoteDisposition.Refund when transaction is null || transaction.AmountPaid - transaction.AmountRefunded < total:
                return result.Fail(S["Only {0} was paid on this invoice, so {1} can't be refunded.", Money(Math.Max(0m, (transaction?.AmountPaid ?? 0m) - (transaction?.AmountRefunded ?? 0m)), currency), Money(total, currency)]);

            case CreditNoteDisposition.CustomerCredit when _serviceProvider.GetService<ICustomerCreditService>() is not { } credit || !credit.CanHold(invoice.BillTo?.Party ?? default):
                return result.Fail(S["The customer can't hold credit (the Store Credit feature is off, or the invoice is billed to someone without an account). Choose another way to credit it."]);
        }

        var number = await UnusedDocumentNumbers.NextAsync(
            _numbers,
            FinancialDocumentKind.CreditNote,
            async candidate => await _creditNotes.FindByNumberAsync(candidate, cancellationToken) is not null,
            cancellationToken);

        if (number is null)
        {
            await _session.CancelAsync();

            return result.Fail(S["The credit note number series keeps giving numbers other credit notes already hold. Change its format under Settings > Business documents > Number series."]);
        }

        creditNote.Number = number.PublicToken;

        var moved = await MoveMoneyAsync(creditNote, invoice, total, result, cancellationToken);

        if (!moved)
        {
            // Nothing moved, so nothing is saved: the number taken in this session is given back with it.
            await _session.CancelAsync();

            return result;
        }

        var now = _clock.UtcNow;

        creditNote.Status = CreditNoteStatus.Issued;
        creditNote.IssuedUtc = now;
        creditNote.IssueDate = issueDate;
        creditNote.ModifiedUtc = now;

        AddHistory(creditNote, DocumentEventKinds.Issued, S["Issued as {0}.", creditNote.Number].Value, total);
        AddHistory(invoice, DocumentEventKinds.Credited, S["Credited by {0}: {1}", creditNote.Number, Money(total, currency)].Value, total, InvoicingConstants.CreditNoteDocumentKind, creditNote.ItemId);

        // The invoice follows the ledger, now with this credit note among those that credit it.
        var credited = others.Where(note => note.Status == CreditNoteStatus.Issued && !string.Equals(note.ItemId, creditNote.ItemId, StringComparison.Ordinal)).Sum(note => note.Totals.Total) + total;

        if (!string.IsNullOrEmpty(invoice.TransactionId) && await _transactions.FindByIdAsync(invoice.TransactionId, cancellationToken) is { } current)
        {
            InvoiceLedgerMirror.Apply(invoice, current, credited, S);
        }
        else
        {
            invoice.AmountCredited = credited;
        }

        invoice.ModifiedUtc = now;

        try
        {
            await _creditNotes.UpdateAsync(creditNote, cancellationToken);
            await _invoices.UpdateAsync(invoice, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            _logger.LogError("Credit note '{CreditNoteId}' moved its money but could not be saved because invoice '{InvoiceId}' was changed meanwhile.", creditNote.ItemId, invoice.ItemId);

            return result.Fail(S["The credit note was applied but could not be saved because the invoice was changed meanwhile. Reload the invoice and check it."]);
        }

        // The PDF of what was issued is kept; without it the credit note is rendered from what it keeps.
        if (_serviceProvider.GetService<IInvoicePdfService>() is { } pdf)
        {
            try
            {
                if (await pdf.ArchiveAsync(creditNote, cancellationToken))
                {
                    await _creditNotes.UpdateAsync(creditNote, cancellationToken);
                    await _session.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "The PDF of credit note '{CreditNoteId}' could not be archived.", creditNote.ItemId);
            }
        }

        await _serviceProvider.GetRequiredService<InvoiceObserverNotifier>().NotifyAsync(invoice, observer => observer.CreditNoteIssuedAsync(creditNote, invoice, cancellationToken));

        return result;
    }

    // Lowers the balance, refunds or keeps customer credit. Each is keyed by the credit note (the adjustment names it, the
    // credit change carries its reference), so a retry never moves the money twice.
    private async Task<bool> MoveMoneyAsync(CreditNote creditNote, Invoice invoice, decimal total, CreditNoteCommandResult result, CancellationToken cancellationToken)
    {
        var reason = string.IsNullOrWhiteSpace(creditNote.Reason)
            ? S["Credit note {0} for invoice {1}", creditNote.Number, invoice.Number].Value
            : S["Credit note {0} for invoice {1}: {2}", creditNote.Number, invoice.Number, creditNote.Reason].Value;

        switch (creditNote.Disposition)
        {
            case CreditNoteDisposition.ReduceBalance:
                var adjusted = await _transactionCommands.AdjustAsync(invoice.TransactionId, new TransactionAdjustmentRequest
                {
                    Amount = total,
                    Kind = TransactionAdjustmentKind.Credit,
                    Reason = reason,
                    DocumentKind = InvoicingConstants.CreditNoteDocumentKind,
                    DocumentId = creditNote.ItemId,
                    DocumentNumber = creditNote.Number,
                }, cancellationToken);

                Copy(adjusted.Errors, result);
                creditNote.AmountApplied = adjusted.Succeeded ? total : 0m;

                return adjusted.Succeeded;

            case CreditNoteDisposition.Refund:
                var refunded = await _transactionCommands.RefundAsync(invoice.TransactionId, total, reason, cancellationToken);

                Copy(refunded.Errors, result);
                creditNote.AmountRefunded = refunded.Succeeded ? total : 0m;

                return refunded.Succeeded;

            default:
                var credit = _serviceProvider.GetRequiredService<ICustomerCreditService>();
                var kept = await credit.AddAsync(new CustomerCreditChange
                {
                    Party = invoice.BillTo.Party,
                    Currency = creditNote.Currency,
                    Amount = total,
                    Reason = reason,
                    Reference = GetCreditReference(creditNote.ItemId),
                    Actor = _httpContextAccessor.HttpContext?.User?.Identity?.Name,
                }, cancellationToken);

                if (!kept.Succeeded)
                {
                    result.Fail(S["The customer's credit could not be changed ({0}).", kept.Error]);

                    return false;
                }

                creditNote.AmountCredited = total;

                return true;
        }
    }

    // A line still creditable in full is copied as it is; one partly credited already is credited by what is left, as a
    // single amount, which staff can change on the draft.
    private static DocumentLine CreditLine(DocumentLine line, decimal left, string currency)
    {
        var credited = new DocumentLine
        {
            Id = IdGenerator.GenerateId(),
            Kind = line.Kind,
            ProductId = line.ProductId,
            Sku = line.Sku,
            Name = line.Name,
            Description = line.Description,
            Quantity = line.Quantity,
            Unit = line.Unit,
            UnitPrice = line.UnitPrice,
            DiscountPercent = line.DiscountPercent,
            DiscountAmount = line.DiscountAmount,
            TaxCategoryCode = line.TaxCategoryCode,
            Taxes = [.. line.Taxes],
            TaxTreatment = line.TaxTreatment,
            TaxTreatmentReason = line.TaxTreatmentReason,
            ServiceStart = line.ServiceStart,
            ServiceEnd = line.ServiceEnd,
            CreditedLineId = line.Id,
        };

        if (left < line.Total && line.Total > 0m)
        {
            credited.Quantity = 1m;
            credited.DiscountPercent = null;
            credited.DiscountAmount = null;
            credited.UnitPrice = CurrencyScale.Round(line.Net * left / line.Total, currency);
        }

        return credited;
    }

    private async Task<CreditNoteDisposition> GetDefaultDispositionAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(invoice.TransactionId) &&
            await _transactions.FindByIdAsync(invoice.TransactionId, cancellationToken) is { OutstandingAmount: > 0m })
        {
            return CreditNoteDisposition.ReduceBalance;
        }

        return _serviceProvider.GetService<ICustomerCreditService>() is { } credit && credit.CanHold(invoice.BillTo?.Party ?? default)
            ? CreditNoteDisposition.CustomerCredit
            : CreditNoteDisposition.Refund;
    }

    // An invoice split into a payment schedule owes through its payments, not one debt a credit note could lower.
    private static bool CanCredit(Invoice invoice)
        => !string.IsNullOrEmpty(invoice.Number) && invoice.Status is InvoiceStatus.Open or InvoiceStatus.Uncollectible &&
            string.IsNullOrEmpty(invoice.InstallmentPlanId);

    private string Describe(CreditNoteError error, Invoice invoice, CreditNote creditNote)
    {
        var lineName = string.IsNullOrEmpty(error.LineId)
            ? null
            : invoice.Lines.FirstOrDefault(line => string.Equals(line.Id, error.LineId, StringComparison.Ordinal))?.Name ?? error.LineId;

        return error.Code switch
        {
            CreditNoteRules.NothingCredited => S["The credit note credits nothing. Keep at least one line with an amount."],
            CreditNoteRules.UnknownLine => S["A line of the credit note credits a line that is not on invoice {0}.", invoice.Number],
            CreditNoteRules.LineExceeded => S["\"{0}\" can be credited by {1} at most.", lineName, Money(error.Allowed ?? 0m, creditNote.Currency)],
            CreditNoteRules.TotalExceeded => S["Invoice {0} can be credited by {1} at most.", invoice.Number, Money(error.Allowed ?? 0m, creditNote.Currency)],
            _ => error.Code,
        };
    }

    private void AddHistory(InvoicingDocument document, string kind, string message, decimal? amount, string referenceType = null, string referenceId = null)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        document.History.Add(new DocumentEvent
        {
            Id = IdGenerator.GenerateId(),
            Kind = kind,
            CreatedUtc = _clock.UtcNow,
            ActorId = user?.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorName = user?.Identity?.Name,
            Message = message,
            Amount = amount,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
        });
    }

    private static void Copy(IEnumerable<string> errors, CreditNoteCommandResult result)
    {
        foreach (var error in errors)
        {
            result.Fail(error);
        }
    }

    private static string Money(decimal amount, string currency)
        => $"{CurrencyScale.Format(amount, currency)} {currency}";

    private async Task<InvoicingSettings> GetSettingsAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();

        return site.GetOrCreate<InvoicingSettings>() ?? new InvoicingSettings();
    }
}
