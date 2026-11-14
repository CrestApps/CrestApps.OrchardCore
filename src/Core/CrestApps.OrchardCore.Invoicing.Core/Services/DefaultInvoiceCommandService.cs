using System.Security.Claims;
using OrchardCore;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Commerce.Stores;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Addresses.Models;
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
        => RunAsync(invoiceId, async (invoice, result) =>
        {
            if (invoice.Status != InvoiceStatus.Draft)
            {
                if (invoice.IssuedUtc.HasValue)
                {
                    result.AlreadyDone = true;
                }
                else
                {
                    result.Fail(S["Only a draft can be issued."]);
                }

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

            await SetDueDateAsync(invoice, settings, issueDate, cancellationToken);

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

            if (invoice.Totals.Total > 0m)
            {
                invoice.TransactionId = await PostTransactionAsync(invoice, now, cancellationToken);
                invoice.Settlement = InvoiceSettlement.Unpaid;
            }
            else
            {
                // Nothing is owed, so nothing is posted and the invoice is paid at once.
                invoice.Settlement = InvoiceSettlement.Paid;
            }

            invoice.ContentHash = InvoiceContentHash.Compute(invoice);

            AddHistory(invoice, DocumentEventKinds.Issued, S["Issued as {0}.", invoice.Number].Value, invoice.Totals.Total);

            return observer => observer.IssuedAsync(invoice, cancellationToken);
        }, cancellationToken);

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
