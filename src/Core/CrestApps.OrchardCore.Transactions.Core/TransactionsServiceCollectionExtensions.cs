using CrestApps.Core.Services;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.FinancialDocuments;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Transactions.Core;

/// <summary>
/// Registration helpers for the provider-agnostic Transactions core services.
/// </summary>
public static class TransactionsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the transaction ledger store and manager.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddTransactionsCore(this IServiceCollection services)
    {
        services.AddScoped<ITransactionStore, TransactionStore>();
        services.AddScoped<ITransactionManager, TransactionManager>();
        services.AddScoped<ITransactionSettlementService, TransactionSettlementService>();

        // Financial document numbers come from the shared number series, one series per kind of document, so a site
        // chooses the format of its receipts, invoices and credit notes in one place.
        services.AddNumberSequence();
        services.AddNumberSeries(FinancialDocumentSeries.GetName(FinancialDocumentKind.Receipt), LocalizationSource.Create("Receipts", typeof(FinancialDocumentSeries)), FinancialDocumentSeries.GetDefaultFormat(FinancialDocumentKind.Receipt));
        services.AddNumberSeries(FinancialDocumentSeries.GetName(FinancialDocumentKind.Invoice), LocalizationSource.Create("Invoices", typeof(FinancialDocumentSeries)), FinancialDocumentSeries.GetDefaultFormat(FinancialDocumentKind.Invoice));
        services.AddNumberSeries(FinancialDocumentSeries.GetName(FinancialDocumentKind.CreditNote), LocalizationSource.Create("Credit notes", typeof(FinancialDocumentSeries)), FinancialDocumentSeries.GetDefaultFormat(FinancialDocumentKind.CreditNote));
        services.AddNumberSeries(FinancialDocumentSeries.GetName(FinancialDocumentKind.RefundDocument), LocalizationSource.Create("Refund documents", typeof(FinancialDocumentSeries)), FinancialDocumentSeries.GetDefaultFormat(FinancialDocumentKind.RefundDocument));

        services.TryAddScoped<IFinancialDocumentNumberGenerator, SequentialFinancialDocumentNumberGenerator>();
        services.TryAddScoped<ITransactionInvoiceService, TransactionInvoiceService>();

        // A signed-in user sees the transactions they own and those owed by the parties they act for.
        services.AddPartyMembership();
        services.TryAddScoped<ITransactionVisibilityService, DefaultTransactionVisibilityService>();

        return services;
    }

    /// <summary>
    /// Registers the payment terms catalog: <see cref="IPaymentTermManager"/>, its store, and
    /// <see cref="IPaymentTermCalculator"/>. The host also registers <c>PaymentTermIndexProvider</c> and creates its index
    /// table.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddPaymentTermsCore(this IServiceCollection services)
    {
        services.TryAddScoped<IPaymentTermStore, PaymentTermStore>();
        services.TryAddScoped<IPaymentTermManager, PaymentTermManager>();
        services.TryAddScoped<ICatalogManager<PaymentTerm>>(serviceProvider => serviceProvider.GetRequiredService<IPaymentTermManager>());
        services.TryAddSingleton<IPaymentTermCalculator, PaymentTermCalculator>();

        return services;
    }

    /// <summary>
    /// Registers the payment issues: <see cref="IPaymentIssueStore"/> and <see cref="IPaymentIssueRecorder"/>, which
    /// keep money operations that failed and must be finished by hand. The host also registers
    /// <c>PaymentIssueIndexProvider</c> and creates its index table.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddPaymentIssues(this IServiceCollection services)
    {
        services.TryAddScoped<IPaymentIssueStore, PaymentIssueStore>();
        services.TryAddScoped<IPaymentIssueRecorder, DefaultPaymentIssueRecorder>();

        return services;
    }

    /// <summary>
    /// Registers a transaction source so the administration report can present and filter transactions by a
    /// friendly, localizable name instead of the raw source key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The technical source key stored on the transaction.</param>
    /// <param name="configure">A delegate that configures the source (for example its display name).</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddTransactionSource(
        this IServiceCollection services,
        string name,
        Action<TransactionSource> configure)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure<TransactionSourceOptions>(options =>
        {
            var source = new TransactionSource(name);
            configure(source);
            options.AddSource(source);
        });

        return services;
    }
}
