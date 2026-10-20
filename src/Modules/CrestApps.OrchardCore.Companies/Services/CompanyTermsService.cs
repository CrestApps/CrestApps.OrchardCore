using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Companies.Services;

/// <summary>
/// A company's account with the store: what it owes on its terms, what more it may owe, and what part of an order goes
/// on the account rather than paid at checkout.
/// </summary>
public sealed class CompanyTermsService
{
    // The most a company with no credit limit can put on account, to stand for "no limit" in the ledger's sums.
    internal const decimal Unlimited = 1_000_000_000m;

    private readonly ITransactionManager _transactions;
    private readonly CompanyService _companies;
    private readonly ISiteService _siteService;

    public CompanyTermsService(ITransactionManager transactions, CompanyService companies, ISiteService siteService)
    {
        _transactions = transactions;
        _companies = companies;
        _siteService = siteService;
    }

    /// <summary>
    /// Returns what a company owes on its terms, across its buyers, in the store's currency.
    /// </summary>
    public async Task<(decimal Owed, string Currency)> GetOwedAsync(Company company, CancellationToken cancellationToken = default)
    {
        var currency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;
        var owed = 0m;

        foreach (var buyer in company?.Buyers ?? [])
        {
            for (var page = 1; ; page++)
            {
                var result = await _transactions.PageAsync(page, 100, new TransactionQuery
                {
                    OwnerId = buyer.UserId,
                    Source = CompaniesConstants.TransactionSource,
                    OutstandingOnly = true,
                }, cancellationToken);

                owed += result.Entries
                    .Where(transaction => string.Equals(transaction.Currency, currency, StringComparison.OrdinalIgnoreCase))
                    .Sum(transaction => transaction.OutstandingAmount);

                if (result.Entries.Count < 100)
                {
                    break;
                }
            }
        }

        return (owed, currency);
    }

    /// <summary>
    /// Returns what more a company may owe: its credit limit less what it owes, or no limit.
    /// </summary>
    public async Task<decimal> GetAvailableCreditAsync(Company company, CancellationToken cancellationToken = default)
    {
        if (company?.Terms?.CreditLimit is not { } limit)
        {
            return Unlimited;
        }

        var (owed, _) = await GetOwedAsync(company, cancellationToken);

        return Math.Max(0m, limit - owed);
    }

    /// <summary>
    /// Returns the company a checkout is paid for on account and the part of it that goes on the account, or nulls
    /// when it can't be: its buyer doesn't buy for a company with terms, it's a register's sale, or it's in another
    /// currency than the store's.
    /// </summary>
    public async Task<(Company Company, decimal Amount)> GetForCheckoutAsync(CheckoutFlow flow, CheckoutInvoice invoice, CancellationToken cancellationToken = default)
    {
        if (flow?.Session is not CheckoutSession { OwnerId: { Length: > 0 } ownerId } session ||
            session.TryGet<CheckoutCounterSale>(out _) ||
            invoice is null ||
            await _companies.GetForUserAsync(ownerId, cancellationToken) is not { Terms.OffersTerms: true } company)
        {
            return (null, 0m);
        }

        var storeCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;

        if (!string.Equals(invoice.Currency, storeCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return (null, 0m);
        }

        // The deposit is paid at checkout; the rest of the order goes on account, up to the credit left.
        var onAccount = Money.Round(invoice.GrandTotal * (100m - Math.Clamp(company.Terms.DepositPercent, 0m, 100m)) / 100m, invoice.Currency);

        return (company, Math.Min(onAccount, await GetAvailableCreditAsync(company, cancellationToken)));
    }

    /// <summary>
    /// Returns the reference a checkout's account payment is chosen and recorded by: the company and the checkout.
    /// </summary>
    public static string ToReference(string companyId, string sessionId)
        => $"{companyId}:{sessionId}";

    /// <summary>
    /// Returns the company and checkout a reference names.
    /// </summary>
    public static (string CompanyId, string SessionId) FromReference(string reference)
    {
        var separator = reference?.IndexOf(':', StringComparison.Ordinal) ?? -1;

        return separator > 0 ? (reference[..separator], reference[(separator + 1)..]) : (null, null);
    }
}
