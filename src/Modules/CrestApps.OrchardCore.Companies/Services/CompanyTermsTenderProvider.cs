using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Companies.Services;

/// <summary>
/// Pays a company buyer's order on the company's account: the deposit is paid at checkout, and the rest is owed on an
/// invoice due in the company's net days, on the ledger with its reminders. Refunding what went on account takes it off
/// the invoice.
/// </summary>
public sealed class CompanyTermsTenderProvider : ICheckoutTenderProvider
{
    // The obligation an order's account invoice is recorded under, so completing a checkout twice owes once.
    internal const string ObligationId = "company-terms";

    private readonly CompanyTermsService _terms;
    private readonly ICompanyStore _companies;
    private readonly ITransactionManager _transactions;
    private readonly ITransactionCommandService _commands;
    private readonly IOrderStore _orders;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public CompanyTermsTenderProvider(
        CompanyTermsService terms,
        ICompanyStore companies,
        ITransactionManager transactions,
        ITransactionCommandService commands,
        IOrderStore orders,
        IClock clock,
        ILogger<CompanyTermsTenderProvider> logger,
        IStringLocalizer<CompanyTermsTenderProvider> stringLocalizer)
    {
        _terms = terms;
        _companies = companies;
        _transactions = transactions;
        _commands = commands;
        _orders = orders;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    public string Key
        => CompaniesConstants.TermsTenderKey;

    public async Task<IReadOnlyList<TenderLine>> GetTendersAsync(CheckoutTenderContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (company, amount) = await _terms.GetForCheckoutAsync(context.Flow, context.Invoice, cancellationToken);

        if (company is null || amount <= 0m)
        {
            return [];
        }

        // Only the buyer's own company's account pays, for this checkout, whatever the checkout was told.
        var reference = CompanyTermsService.ToReference(company.ItemId, context.Flow.Session.SessionId);

        if (!context.References.Contains(reference, StringComparer.Ordinal))
        {
            return [];
        }

        return
        [
            new TenderLine
            {
                Reference = reference,
                Description = S["On account, net {0}", company.Terms.NetDays],
                Amount = amount,
            },
        ];
    }

    public async Task<decimal?> GetBalanceAsync(string reference, string currency, CancellationToken cancellationToken = default)
    {
        var (companyId, _) = CompanyTermsService.FromReference(reference);

        return await _companies.FindByIdAsync(companyId, cancellationToken) is { IsEnabled: true, Terms.OffersTerms: true } company
            ? await _terms.GetAvailableCreditAsync(company, cancellationToken)
            : null;
    }

    public async Task DebitAsync(string reference, decimal amount, string currency, CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        var (companyId, sessionId) = CompanyTermsService.FromReference(reference);

        if (flow?.Session is not CheckoutSession session ||
            !string.Equals(session.SessionId, sessionId, StringComparison.Ordinal) ||
            await _companies.FindByIdAsync(companyId, cancellationToken) is not { } company)
        {
            throw new InvalidOperationException($"The account payment '{reference}' doesn't belong to this checkout.");
        }

        if (await _transactions.GetByObligationAsync(session.SessionId, ObligationId, cancellationToken) is not null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var order = string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal)
            ? await _orders.FindByIdAsync(session.ReferenceId, cancellationToken)
            : null;
        var transaction = await _transactions.NewAsync(cancellationToken: cancellationToken);

        transaction.Title = order is null
            ? S["{0} on account", company.Name].Value
            : string.IsNullOrWhiteSpace(order.PurchaseOrderNumber)
                ? S["Order {0} on account ({1})", order.OrderNumber, company.Name].Value
                : S["Order {0}, PO {1}, on account ({2})", order.OrderNumber, order.PurchaseOrderNumber, company.Name].Value;
        transaction.Source = CompaniesConstants.TransactionSource;
        transaction.OwnerId = session.OwnerId;
        transaction.OwnerKind = CustomerOwnerKind.Authenticated;
        transaction.ReferenceType = session.ReferenceType;
        transaction.ReferenceId = session.ReferenceId;
        transaction.ReferenceVersionId = session.ReferenceVersionId;
        transaction.CheckoutSessionId = session.SessionId;
        transaction.ObligationId = ObligationId;
        transaction.Currency = currency;
        transaction.Amount = amount;
        transaction.TaxAmount = 0m;
        transaction.TotalAmount = amount;
        transaction.AmountPaid = 0m;
        transaction.Status = TransactionStatus.Outstanding;
        transaction.CreatedUtc = now;
        transaction.UpdatedUtc = now;
        transaction.DueUtc = now.AddDays(company.Terms.NetDays ?? 0);
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.Created,
            Message = S["Put on {0}'s account, due in {1} days.", company.Name, company.Terms.NetDays ?? 0].Value,
        });

        await _transactions.CreateAsync(transaction, cancellationToken);
    }

    public async Task CreditAsync(string reference, decimal amount, string currency, string reason, CancellationToken cancellationToken = default)
    {
        var (_, sessionId) = CompanyTermsService.FromReference(reference);

        if (amount <= 0m || string.IsNullOrEmpty(sessionId) ||
            await _transactions.GetByObligationAsync(sessionId, ObligationId, cancellationToken) is not { } transaction)
        {
            return;
        }

        // What's refunded comes off what's still owed; what was already paid on the invoice is refunded by the store.
        var owed = transaction.OutstandingAmount;
        var adjustment = Math.Min(amount, owed);

        if (adjustment <= 0m)
        {
            _logger.LogWarning("A refund of {Amount} went to the account of checkout '{SessionId}', whose invoice was already paid; refund the company directly.", amount, sessionId);

            return;
        }

        var result = await _commands.AdjustAsync(transaction.ItemId, adjustment, TransactionAdjustmentKind.Waiver, string.IsNullOrWhiteSpace(reason) ? S["Refunded"].Value : reason, cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning("The account invoice '{TransactionId}' couldn't be lowered by a refund: {Errors}", transaction.ItemId, string.Join(" ", result.Errors));
        }
    }
}
