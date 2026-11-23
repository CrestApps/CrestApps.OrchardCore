using CrestApps.OrchardCore.MailingLists.Mailchimp.Services;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp;

/// <summary>
/// Registers the Mailchimp provider of mailing lists. The host registers <see cref="IMailchimpCredentialsProvider"/>.
/// </summary>
public static class MailchimpCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Mailchimp client with its HTTP client, resilience and per-tenant call limit, the mailing list
    /// provider, the default welcome coupon code reader and the webhook inbox handler.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddMailchimpMailingLists(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Mailchimp allows a call 120 seconds; the client's own time-out stays above the resilience pipeline's.
        services.AddHttpClient(MailchimpConstants.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(200))
            .AddStandardResilienceHandler(ConfigureResilience);

        services.TryAddSingleton<MailchimpConcurrencyGate>();
        services.TryAddScoped<MailchimpClient>();
        services.AddMailingListProvider<MailchimpMailingListProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IMailingListSettingsValidator, MailchimpMailingListSettingsValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProviderWebhookInboxHandler, MailchimpWebhookInboxHandler>());

        return services;
    }

    /// <summary>
    /// Gives each attempt up to a minute, honors Mailchimp's <c>Retry-After</c> on a 429, and never retries a request
    /// unless <see cref="MailchimpClient"/> marks it safe to send again: reads, member upserts, status changes, tags and
    /// deletes are; creating a webhook or a merge field is not.
    /// </summary>
    /// <param name="options">The resilience options.</param>
    internal static void ConfigureResilience(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(180);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
        options.Retry.DisableForUnsafeHttpMethods();

        var unsafeMethodsDisabled = options.Retry.ShouldHandle;

        options.Retry.ShouldHandle = args =>
        {
            var request = args.Context.GetRequestMessage();

            if (request is not null && request.Options.TryGetValue(MailchimpClient.RetryableKey, out var retryable) && retryable)
            {
                return ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome));
            }

            return unsafeMethodsDisabled(args);
        };
    }
}
