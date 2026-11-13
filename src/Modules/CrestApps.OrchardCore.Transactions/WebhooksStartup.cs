using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions;

/// <summary>
/// Tells webhooks about transactions when the Commerce Webhooks feature is on.
/// </summary>
[RequireFeatures(CommerceConstants.Features.Webhooks)]
public sealed class WebhooksStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<TransactionWebhookEventHandler>();
        services.AddScoped<ITransactionEventHandler>(sp => sp.GetRequiredService<TransactionWebhookEventHandler>());
        services.AddScoped<ITransactionPaymentHandler>(sp => sp.GetRequiredService<TransactionWebhookEventHandler>());

        services.AddWebhookEvent(TransactionsConstants.WebhookEvents.Created, "Transaction created", "Transactions");
        services.AddWebhookEvent(TransactionsConstants.WebhookEvents.PaymentRecorded, "Transaction payment recorded", "Transactions");
        services.AddWebhookEvent(TransactionsConstants.WebhookEvents.Settled, "Transaction settled", "Transactions");
        services.AddWebhookEvent(TransactionsConstants.WebhookEvents.Overdue, "Transaction overdue", "Transactions");
        services.AddWebhookEvent(TransactionsConstants.WebhookEvents.Canceled, "Transaction canceled", "Transactions");
        services.AddWebhookEvent(TransactionsConstants.WebhookEvents.Refunded, "Transaction refunded", "Transactions");
    }
}
