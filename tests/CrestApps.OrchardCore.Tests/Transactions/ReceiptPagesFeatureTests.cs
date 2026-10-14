using System.Reflection;
using CrestApps.OrchardCore.Receipts.Core;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Controllers;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// Printable receipts and invoices only show what the ledger holds, so they come with Transactions and Receipts
/// together. Sending receipts messages customers, so it stays a feature a site turns on deliberately.
/// </summary>
public sealed class ReceiptPagesFeatureTests
{
    [Fact]
    public void TheReceiptAndInvoicePages_ComeWithTheReceiptsModule_NotWithSendingReceipts()
    {
        Assert.Equal([ReceiptsConstants.Feature.Area], RequireFeaturesAttribute.GetRequiredFeatureNamesForType(typeof(ReceiptPagesStartup)));
        Assert.Null(typeof(ReceiptPagesStartup).GetCustomAttribute<FeatureAttribute>());
        Assert.Null(typeof(TransactionReceiptController).GetCustomAttribute<FeatureAttribute>());
        Assert.Null(typeof(TransactionInvoiceController).GetCustomAttribute<FeatureAttribute>());

        var pages = new ServiceCollection();
        new ReceiptPagesStartup().ConfigureServices(pages);

        Assert.Contains(pages, service => service.ServiceType == typeof(ITransactionReceiptBuilder));
        Assert.Contains(pages, service => service.ServiceType == typeof(ITransactionInvoiceBuilder));
    }

    [Fact]
    public void SendingReceipts_IsItsOwnFeature_AndOnlySends()
    {
        Assert.Equal(TransactionsConstants.Features.Receipts, typeof(ReceiptsStartup).GetCustomAttribute<FeatureAttribute>()?.FeatureName);

        var sending = new ServiceCollection();
        new ReceiptsStartup().ConfigureServices(sending);

        Assert.Contains(sending, service => service.ServiceType == typeof(PaymentReceiptTransactionPaymentHandler));
        Assert.DoesNotContain(sending, service => service.ServiceType == typeof(ITransactionReceiptBuilder));
        Assert.DoesNotContain(sending, service => service.ServiceType == typeof(ITransactionInvoiceBuilder));
    }
}
