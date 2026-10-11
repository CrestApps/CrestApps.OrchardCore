using System.Net;
using CrestApps.OrchardCore.Stripe.Workflows;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Observes outgoing Stripe API calls and raises the Stripe "request failed" workflow event when an
/// authentication or connectivity problem is detected, so operators can build alerting workflows. The
/// event is throttled to avoid flooding when many calls fail in quick succession.
/// </summary>
public sealed class StripeApiFailureHandler : DelegatingHandler
{
    private const string ThrottleCacheKey = "Stripe:RequestFailedEvent:Throttle";
    private static readonly TimeSpan _throttleWindow = TimeSpan.FromMinutes(5);

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

    private static void NotifyFailure(string reason, string message)
    {
        var scope = ShellScope.Current;

        if (scope == null)
        {
            return;
        }

        var cache = scope.ServiceProvider.GetService<IMemoryCache>();

        if (cache != null)
        {
            if (cache.TryGetValue(ThrottleCacheKey, out _))
            {
                return;
            }

            cache.Set(ThrottleCacheKey, true, _throttleWindow);
        }

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
