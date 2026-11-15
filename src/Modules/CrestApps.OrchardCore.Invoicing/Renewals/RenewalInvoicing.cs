using System.Globalization;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
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

namespace CrestApps.OrchardCore.Invoicing.Renewals;

/// <summary>
/// A renewal produces an invoice: the debt of each renewal cycle that an offline provider writes on the ledger (Pay
/// Later, for a subscription no gateway bills) gets an issued invoice that takes the debt over, numbered and printed
/// like any invoice, instead of a second debt.
/// </summary>
public sealed class RenewalInvoicingStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<RenewalInvoiceBuilder>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITransactionEventHandler, RenewalDebtInvoiceHandler>());
    }
}

/// <summary>
/// Hears a renewal cycle's debt created on the ledger and invoices it once the request has saved it.
/// </summary>
internal sealed class RenewalDebtInvoiceHandler : ITransactionEventHandler
{
    private readonly IServiceProvider _serviceProvider;

    public RenewalDebtInvoiceHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task CreatedAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        if (!RenewalInvoiceBuilder.IsRenewal(transaction))
        {
            return Task.CompletedTask;
        }

        var transactionId = transaction.ItemId;

        if (ShellScope.Current is null)
        {
            return _serviceProvider.GetRequiredService<RenewalInvoiceBuilder>().InvoiceAsync(transactionId, cancellationToken);
        }

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<RenewalInvoiceBuilder>().InvoiceAsync(transactionId, CancellationToken.None));

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
/// Makes the invoice of a renewal cycle's debt: one line for the agreement's cycle, over the period it covers, billed to
/// whoever owes the debt and issued taking the debt over. When the invoice does not come to what the debt owes (tax
/// worked out differently), it is left as a draft for staff to correct and issue.
/// </summary>
public sealed class RenewalInvoiceBuilder
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

    public RenewalInvoiceBuilder(
        ITransactionManager transactions,
        IInvoiceManager invoices,
        IDocumentCalculator calculator,
        ISiteService siteService,
        ILocalClock localClock,
        ISession session,
        IServiceProvider serviceProvider,
        ILogger<RenewalInvoiceBuilder> logger,
        IStringLocalizer<RenewalInvoiceBuilder> stringLocalizer)
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
    /// owed. The first cycle is billed with what was bought (an order's invoice, for one).
    /// </summary>
    public static bool IsRenewal(Transaction transaction)
        => transaction?.Recurrence is { CycleNumber: > 1 } &&
            transaction.Status == TransactionStatus.Outstanding &&
            transaction.TotalAmount > 0m;

    /// <summary>
    /// Invoices a renewal's debt, once. Returns the invoice, issued or left as a draft, or <see langword="null"/> when
    /// the transaction is not a renewal's debt.
    /// </summary>
    public async Task<Invoice> InvoiceAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        var transaction = string.IsNullOrEmpty(transactionId) ? null : await _transactions.FindByIdAsync(transactionId, cancellationToken);

        if (!IsRenewal(transaction))
        {
            return null;
        }

        var existing = await _invoices.PageAsync(1, 1, new InvoiceQuery { SourceType = InvoicingConstants.RenewalReferenceType, SourceId = transaction.ItemId }, cancellationToken);

        if (existing.Entries.FirstOrDefault() is { } made)
        {
            return made;
        }

        var invoice = await BuildDraftAsync(transaction, cancellationToken);
        var validation = await _invoices.ValidateAsync(invoice, cancellationToken);

        if (!validation.Succeeded)
        {
            _logger.LogWarning("The invoice of renewal '{TransactionId}' could not be made: {Errors}", transaction.ItemId, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));

            return null;
        }

        await _invoices.CreateAsync(invoice, cancellationToken);

        // Issued in a unit of work of its own, which reads the draft under its lock.
        await _session.SaveChangesAsync(cancellationToken);

        if (Math.Abs(invoice.Totals.Total - transaction.TotalAmount) > 0.01m)
        {
            _logger.LogWarning(
                "The invoice of renewal '{TransactionId}' was left as a draft: it comes to {InvoiceTotal}, the debt to {DebtTotal}.",
                transaction.ItemId,
                invoice.Totals.Total,
                transaction.TotalAmount);

            return invoice;
        }

        await using var scope = _serviceProvider.CreateAsyncScope();

        var issued = await scope.ServiceProvider.GetRequiredService<IInvoiceCommandService>().IssueAsync(invoice.ItemId, cancellationToken);

        if (!issued.Succeeded)
        {
            _logger.LogWarning("The invoice of renewal '{TransactionId}' was left as a draft: {Errors}", transaction.ItemId, string.Join(" ", issued.Errors));

            return invoice;
        }

        return issued.Invoice;
    }

    private async Task<Invoice> BuildDraftAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        var settings = await _siteService.GetSettingsAsync<InvoicingSettings>();
        var invoice = await _invoices.NewAsync(cancellationToken: cancellationToken);
        var recurrence = transaction.Recurrence;
        var payer = transaction.GetPayer();
        var periodStart = await ToLocalDateAsync(recurrence.PeriodStartUtc);
        var periodEnd = await ToLocalDateAsync(recurrence.PeriodEndUtc);

        // The period runs up to the moment the next one starts, so its last day is the day before.
        var lastDay = periodEnd > periodStart ? periodEnd.AddDays(-1) : periodStart;

        invoice.SourceType = InvoicingConstants.RenewalReferenceType;
        invoice.SourceId = transaction.ItemId;
        invoice.TransactionId = transaction.ItemId;
        invoice.Currency = transaction.Currency;
        invoice.Reference = transaction.Title;
        invoice.AllowPartialPayment = settings.AllowPartialPayment;
        invoice.ServiceStart = periodStart;
        invoice.ServiceEnd = lastDay;
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

        invoice.Lines.Add(new DocumentLine
        {
            Id = IdGenerator.GenerateId(),
            Kind = DocumentLineKind.Service,
            Name = string.IsNullOrWhiteSpace(transaction.Title) ? S["Renewal"].Value : transaction.Title,
            Description = S["Cycle {0}, {1} to {2}", recurrence.CycleNumber, periodStart.ToString("d", CultureInfo.CurrentCulture), lastDay.ToString("d", CultureInfo.CurrentCulture)].Value,
            Quantity = 1m,
            UnitPrice = transaction.Amount,
            ServiceStart = periodStart,
            ServiceEnd = lastDay,
            SourceType = InvoicingConstants.RenewalReferenceType,
            SourceId = transaction.ItemId,
        });

        await InvoiceCalculation.ApplyAsync(_calculator, invoice, settings, issueDate: null, cancellationToken);

        return invoice;
    }

    private async Task<DateOnly> ToLocalDateAsync(DateTime utc)
        => DateOnly.FromDateTime((await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)))).DateTime);
}
