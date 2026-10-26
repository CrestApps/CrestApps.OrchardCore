using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Transactions.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions;

/// <summary>
/// Exposes the transaction ledger as a data source of Orchard Core, for reports and data pipelines: the data source and
/// the data set of the transactions. The payments and refunds data sets are registered only while Checkout, which keeps
/// those ledgers, is enabled.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<TransactionReportDataSource>()
            .AddScoped<ITransactionReportDataSet, TransactionsReportDataSet>();
    }
}

/// <summary>
/// Registers the data sets of the payments and refunds ledgers, which Checkout keeps.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core", CheckoutConstants.Features.Area)]
public sealed class CheckoutDataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<ITransactionReportDataSet, PaymentAttemptsReportDataSet>()
            .AddScoped<ITransactionReportDataSet, PaymentRefundsReportDataSet>();
    }
}
