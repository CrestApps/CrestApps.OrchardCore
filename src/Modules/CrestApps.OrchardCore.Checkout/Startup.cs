using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Handlers;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Core.Migrations;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Registers the provider-agnostic checkout and payment framework services.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICheckoutSessionStore, CheckoutSessionStore>();
        services.AddScoped<IPaymentAttemptStore, PaymentAttemptStore>();
        services.AddScoped<ICheckoutReconciliationService, CheckoutReconciliationService>();
        services.AddScoped<ICheckoutPaymentProviderResolver, CheckoutPaymentProviderResolver>();
        services.AddScoped<IPaymentAttemptLimiter, PaymentAttemptLimiter>();
        services.AddScoped<PaymentSessionCache>();
        services.AddScoped<ICheckoutHandler, PaymentCheckoutHandler>();
        services.AddSingleton<IBackgroundTask, CheckoutReconciliationBackgroundTask>();

        // The default tax service is a no-op. The Taxation integration feature replaces it with a
        // taxation-aware implementation when the Taxation feature is enabled.
        services.AddScoped<ICheckoutTaxService, NullCheckoutTaxService>();

        services.AddDataMigration<CheckoutMigrations>()
            .AddIndexProvider<CheckoutSessionIndexProvider>()
            .AddIndexProvider<PaymentAttemptIndexProvider>();

        services.Configure<PaymentSessionCacheOptions>(options =>
        {
            options.MaxLiveSession = TimeSpan.FromHours(2);
        });

        services.Configure<PaymentRateLimitOptions>(_ => { });
    }
}
