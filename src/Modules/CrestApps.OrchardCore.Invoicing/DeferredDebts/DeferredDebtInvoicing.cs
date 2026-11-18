using System.Globalization;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.Settings;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.DeferredDebts;

/// <summary>
/// A debt a customer was let to pay later gets an invoice, as billing systems send one for every charge collected by
/// invoice: the debt a pay-later checkout method (Pay Later, account terms) writes on the ledger for a purchase that is
/// not an order (a subscription's first cycle, a membership), and the debt of each renewal cycle such a method writes when
/// a period ends. The invoice takes the debt over, numbered and printed like any invoice, instead of a second debt. An
/// order's debt gets the order's own invoice (<c>OrderInvoiceBuilder</c>).
/// </summary>
public sealed class DeferredDebtInvoicingStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<DeferredDebtInvoiceBuilder>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITransactionEventHandler, DeferredDebtInvoiceHandler>());
    }
}

/// <summary>
/// Hears a deferred debt created on the ledger and invoices it once the request has saved it.
/// </summary>
internal sealed class DeferredDebtInvoiceHandler : ITransactionEventHandler
{
    private readonly IServiceProvider _serviceProvider;

    public DeferredDebtInvoiceHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task CreatedAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        if (DeferredDebtInvoiceBuilder.GetSourceType(transaction, _serviceProvider.GetService<ICheckoutPaymentProviderResolver>()) is null)
        {
            return Task.CompletedTask;
        }

        var transactionId = transaction.ItemId;

        if (ShellScope.Current is null)
        {
            return _serviceProvider.GetRequiredService<DeferredDebtInvoiceBuilder>().InvoiceAsync(transactionId, cancellationToken);
        }

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<DeferredDebtInvoiceBuilder>().InvoiceAsync(transactionId, CancellationToken.None));

        return Task.CompletedTask;
    }

    public Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task CanceledAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task BecameOverdueAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// Makes the invoice of a deferred debt: one service line named after what was bought, over the period it covers when it
/// bills a cycle, billed to whoever owes the debt and issued taking the debt over. When the invoice does not come to what
/// the debt owes (tax worked out differently), it is left as a draft for staff to correct and issue.
/// </summary>
public sealed class DeferredDebtInvoiceBuilder
{
    private readonly ITransactionManager _transactions;
    private readonly IInvoiceManager _invoices;
    private readonly IDocumentCalculator _calculator;
    private readonly ISiteService _siteService;
    private readonly ILocalClock _localClock;
    private readonly ISession _session;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public DeferredDebtInvoiceBuilder(
        ITransactionManager transactions,
        IInvoiceManager invoices,
        IDocumentCalculator calculator,
        ISiteService siteService,
        ILocalClock localClock,
        ISession session,
        IServiceProvider serviceProvider,
        ILogger<DeferredDebtInvoiceBuilder> logger,
        IStringLocalizer<DeferredDebtInvoiceBuilder> stringLocalizer)
    {
        _transactions = transactions;
        _invoices = invoices;
        _calculator = calculator;
        _siteService = siteService;
        _localClock = localClock;
        _session = session;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Whether a ledger transaction is the debt of a renewal: a cycle after the first of a recurring commitment, still
    /// owed. The first cycle is billed with what was bought.
    /// </summary>
    public static bool IsRenewal(Transaction transaction)
        => transaction?.Recurrence is { CycleNumber: > 1 } &&
            transaction.Status == TransactionStatus.Outstanding &&
            transaction.TotalAmount > 0m;

    /// <summary>
    /// Whether a ledger transaction is what a checkout left owing for a purchase that is not an order: still owed, written
    /// by a payment method that defers payment (Pay Later, account terms), for the first cycle when it recurs.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="providers">The checkout's payment methods, or <see langword="null"/> when Checkout is off.</param>
    public static bool IsCheckoutDebt(Transaction transaction, ICheckoutPaymentProviderResolver providers)
        => transaction is { Status: TransactionStatus.Outstanding, TotalAmount: > 0m } &&
            transaction.Recurrence is null or { CycleNumber: <= 1 } &&
            !string.IsNullOrEmpty(transaction.CheckoutSessionId) &&

            // An order's debt gets the order's invoice; an invoice's own debt and a settlement are invoiced already.
            transaction.ReferenceType is not (CheckoutReferenceTypes.Order or InvoicingConstants.InvoiceReferenceType or TransactionsConstants.ReferenceTypes.Transaction) &&
            !string.IsNullOrEmpty(transaction.Source) &&
            providers?.GetProvider(transaction.Source)?.Capabilities?.DefersPayment == true;

    /// <summary>
    /// Returns the source type of the invoice a transaction gets, <see cref="InvoicingConstants.RenewalReferenceType"/> or
    /// <see cref="InvoicingConstants.CheckoutDebtReferenceType"/>, or <see langword="null"/> when it gets none from here.
    /// </summary>
    public static string GetSourceType(Transaction transaction, ICheckoutPaymentProviderResolver providers)
        => IsRenewal(transaction)
            ? InvoicingConstants.RenewalReferenceType
            : IsCheckoutDebt(transaction, providers) ? InvoicingConstants.CheckoutDebtReferenceType : null;

    /// <summary>
    /// Invoices a deferred debt, once. Returns the invoice, issued or left as a draft, or <see langword="null"/> when the
    /// transaction is not a deferred debt.
    /// </summary>
    public async Task<Invoice> InvoiceAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        var transaction = string.IsNullOrEmpty(transactionId) ? null : await _transactions.FindByIdAsync(transactionId, cancellationToken);

        if (GetSourceType(transaction, _serviceProvider.GetService<ICheckoutPaymentProviderResolver>()) is not { } sourceType)
        {
            return null;
        }

        var existing = await _invoices.PageAsync(1, 1, new InvoiceQuery { SourceType = sourceType, SourceId = transaction.ItemId }, cancellationToken);

        if (existing.Entries.FirstOrDefault() is { } made)
        {
            return made;
        }

        var invoice = await BuildDraftAsync(transaction, sourceType, cancellationToken);
        var validation = await _invoices.ValidateAsync(invoice, cancellationToken);

        if (!validation.Succeeded)
        {
            _logger.LogWarning("The invoice of the debt '{TransactionId}' could not be made: {Errors}", transaction.ItemId, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));

            return null;
        }

