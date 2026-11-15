using System.Globalization;
using System.Security.Claims;
using OrchardCore;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Commerce.Stores;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.ExchangeRates.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.FinancialDocuments;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// The default <see cref="IInvoiceCommandService"/>.
/// </summary>
public sealed class DefaultInvoiceCommandService : IInvoiceCommandService
{
    /// <summary>
    /// The prefix of the distributed lock each invoice command takes.
    /// </summary>
    public const string LockPrefix = "Invoicing:Invoice:";

    /// <summary>
    /// The source of the transactions invoices post.
    /// </summary>
    public const string TransactionSource = "Invoicing";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private readonly IInvoiceStore _store;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultInvoiceCommandService"/> class.
    /// </summary>
    /// <param name="store">The invoice store, which commands save through without the draft handlers.</param>
    /// <param name="creditNotes">The credit note store, which tells whether a credit note was issued for an invoice.</param>
    /// <param name="transactions">The ledger the invoice posts what is owed to.</param>
    /// <param name="transactionCommands">The ledger commands that cancel and adjust the invoice's transaction.</param>
    /// <param name="numbers">The generator of invoice numbers.</param>
    /// <param name="calculator">The document calculator.</param>
    /// <param name="siteService">The site service, for the invoicing settings and the store profile.</param>
    /// <param name="localClock">The clock of the site's time zone, for the issue and due dates.</param>
    /// <param name="distributedLock">The lock each command takes on its invoice.</param>
    /// <param name="session">The session the issue is saved in, with its number and transaction.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="httpContextAccessor">The accessor for the signed-in user recorded on the history.</param>
    /// <param name="serviceProvider">The service provider the optional services (payment terms, billing profiles, observers) come from.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultInvoiceCommandService(
        IInvoiceStore store,
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
        ILogger<DefaultInvoiceCommandService> logger,
        IStringLocalizer<DefaultInvoiceCommandService> stringLocalizer)
    {
        _store = store;
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

    /// <inheritdoc/>
    public Task<InvoiceCommandResult> IssueAsync(string invoiceId, CancellationToken cancellationToken = default)
        => RunAsync(invoiceId, (invoice, result) => IssueCoreAsync(invoice, result, approvedBy: null, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public Task<InvoiceCommandResult> ApproveAsync(string invoiceId, string approvedBy, CancellationToken cancellationToken = default)
        => RunAsync(invoiceId, (invoice, result) =>
        {
            if (string.IsNullOrWhiteSpace(approvedBy))
            {
                result.Fail(S["Say who approves the invoice."]);

                return Task.FromResult<Func<IInvoiceObserver, Task>>(null);
            }

            return IssueCoreAsync(invoice, result, approvedBy.Trim(), cancellationToken);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<InvoiceCommandResult> SubmitForApprovalAsync(string invoiceId, string submittedBy, CancellationToken cancellationToken = default)
        => RunAsync(invoiceId, async (invoice, result) =>
        {
            if (invoice.Status == InvoiceStatus.PendingApproval)
            {
                result.AlreadyDone = true;

                return null;
            }

            if (invoice.Status != InvoiceStatus.Draft)
            {
                result.Fail(S["Only a draft can be submitted for approval."]);

                return null;
            }

            var settings = await GetSettingsAsync();

            await FillFromProfileAsync(invoice, settings, cancellationToken);
            await InvoiceCalculation.ApplyAsync(_calculator, invoice, settings, invoice.IssueDate, cancellationToken);

            foreach (var error in InvoiceIssueRules.ValidateForIssue(invoice).Concat(InvoiceIssueRules.ValidateTotals(invoice)))
            {
                result.Fail(InvoiceIssueMessages.Describe(error, invoice, S));
            }

            if (!result.Succeeded)
            {
                return null;
            }

            if (!InvoiceApprovalRules.Requires(invoice, settings))
            {
                result.Fail(S["This invoice doesn't need approval: issue it."]);

                return null;
            }

            invoice.Status = InvoiceStatus.PendingApproval;
            invoice.Approval = new InvoiceApproval
            {
                SubmittedBy = string.IsNullOrWhiteSpace(submittedBy) ? null : submittedBy.Trim(),
                SubmittedUtc = _clock.UtcNow,
            };

            AddHistory(invoice, DocumentEventKinds.Submitted, S["Submitted for approval."].Value, invoice.Totals.Total);

            return null;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<InvoiceCommandResult> RejectAsync(string invoiceId, string rejectedBy, string reason, CancellationToken cancellationToken = default)
        => RunAsync(invoiceId, (invoice, result) =>
        {
            var trimmed = reason?.Trim();

            if (invoice.Status != InvoiceStatus.PendingApproval)
            {
                result.Fail(S["Only an invoice waiting for approval can be rejected."]);
            }
            else if (string.IsNullOrEmpty(trimmed))
            {
                result.Fail(S["Say why the invoice is rejected."]);
            }
            else
            {
                invoice.Status = InvoiceStatus.Draft;
                invoice.Approval ??= new InvoiceApproval();
                invoice.Approval.RejectedBy = string.IsNullOrWhiteSpace(rejectedBy) ? null : rejectedBy.Trim();
                invoice.Approval.RejectedUtc = _clock.UtcNow;
                invoice.Approval.RejectionReason = trimmed;

                AddHistory(invoice, DocumentEventKinds.Rejected, S["Rejected: {0}", trimmed].Value, null);
            }

            return Task.FromResult<Func<IInvoiceObserver, Task>>(null);
        }, cancellationToken);

    /// <inheritdoc/>
    public async Task<InvoiceCommandResult> ReviseAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        Invoice revision = null;

        var result = await RunAsync(invoiceId, async (invoice, result) =>
        {
            if (invoice.Status != InvoiceStatus.Open || invoice.Kind != InvoiceKind.Standard || !string.IsNullOrEmpty(invoice.ReplacedById) || !string.IsNullOrEmpty(invoice.InstallmentPlanId))
            {
                result.Fail(S["Only an open invoice that wasn't replaced already, and is not split into a payment schedule, can be revised."]);

                return null;
            }

            // An invoice that took over a debt it did not post (an order's, a renewal's) is corrected with a credit note:
            // voiding it would cancel what the customer owes for the order.
            if (invoice.SourceType is InvoicingConstants.OrderReferenceType or InvoicingConstants.RenewalReferenceType)
            {
                result.Fail(S["This invoice took over the debt of an order or a renewal, so it is corrected with a credit note rather than revised."]);

                return null;
            }

            // Work billed by the invoice is let go when it is voided, and the revision's lines would not hold it.
            if (invoice.BilledItems.Count > 0)
            {
                result.Fail(S["This invoice bills charges or time kept to bill later, so it is corrected with a credit note rather than revised."]);

                return null;
            }

            if (!await CanBeReplacedAsync(invoice, result, cancellationToken))
            {
                return null;
            }

            var manager = _serviceProvider.GetRequiredService<IInvoiceManager>();

            revision = await manager.NewAsync(cancellationToken: cancellationToken);
            revision.RevisionOfId = invoice.ItemId;
            revision.BillTo = invoice.BillTo;
            revision.Currency = invoice.Currency;
            revision.Culture = invoice.Culture;
            revision.PricesIncludeTax = invoice.PricesIncludeTax;
            revision.PaymentTermId = invoice.PaymentTermId;
            revision.PurchaseOrderNumber = invoice.PurchaseOrderNumber;
            revision.Reference = invoice.Reference;
            revision.SourceType = invoice.SourceType;
            revision.SourceId = invoice.SourceId;
            revision.ServiceStart = invoice.ServiceStart;
            revision.ServiceEnd = invoice.ServiceEnd;
            revision.Notes = invoice.Notes;
            revision.Terms = invoice.Terms;
            revision.Footer = invoice.Footer;
            revision.TemplateId = invoice.TemplateId;
            revision.AllowPartialPayment = invoice.AllowPartialPayment;
            revision.AutoCharge = invoice.AutoCharge;
            revision.LateFeesExempt = invoice.LateFeesExempt;
            revision.Schedule = [.. invoice.Schedule.Select(part => new InvoiceInstallment { DueDate = part.DueDate, Amount = part.Amount, Percent = part.Percent })];
            revision.Charges = [.. invoice.Charges];
            revision.Lines = [.. invoice.Lines.Select(line => new DocumentLine
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
                Taxes = [.. line.Taxes ?? []],
                TaxTreatment = line.TaxTreatment,
                TaxTreatmentReason = line.TaxTreatmentReason,
                ServiceStart = line.ServiceStart,
                ServiceEnd = line.ServiceEnd,
                SourceType = line.SourceType,
                SourceId = line.SourceId,
            })];

            await InvoiceCalculation.ApplyAsync(_calculator, revision, await GetSettingsAsync(), issueDate: null, cancellationToken);
            await manager.CreateAsync(revision, cancellationToken);

            AddHistory(invoice, DocumentEventKinds.Revised, S["A revision was drafted. Issuing it voids this invoice."].Value, null);

            return null;
        }, cancellationToken);

        if (result.Succeeded && revision is not null)
        {
            result.Invoice = revision;
        }

        return result;
    }

    // An invoice is replaced only while open, with nothing paid on its debt and no credit note citing it.
    private async Task<bool> CanBeReplacedAsync(Invoice invoice, InvoiceCommandResult result, CancellationToken cancellationToken)
    {
        if (invoice.AmountPaid > 0m ||
            !string.IsNullOrEmpty(invoice.TransactionId) && await _transactions.FindByIdAsync(invoice.TransactionId, cancellationToken) is { AmountPaid: > 0m })
        {
            result.Fail(S["Something was paid on invoice {0}, so it can't be replaced. Correct it with a credit note.", invoice.Number]);

            return false;
        }

        if (await _creditNotes.HasIssuedForInvoiceAsync(invoice.ItemId, cancellationToken))
        {
            result.Fail(S["A credit note was issued for invoice {0}, so it can't be replaced.", invoice.Number]);

            return false;
        }

        return true;
    }

    // Issues a draft, or with an approver, an invoice waiting for approval. A revision voids the invoice it replaces.
    private async Task<Func<IInvoiceObserver, Task>> IssueCoreAsync(Invoice invoice, InvoiceCommandResult result, string approvedBy, CancellationToken cancellationToken)
    {
            var expected = approvedBy is null ? InvoiceStatus.Draft : InvoiceStatus.PendingApproval;

            if (invoice.Status != expected)
            {
                if (invoice.IssuedUtc.HasValue)
                {
                    result.AlreadyDone = true;
                }
                else if (invoice.Status == InvoiceStatus.PendingApproval)
                {
                    result.Fail(S["This invoice is waiting for approval. It is issued when it is approved."]);
                }
                else
                {
                    result.Fail(approvedBy is null ? S["Only a draft can be issued."] : S["Only an invoice waiting for approval can be approved."]);
                }

                return null;
            }

            if (approvedBy is not null && string.Equals(invoice.Approval?.SubmittedBy, approvedBy, StringComparison.OrdinalIgnoreCase))
            {
                result.Fail(S["Whoever submitted an invoice for approval can't approve it: someone else must."]);

                return null;
            }

            var settings = await GetSettingsAsync();

            await FillFromProfileAsync(invoice, settings, cancellationToken);

            foreach (var error in InvoiceIssueRules.ValidateForIssue(invoice))
            {
                result.Fail(InvoiceIssueMessages.Describe(error, invoice, S));
            }

            if (!result.Succeeded)
            {
                return null;
            }

            // The date chosen on the draft is kept; without one, the invoice is dated today in the site's time zone. It is
            // checked before anything is numbered, so a refused date takes no number.
            var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
            var issueDate = invoice.IssueDate ?? today;

            if (issueDate > today && !settings.AllowFutureIssueDate)
            {
                result.Fail(S["The invoice can't be dated after today ({0:d}).", issueDate.ToDateTime(TimeOnly.MinValue)]);

                return null;
            }

            if (settings.ChronologicalIssueDates &&
                await _store.FindLastIssuedAsync(cancellationToken) is { IssueDate: { } lastDate } last &&
                issueDate < lastDate)
            {
                result.Fail(S["Invoice {0} was dated {1:d}, and invoices are dated in the order they are numbered, so this one can't be dated earlier.", last.Number, lastDate.ToDateTime(TimeOnly.MinValue)]);

                return null;
            }

            invoice.IssueDate = issueDate;
            invoice.Currency = invoice.Currency.Trim().ToUpperInvariant();

            await ApplyExchangeRateAsync(invoice, issueDate, cancellationToken);

            // The final calculation: what is issued is exactly what the calculator says now.
            await InvoiceCalculation.ApplyAsync(_calculator, invoice, settings, issueDate, cancellationToken);

            foreach (var error in InvoiceIssueRules.ValidateTotals(invoice))
            {
                result.Fail(InvoiceIssueMessages.Describe(error, invoice, S));
            }

            if (!result.Succeeded)
            {
                return null;
            }

            // An invoice at or above the approval amount is issued only by its approver.
            if (approvedBy is null && InvoiceApprovalRules.Requires(invoice, settings))
            {
                result.NeedsApproval = true;
                result.Fail(S["This invoice needs approval before it is issued: submit it for approval."]);

                return null;
            }

            // A payment schedule must add up and fall due from the issue date on; it is split on the ledger once issued.
            if (invoice.Schedule is { Count: > 0 })
            {
                if (_serviceProvider.GetService<CrestApps.OrchardCore.Subscriptions.Services.IInstallmentPlanService>() is null)
                {
                    result.Fail(S["A payment schedule needs the Installment Plans feature. Enable it, or take the schedule off."]);

                    return null;
                }

                if (!string.IsNullOrEmpty(invoice.TransactionId))
                {
                    result.Fail(S["An invoice that takes over a debt already on the ledger can't have a payment schedule."]);

                    return null;
                }

                foreach (var error in InvoiceSchedule.Resolve(invoice, issueDate, S).Errors)
                {
                    result.Fail(error);
                }

                if (!result.Succeeded)
                {
                    return null;
                }
            }

            // A revision replaces the invoice it revises, which must still be replaceable.
            Invoice replaced = null;

            if (!string.IsNullOrEmpty(invoice.RevisionOfId))
            {
                replaced = await _store.FindByIdAsync(invoice.RevisionOfId, cancellationToken);

                if (replaced is null || replaced.Status != InvoiceStatus.Open || !string.IsNullOrEmpty(replaced.ReplacedById))
                {
                    result.Fail(S["The invoice this revises is no longer open, or was replaced already. Delete this revision, or remove what it revises."]);

                    return null;
                }

                if (!await CanBeReplacedAsync(replaced, result, cancellationToken))
                {
                    return null;
                }
            }

            await SetDueDateAsync(invoice, settings, issueDate, cancellationToken);

            // A scheduled invoice is due in full when its last part is.
            if (invoice.Schedule is { Count: > 0 } schedule)
            {
                invoice.DueDate = schedule[^1].DueDate;
            }

            invoice.Issuer ??= await GetIssuerAsync();
            invoice.Notes ??= settings.DefaultNotes;
            invoice.Terms ??= settings.DefaultTerms;
            invoice.Footer ??= settings.DefaultFooter;

            // Numbered in this unit of work: a failure below saves nothing, so no number is used up. A number an imported
            // invoice already holds is passed over.
            var number = await UnusedDocumentNumbers.NextAsync(
                _numbers,
                FinancialDocumentKind.Invoice,
                async candidate => await _store.FindByNumberAsync(candidate, cancellationToken) is not null,
                cancellationToken);

            if (number is null)
            {
                result.Fail(S["The invoice number series keeps giving numbers other invoices already hold. Change its format under Settings > Business documents > Number series."]);

                return null;
            }

            invoice.Number = number.PublicToken;

            var now = _clock.UtcNow;

            invoice.Status = InvoiceStatus.Open;
            invoice.IssuedUtc = now;
            invoice.AmountPaid = 0m;
            invoice.AmountCredited = 0m;
            invoice.AmountDue = invoice.Totals.Total;

            if (!string.IsNullOrEmpty(invoice.TransactionId))
            {
                // A debt already on the ledger, such as an order paid on account: the invoice takes it over rather than
                // posting a second one.
                if (!await AdoptTransactionAsync(invoice, now, result, cancellationToken))
                {
                    return null;
                }
            }
            else if (invoice.Totals.Total > 0m)
            {
                invoice.TransactionId = await PostTransactionAsync(invoice, now, cancellationToken);
                invoice.Settlement = InvoiceSettlement.Unpaid;
            }
            else
            {
                // Nothing is owed, so nothing is posted and the invoice is paid at once.
                invoice.Settlement = InvoiceSettlement.Paid;
            }

            if (approvedBy is not null)
            {
                invoice.Approval ??= new InvoiceApproval();
                invoice.Approval.ApprovedBy = approvedBy;
                invoice.Approval.ApprovedUtc = now;

                AddHistory(invoice, DocumentEventKinds.Approved, S["Approved by {0}.", approvedBy].Value, null);
            }

            if (replaced is not null)
            {
                // The replaced invoice's debt is canceled under the ledger's lock: a payment made a moment ago refuses it.
                if (!string.IsNullOrEmpty(replaced.TransactionId))
                {
                    var canceled = await _transactionCommands.CancelUnpaidAsync(replaced.TransactionId, S["Invoice {0} was replaced by {1}.", replaced.Number, invoice.Number].Value, cancellationToken);

                    if (!canceled.Succeeded)
                    {
                        foreach (var error in canceled.Errors)
                        {
                            result.Fail(error);
                        }

                        return null;
                    }
                }

                replaced.Status = InvoiceStatus.Void;
                replaced.VoidedUtc = now;
                replaced.VoidReason = S["Replaced by {0}.", invoice.Number].Value;
                replaced.ReplacedById = invoice.ItemId;
                replaced.AmountDue = 0m;
                replaced.ModifiedUtc = now;

                AddHistory(replaced, DocumentEventKinds.Voided, replaced.VoidReason, null);
                await _store.UpdateAsync(replaced, cancellationToken);
            }

            invoice.ContentHash = InvoiceContentHash.Compute(invoice);

            AddHistory(invoice, DocumentEventKinds.Issued, replaced is null ? S["Issued as {0}.", invoice.Number].Value : S["Issued as {0}, replacing {1}.", invoice.Number, replaced.Number].Value, invoice.Totals.Total);

            return replaced is null
                ? observer => observer.IssuedAsync(invoice, cancellationToken)
                : async observer =>
                {
                    await observer.IssuedAsync(invoice, cancellationToken);
                    await observer.VoidedAsync(replaced, cancellationToken);
                };
    }

    /// <inheritdoc/>
    public Task<InvoiceCommandResult> VoidAsync(string invoiceId, string reason, CancellationToken cancellationToken = default)
        => RunAsync(invoiceId, async (invoice, result) =>
        {
            if (invoice.Status == InvoiceStatus.Void)
            {
                result.AlreadyDone = true;

                return null;
            }

            var trimmedReason = reason?.Trim();

            if (string.IsNullOrEmpty(trimmedReason))
            {
                result.Fail(S["Say why the invoice is voided."]);

                return null;
            }

            if (!InvoiceLifecycle.CanMove(invoice.Status, InvoiceStatus.Void))
            {
                result.Fail(S["Only an open invoice can be voided."]);

                return null;
            }

            if (!string.IsNullOrEmpty(invoice.InstallmentPlanId))
            {
                if (invoice.AmountPaid > 0m)
                {
                    result.Fail(S["A payment was made on this invoice's schedule, so it can't be voided."]);

                    return null;
                }

                var plans = _serviceProvider.GetService<CrestApps.OrchardCore.Subscriptions.Services.IInstallmentPlanService>();
                var canceledPlan = plans is null ? null : await plans.CancelAsync(invoice.InstallmentPlanId, S["Invoice {0} was voided: {1}", invoice.Number, trimmedReason].Value, cancellationToken);

                if (canceledPlan is not { Succeeded: true })
                {
                    result.Fail(S["The invoice's payment schedule could not be canceled: {0}", string.Join(" ", canceledPlan?.Errors.Select(error => error.Value) ?? [])]);

                    return null;
                }

                invoice.Status = InvoiceStatus.Void;
                invoice.VoidedUtc = _clock.UtcNow;
                invoice.VoidReason = trimmedReason;
                invoice.AmountDue = 0m;

                AddHistory(invoice, DocumentEventKinds.Voided, S["Voided: {0}", trimmedReason].Value, null);

                return observer => observer.VoidedAsync(invoice, cancellationToken);
            }

            // A credit note is a tax document that cites this invoice, so the invoice stays and is corrected by it.
            if (await _creditNotes.HasIssuedForInvoiceAsync(invoice.ItemId, cancellationToken))
            {
                result.Fail(S["A credit note was issued for this invoice, so it can't be voided. Issue a credit note for the rest instead."]);

                return null;
            }

            if (!string.IsNullOrEmpty(invoice.TransactionId))
            {
                // Decided by the ledger under the transaction's lock: a payment recorded a moment ago refuses the void.
                var canceled = await _transactionCommands.CancelUnpaidAsync(invoice.TransactionId, S["Invoice {0} was voided: {1}", invoice.Number, trimmedReason].Value, cancellationToken);

                if (!canceled.Succeeded)
                {
                    foreach (var error in canceled.Errors)
                    {
                        result.Fail(error);
                    }

                    return null;
                }
            }

            invoice.Status = InvoiceStatus.Void;
            invoice.VoidedUtc = _clock.UtcNow;
            invoice.VoidReason = trimmedReason;
            invoice.AmountDue = 0m;

            AddHistory(invoice, DocumentEventKinds.Voided, S["Voided: {0}", trimmedReason].Value, null);

            return observer => observer.VoidedAsync(invoice, cancellationToken);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<InvoiceCommandResult> WriteOffAsync(string invoiceId, string reason, CancellationToken cancellationToken = default)
        => RunAsync(invoiceId, async (invoice, result) =>
        {
            if (invoice.Status == InvoiceStatus.Uncollectible)
            {
                result.AlreadyDone = true;

                return null;
            }

            if (!string.IsNullOrEmpty(invoice.InstallmentPlanId))
            {
                result.Fail(S["This invoice is split into a payment schedule: write off or cancel its payments on the ledger instead."]);

                return null;
            }

            var trimmedReason = reason?.Trim();

            if (string.IsNullOrEmpty(trimmedReason))
            {
                result.Fail(S["Say why the invoice is written off."]);

                return null;
            }

            if (!InvoiceLifecycle.CanMove(invoice.Status, InvoiceStatus.Uncollectible) || string.IsNullOrEmpty(invoice.TransactionId))
            {
                result.Fail(S["Only an open invoice with something still owed can be written off."]);

                return null;
            }

            // What is still owed comes from the ledger, never from the amounts mirrored here. The adjustment refuses more than
            // the transaction still owes under its own lock, so a payment recorded meanwhile is never written off.
            var transaction = await _transactions.FindByIdAsync(invoice.TransactionId, cancellationToken);

            if (transaction is null || transaction.OutstandingAmount <= 0m)
            {
                result.Fail(S["Nothing is owed on this invoice any more, so there is nothing to write off."]);

                return null;
            }

            var amount = transaction.OutstandingAmount;
            var adjusted = await _transactionCommands.AdjustAsync(invoice.TransactionId, new TransactionAdjustmentRequest
            {
                Amount = amount,
                Kind = TransactionAdjustmentKind.WriteOff,
                Reason = trimmedReason,
                DocumentKind = InvoicingConstants.InvoiceReferenceType,
                DocumentId = invoice.ItemId,
                DocumentNumber = invoice.Number,
            }, cancellationToken);

            if (!adjusted.Succeeded)
            {
                foreach (var error in adjusted.Errors)
                {
                    result.Fail(error);
                }

                return null;
            }

            invoice.Status = InvoiceStatus.Uncollectible;
            invoice.AmountDue = adjusted.Transaction?.OutstandingAmount ?? 0m;

            AddHistory(invoice, DocumentEventKinds.WrittenOff, S["Written off: {0}", trimmedReason].Value, amount);

            return observer => observer.WrittenOffAsync(invoice, cancellationToken);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<InvoiceCommandResult> ReinstateAsync(string invoiceId, string reason, CancellationToken cancellationToken = default)
        => RunAsync(invoiceId, async (invoice, result) =>
        {
            if (invoice.Status == InvoiceStatus.Open)
            {
                result.AlreadyDone = true;

                return null;
            }

            var trimmedReason = reason?.Trim();

            if (string.IsNullOrEmpty(trimmedReason))
            {
                result.Fail(S["Say why the invoice is reinstated."]);

                return null;
            }

            if (invoice.Status != InvoiceStatus.Uncollectible || string.IsNullOrEmpty(invoice.TransactionId))
            {
                result.Fail(S["Only an invoice that was written off can be reinstated."]);

                return null;
            }

            var transaction = await _transactions.FindByIdAsync(invoice.TransactionId, cancellationToken);
            var writeOff = transaction?.Events.LastOrDefault(entry =>
                entry.Type == TransactionEventType.Adjusted &&
                entry.AdjustmentKind == TransactionAdjustmentKind.WriteOff &&
                string.Equals(entry.DocumentId, invoice.ItemId, StringComparison.Ordinal) &&
                !transaction.Events.Any(other => string.Equals(other.ReversesEventId, entry.Id, StringComparison.Ordinal)));

            if (writeOff is not null)
            {
                var reversed = await _transactionCommands.ReverseAsync(invoice.TransactionId, writeOff.Id, trimmedReason, cancellationToken);

                if (!reversed.Succeeded)
                {
                    foreach (var error in reversed.Errors)
                    {
                        result.Fail(error);
                    }

                    return null;
                }

                invoice.AmountDue = reversed.Transaction?.OutstandingAmount ?? invoice.AmountDue;
            }

            invoice.Status = InvoiceStatus.Open;

            AddHistory(invoice, DocumentEventKinds.Note, S["Reinstated: {0}", trimmedReason].Value, null);

            return observer => observer.ReinstatedAsync(invoice, cancellationToken);
        }, cancellationToken);

    // Runs one command under the invoice's lock: loads it fresh, lets the command change it, saves it with whatever the
    // command added to the session (its number, its transaction), and tells the observers after the save.
    private async Task<InvoiceCommandResult> RunAsync(
        string invoiceId,
        Func<Invoice, InvoiceCommandResult, Task<Func<IInvoiceObserver, Task>>> command,
        CancellationToken cancellationToken)
    {
        var result = new InvoiceCommandResult();

        if (string.IsNullOrEmpty(invoiceId))
        {
            return result.Fail(S["The invoice could not be found."]);
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockPrefix + invoiceId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return result.Fail(S["This invoice is being changed by someone else. Try again in a moment."]);
        }

        await using var _ = locker;

        // Ledger work this command causes sees the invoice is busy in this request and doesn't wait for its lock.
        using var held = _serviceProvider.GetRequiredService<InvoiceCommandLocks>().Hold(invoiceId);

        var invoice = await _store.FindByIdAsync(invoiceId, cancellationToken);

        if (invoice is null)
        {
            return result.Fail(S["The invoice could not be found."]);
        }

        result.Invoice = invoice;

        Func<IInvoiceObserver, Task> notify;

        try
        {
            notify = await command(invoice, result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nothing the command left in the session is saved, such as a number it took: the next issue gets that number.
            await _session.CancelAsync();

            throw;
        }

        if (result.AlreadyDone)
        {
            return result;
        }

        if (!result.Succeeded)
        {
            await _session.CancelAsync();

            return result;
        }

        invoice.ModifiedUtc = _clock.UtcNow;

        try
        {
            await _store.UpdateAsync(invoice, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            await _session.CancelAsync();
            _logger.LogWarning("A concurrency conflict prevented a command on invoice '{InvoiceId}'; another writer updated it first.", invoice.ItemId);

            return result.Fail(S["This invoice was changed by someone else while you were working on it. Reload it and try again."]);
        }

        if (notify is not null)
        {
            await _serviceProvider.GetRequiredService<InvoiceObserverNotifier>().NotifyAsync(invoice, notify);
        }

        return result;
    }

    // The bill-to's billing profile fills what the draft leaves empty: who they are, their contact, tax id, currency and
    // term. A user party still gets a name and an e-mail from the user store when no profile source knows them.
    private async Task FillFromProfileAsync(Invoice invoice, InvoicingSettings settings, CancellationToken cancellationToken)
    {
        invoice.BillTo ??= new DocumentParty();

        var party = invoice.BillTo.Party;

        if (!party.IsEmpty && _serviceProvider.GetService<IPartyBillingProfileService>() is { } profiles &&
            await profiles.GetAsync(party, cancellationToken) is { } profile)
        {
            invoice.BillTo.Contact = Merge(invoice.BillTo.Contact, profile.Contact);
            invoice.BillTo.ShippingAddress ??= profile.ShippingAddress;
            invoice.BillTo.TaxId ??= profile.TaxId;

            if (profile.TaxExempt && invoice.BillTo.TaxTreatment == TaxTreatment.Taxable)
            {
                invoice.BillTo.TaxTreatment = TaxTreatment.Exempt;
            }

            foreach (var copy in profile.CopyTo ?? [])
            {
                if (!invoice.BillTo.CopyTo.Contains(copy, StringComparer.OrdinalIgnoreCase))
                {
                    invoice.BillTo.CopyTo.Add(copy);
                }
            }

            invoice.Currency ??= profile.Currency;
            invoice.Culture ??= profile.Culture;
            invoice.PaymentTermId ??= profile.PaymentTermId;
        }

        if (string.Equals(party.Kind, PartyKinds.User, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(invoice.BillTo.Contact?.Name) || string.IsNullOrWhiteSpace(invoice.BillTo.Contact?.Email)) &&
            _serviceProvider.GetService<ICustomerContactResolver>() is { } contacts &&
            await contacts.ResolveAsync(CustomerOwner.ForUser(party.Id), null, cancellationToken) is { } contact)
        {
            invoice.BillTo.Contact = Merge(invoice.BillTo.Contact, ContactInfo.From(contact.DisplayName, contact.Email, null));
        }

        if (!party.IsEmpty && string.IsNullOrWhiteSpace(invoice.BillTo.Contact?.Name) &&
            _serviceProvider.GetService<IPartyDirectory>() is { } directory &&
            await directory.GetNameAsync(party, cancellationToken) is { Length: > 0 } name)
        {
            invoice.BillTo.Contact = Merge(invoice.BillTo.Contact, ContactInfo.From(name, null, null));
        }

        invoice.Currency ??= settings.DefaultCurrency;
        invoice.PaymentTermId ??= settings.DefaultPaymentTermId;
    }

    // The term sets the due date from the issue date; a date typed on the draft wins, and with neither it is due on issue.
    private async Task SetDueDateAsync(Invoice invoice, InvoicingSettings settings, DateOnly issueDate, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(invoice.PaymentTermId) &&
            _serviceProvider.GetService<IPaymentTermManager>() is { } terms &&
            _serviceProvider.GetService<IPaymentTermCalculator>() is { } calculator &&
            await terms.FindByIdAsync(invoice.PaymentTermId, cancellationToken) is { } term)
        {
            invoice.PaymentTermName = term.Name;
            invoice.DueDate ??= calculator.GetDueDate(term, issueDate);
        }

        invoice.DueDate ??= issueDate;
        invoice.AllowPartialPayment = invoice.AllowPartialPayment || settings.AllowPartialPayment;
    }

    private async Task<DocumentIssuer> GetIssuerAsync()
    {
        var profile = await _siteService.GetStoreProfileAsync();

        return new DocumentIssuer
        {
            Name = profile.Name,
            Address = profile.Address,
            TaxId = profile.TaxId,
            Email = profile.Email,
            Phone = profile.Phone,
            Website = profile.Website,
            LogoUrl = profile.LogoUrl,
        };
    }

    private async Task<InvoicingSettings> GetSettingsAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();

        return site.GetOrCreate<InvoicingSettings>() ?? new InvoicingSettings();
    }

    // Posts what the invoice says is owed: the payer is the bill-to, a user owns it, and anyone else is reached as a guest
    // at the bill-to's e-mail. An earlier attempt that already posted it is reused, so nothing is posted twice.
    // Takes over a transaction already on the ledger for the invoice: it must be in the invoice's currency, owe what the
    // invoice comes to (to the cent), still be open, and not belong to another invoice. Its timeline names the invoice.
    // With Exchange Rates on and a reporting currency set, records the rate of the issue day to it (1 in that currency).
    // No rate known leaves the invoice without one, and reports leave it out of their totals in that currency.
    private async Task ApplyExchangeRateAsync(Invoice invoice, DateOnly issueDate, CancellationToken cancellationToken)
    {
        if (_serviceProvider.GetService<IExchangeRateService>() is not { } rates ||
            await rates.GetBaseCurrencyAsync(cancellationToken) is not { Length: > 0 } baseCurrency)
        {
            return;
        }

        var quote = await rates.GetRateAsync(invoice.Currency, baseCurrency, issueDate, cancellationToken);

        invoice.BaseCurrency = quote is null ? null : baseCurrency;
        invoice.ExchangeRate = quote?.Rate;

        if (quote is null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("No {Currency} to {BaseCurrency} rate was known on {IssueDate}, so the invoice was issued without one.", invoice.Currency, baseCurrency, issueDate);
        }
    }

    private async Task<bool> AdoptTransactionAsync(Invoice invoice, DateTime now, InvoiceCommandResult result, CancellationToken cancellationToken)
    {
        var transaction = await _transactions.FindByIdAsync(invoice.TransactionId, cancellationToken);

        if (transaction is null)
        {
            result.Fail(S["The ledger entry this invoice takes over could not be found."]);

            return false;
        }

        if (!string.Equals(transaction.Currency, invoice.Currency, StringComparison.OrdinalIgnoreCase) ||
            Math.Abs(transaction.TotalAmount - invoice.Totals.Total) > 0.01m)
        {
            result.Fail(S["The invoice comes to {0} {1}, but the ledger entry it takes over is {2} {3}. Change the lines to match before issuing it.", invoice.Totals.Total.ToString("0.00", CultureInfo.CurrentCulture), invoice.Currency, transaction.TotalAmount.ToString("0.00", CultureInfo.CurrentCulture), transaction.Currency]);

            return false;
        }

        if (transaction.Status is TransactionStatus.Canceled or TransactionStatus.Abandoned ||
            await _store.FindByTransactionIdAsync(transaction.ItemId, cancellationToken) is { } other && !string.Equals(other.ItemId, invoice.ItemId, StringComparison.Ordinal))
        {
            result.Fail(S["The ledger entry this invoice would take over is closed or belongs to another invoice."]);

            return false;
        }

        transaction.Events.Add(new TransactionEvent
        {
            Id = IdGenerator.GenerateId(),
            CreatedUtc = now,
            Type = TransactionEventType.Note,
            Message = S["Invoiced as {0}.", invoice.Number].Value,
            DocumentKind = InvoicingConstants.InvoiceReferenceType,
            DocumentId = invoice.ItemId,
            DocumentNumber = invoice.Number,
        });
        transaction.UpdatedUtc = now;

        // The debt reports in the store's currency at the rate the invoice was issued with, unless it had one already.
        if (transaction.ExchangeRate is null && invoice.ExchangeRate is not null)
        {
            transaction.ExchangeRate = invoice.ExchangeRate;
            transaction.BaseCurrency = invoice.BaseCurrency;
        }

        await _transactions.UpdateAsync(transaction, data: null, cancellationToken);

        invoice.AmountPaid = transaction.AmountPaid;
        invoice.AmountDue = transaction.OutstandingAmount;
        invoice.Settlement = transaction.OutstandingAmount <= 0m
            ? InvoiceSettlement.Paid
            : transaction.AmountPaid > 0m ? InvoiceSettlement.PartiallyPaid : InvoiceSettlement.Unpaid;

        return true;
    }

    private async Task<string> PostTransactionAsync(Invoice invoice, DateTime now, CancellationToken cancellationToken)
    {
        var existing = await _transactions.PageAsync(1, 1, new TransactionQuery
        {
            ReferenceType = InvoicingConstants.InvoiceReferenceType,
            ReferenceId = invoice.ItemId,
        }, cancellationToken);

        if (existing.Entries.FirstOrDefault() is { } posted)
        {
            return posted.ItemId;
        }

        var transaction = await _transactions.NewAsync(cancellationToken: cancellationToken);
        var party = invoice.BillTo.Party;

        transaction.Title = S["Invoice {0}", invoice.Number].Value;
        transaction.Source = TransactionSource;

        if (string.Equals(party.Kind, PartyKinds.User, StringComparison.OrdinalIgnoreCase))
        {
            transaction.OwnerId = party.Id;
            transaction.OwnerKind = CustomerOwnerKind.Authenticated;
        }
        else
        {
            transaction.OwnerId = invoice.ItemId;
            transaction.OwnerKind = CustomerOwnerKind.Guest;
            transaction.GuestContactName = invoice.BillTo.Contact?.Name;
            transaction.GuestContactEmail = invoice.BillTo.Contact?.Email;
        }

        if (!party.IsEmpty)
        {
            transaction.SetPayer(party);
        }

        transaction.PayerContact = invoice.BillTo.Contact;
        transaction.ReferenceType = InvoicingConstants.InvoiceReferenceType;
        transaction.ReferenceId = invoice.ItemId;
        transaction.InvoiceNumber = invoice.Number;
        transaction.Currency = invoice.Currency;
        transaction.ExchangeRate = invoice.ExchangeRate;
        transaction.BaseCurrency = invoice.BaseCurrency;
        transaction.Amount = invoice.Totals.Total - invoice.Totals.TaxTotal;
        transaction.TaxAmount = invoice.Totals.TaxTotal;
        transaction.TotalAmount = invoice.Totals.Total;
        transaction.AmountPaid = 0m;
        transaction.AllowPartialPayment = invoice.AllowPartialPayment;
        transaction.Status = TransactionStatus.Outstanding;
        transaction.CreatedUtc = now;
        transaction.UpdatedUtc = now;
        transaction.TaxLines = [.. invoice.Taxes.Select(tax => new TaxLine
        {
            TaxCode = tax.Code,
            TaxName = tax.Name,
            // A document keeps its rate in percent; the ledger keeps it as a fraction, as Taxation does.
            Rate = tax.Rate / 100m,
            TaxableAmount = tax.TaxableAmount,
            TaxAmount = tax.TaxAmount,
            Treatment = tax.Treatment,
            TreatmentReason = tax.ExemptionReason,
            IsCompound = tax.IsCompound,
            IncludedInPrice = invoice.PricesIncludeTax,
        })];

        // Due at the end of the due date in the site's time zone.
        if (invoice.DueDate is DateOnly due)
        {
            transaction.DueUtc = await _localClock.ToUtcLenientAsync(due.ToDateTime(new TimeOnly(23, 59, 59)));
        }

        transaction.Events.Add(new TransactionEvent
        {
            Id = IdGenerator.GenerateId(),
            CreatedUtc = now,
            Type = TransactionEventType.Created,
            Message = S["Posted when invoice {0} was issued.", invoice.Number].Value,
            DocumentKind = InvoicingConstants.InvoiceReferenceType,
            DocumentId = invoice.ItemId,
            DocumentNumber = invoice.Number,
        });

        await _transactions.CreateAsync(transaction, cancellationToken);

        return transaction.ItemId;
    }

    private void AddHistory(Invoice invoice, string kind, string message, decimal? amount)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        invoice.History.Add(new DocumentEvent
        {
            Id = IdGenerator.GenerateId(),
            Kind = kind,
            CreatedUtc = _clock.UtcNow,
            ActorId = user?.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorName = user?.Identity?.Name,
            Message = message,
            Amount = amount,
        });
    }

    // Keeps what the document already says, filling only what it leaves empty.
    private static ContactInfo Merge(ContactInfo current, ContactInfo fallback)
    {
        if (fallback is null)
        {
            return current;
        }

        if (current is null)
        {
            return fallback;
        }

        return new ContactInfo
        {
            Name = string.IsNullOrWhiteSpace(current.Name) ? fallback.Name : current.Name,
            Email = string.IsNullOrWhiteSpace(current.Email) ? fallback.Email : current.Email,
            Phone = current.Phone.HasValue ? current.Phone : fallback.Phone,
            Address = current.Address ?? fallback.Address,
        };
    }
}
