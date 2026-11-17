using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Plaid.Services;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace CrestApps.OrchardCore.Plaid;

/// <summary>
/// Registers the Plaid services. The host registers <see cref="IPlaidCredentialsProvider"/> and
/// <see cref="IPlaidLinkSite"/>.
/// </summary>
public static class PlaidCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Plaid client, its HTTP client and resilience, the webhook check, the item store, the bank account
    /// link provider, its observer and its webhook handler.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPlaidCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient(PlaidConstants.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .AddStandardResilienceHandler(ConfigureResilience);

        services.AddMemoryCache();
        services.AddSensitiveValueProtector();
        services.TryAddScoped<PlaidClient>();
        services.TryAddScoped<PlaidWebhookVerifier>();
        services.TryAddScoped<IPlaidItemStore, PlaidItemStore>();
        services.TryAddScoped<PlaidItemCleaner>();
        services.AddBankAccountLinkProvider<PlaidBankAccountLinkProvider>();
        services.AddBankAccountObserver<PlaidBankAccountObserver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProviderWebhookInboxHandler, PlaidWebhookInboxHandler>());

        return services;
    }

    /// <summary>
    /// Never retries a POST unless <see cref="PlaidClient"/> marks the request as safe to send again. Every Plaid call
    /// is a POST, so the rule of retrying safe methods only stays the default, and only Plaid's reads and idempotent
    /// calls opt in. The public token exchange never does: a public token works once.
    /// </summary>
    /// <param name="options">The resilience options.</param>
    internal static void ConfigureResilience(HttpStandardResilienceOptions options)
    {
        options.Retry.DisableForUnsafeHttpMethods();

        var unsafeMethodsDisabled = options.Retry.ShouldHandle;

        options.Retry.ShouldHandle = args =>
        {
            var request = args.Context.GetRequestMessage();

            if (request is not null && request.Options.TryGetValue(PlaidClient.RetryableKey, out var retryable) && retryable)
            {
                return ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome));
            }

            return unsafeMethodsDisabled(args);
        };
    }
}