        await _invoices.CreateAsync(invoice, cancellationToken);

        // Issued in a unit of work of its own, which reads the draft under its lock.
        await _session.SaveChangesAsync(cancellationToken);

        if (Math.Abs(invoice.Totals.Total - transaction.TotalAmount) > 0.01m)
        {
            _logger.LogWarning(
                "The invoice of the debt '{TransactionId}' was left as a draft: it comes to {InvoiceTotal}, the debt to {DebtTotal}.",
                transaction.ItemId,
                invoice.Totals.Total,
                transaction.TotalAmount);

            return invoice;
        }

        await using var scope = _serviceProvider.CreateAsyncScope();

        var issued = await scope.ServiceProvider.GetRequiredService<IInvoiceCommandService>().IssueAsync(invoice.ItemId, cancellationToken);

        if (!issued.Succeeded)
        {
            _logger.LogWarning("The invoice of the debt '{TransactionId}' was left as a draft: {Errors}", transaction.ItemId, string.Join(" ", issued.Errors));

            return invoice;
        }

        // The customer was told at checkout that an invoice follows, and billing systems send an invoice collected later
        // as soon as it is issued; the reminders then chase it. Without e-mail, or an address, it waits to be sent by hand.
        if (scope.ServiceProvider.GetService<IInvoiceSender>() is { } sender)
        {
            var sent = await sender.SendAsync(new InvoiceSendRequest { InvoiceId = issued.Invoice.ItemId }, cancellationToken);

            if (!sent.Succeeded && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Invoice {InvoiceNumber} of the debt '{TransactionId}' was issued but not sent: {Errors}", issued.Invoice.Number, transaction.ItemId, string.Join(" ", sent.Errors));
            }
        }

        return issued.Invoice;
    }

    private async Task<Invoice> BuildDraftAsync(Transaction transaction, string sourceType, CancellationToken cancellationToken)
    {
        var settings = await _siteService.GetSettingsAsync<InvoicingSettings>();
        var invoice = await _invoices.NewAsync(cancellationToken: cancellationToken);
        var recurrence = transaction.Recurrence;
        var payer = transaction.GetPayer();

        invoice.SourceType = sourceType;
        invoice.SourceId = transaction.ItemId;
        invoice.TransactionId = transaction.ItemId;
        invoice.Currency = transaction.Currency;
        invoice.Reference = transaction.Title;
        invoice.AllowPartialPayment = settings.AllowPartialPayment;
        invoice.BillTo = new DocumentParty
        {
            Party = !payer.IsEmpty ? payer : !string.IsNullOrEmpty(transaction.OwnerId) && transaction.OwnerKind == default ? new PartyRef(PartyKinds.User, transaction.OwnerId) : default,
            Contact = string.IsNullOrEmpty(transaction.GuestContactEmail) && string.IsNullOrEmpty(transaction.GuestContactName)
                ? null
                : new ContactInfo { Name = transaction.GuestContactName, Email = transaction.GuestContactEmail },
        };

        if (transaction.DueUtc is { } dueUtc)
        {
            invoice.DueDate = await ToLocalDateAsync(dueUtc);
        }

        var line = new DocumentLine
        {
            Id = IdGenerator.GenerateId(),
            Kind = DocumentLineKind.Service,
            Name = string.IsNullOrWhiteSpace(transaction.Title)
                ? (recurrence is null ? S["Purchase"].Value : S["Renewal"].Value)
                : transaction.Title,
            Quantity = 1m,
            UnitPrice = transaction.Amount,
            SourceType = sourceType,
            SourceId = transaction.ItemId,
        };

        // A debt that bills a cycle says which period it covers, on the line and on the invoice.
        if (recurrence is not null)
        {
            var periodStart = await ToLocalDateAsync(recurrence.PeriodStartUtc);
            var periodEnd = await ToLocalDateAsync(recurrence.PeriodEndUtc);

            // The period runs up to the moment the next one starts, so its last day is the day before.
            var lastDay = periodEnd > periodStart ? periodEnd.AddDays(-1) : periodStart;

            line.Description = S["Cycle {0}, {1} to {2}", recurrence.CycleNumber, periodStart.ToString("d", CultureInfo.CurrentCulture), lastDay.ToString("d", CultureInfo.CurrentCulture)].Value;
            line.ServiceStart = periodStart;
            line.ServiceEnd = lastDay;
            invoice.ServiceStart = periodStart;
            invoice.ServiceEnd = lastDay;
        }

        invoice.Lines.Add(line);

        await InvoiceCalculation.ApplyAsync(_calculator, invoice, settings, issueDate: null, cancellationToken);

        return invoice;
    }

    private async Task<DateOnly> ToLocalDateAsync(DateTime utc)
        => DateOnly.FromDateTime((await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)))).DateTime);
}
