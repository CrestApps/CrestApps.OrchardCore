using System.Net;
using CrestApps.OrchardCore.Stripe.Workflows;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Observes outgoing Stripe API calls and raises the Stripe "request failed" workflow event when an
/// authentication or connectivity problem is detected, so operators can build alerting workflows. The
/// event is throttled per tenant to avoid flooding when many calls fail in quick succession.
/// </summary>
public sealed class StripeApiFailureHandler : DelegatingHandler
{
    private static readonly TimeSpan _throttleWindow = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _memoryCache;
    private readonly string _throttleCacheKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeApiFailureHandler"/> class.
    /// </summary>
    /// <param name="memoryCache">The cache that holds the alert throttle, which the host shares across tenants.</param>
    /// <param name="shellSettings">The settings of the tenant whose Stripe calls this handler observes.</param>
    public StripeApiFailureHandler(
        IMemoryCache memoryCache,
        ShellSettings shellSettings)
    {
        _memoryCache = memoryCache;

        // The memory cache is shared by every tenant, so one tenant's failures must not silence another's alerts.
        _throttleCacheKey = $"Stripe:RequestFailedEvent:Throttle:{shellSettings.Name}";
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            NotifyFailure("connectivity_error", ex.Message);

            throw;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            NotifyFailure("authentication_error", $"Stripe responded with HTTP status code {(int)response.StatusCode}.");
        }

        return response;
    }

    private void NotifyFailure(string reason, string message)
    {
        if (_memoryCache.TryGetValue(_throttleCacheKey, out _))
        {
            return;
        }

        _memoryCache.Set(_throttleCacheKey, true, _throttleWindow);

        // The workflow event is raised after the request's scope completes, in a scope of its own.
        ShellScope.AddDeferredTask(async deferredScope =>
        {
            var notifier = deferredScope.ServiceProvider.GetService<StripeWorkflowNotifier>();

            if (notifier == null)
            {
                return;
            }

            await notifier.TriggerAsync(
                StripeWorkflowEventNames.RequestFailed,
                new Dictionary<string, object>
                {
                    { "Reason", reason },
                    { "Message", message },
                },
                correlationId: "StripeRequestFailed");
        });
    }
}
