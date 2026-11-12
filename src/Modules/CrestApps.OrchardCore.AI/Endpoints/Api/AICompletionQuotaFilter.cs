using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Endpoints.Api;

/// <summary>
/// Holds every API key (or signed-in API caller) to a quota of model calls on the completion routes. A browser key is
/// public by design and its origin check trusts a header a script can set, so without a quota a copied key spends
/// model money without a ceiling.
/// </summary>
internal sealed class AICompletionQuotaFilter : IEndpointFilter
{
    /// <summary>
    /// The attempt policy that counts completion calls per key. Its limit can be changed under
    /// <c>CrestApps_Throttling:AttemptPolicies:ai-api-completions</c>.
    /// </summary>
    public const string PolicyName = "ai-api-completions";

    /// <summary>
    /// The default number of completion calls a key may make in <see cref="DefaultWindow"/>.
    /// </summary>
    public const int DefaultPermitLimit = 1000;

    /// <summary>
    /// The default quota window.
    /// </summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromHours(1);

    /// <summary>
    /// The rate-limit group of the completion routes, applied when Orchard Core's Rate Limits feature is enabled.
    /// </summary>
    public const string RateLimitGroup = "ai-completion";

    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var key = GetQuotaKey(httpContext.User);

        if (key is not null)
        {
            var limiter = httpContext.RequestServices.GetRequiredService<IAttemptLimiter>();
            var result = await limiter.AcquireAsync(PolicyName, key, httpContext.RequestAborted);

            if (!result.Allowed)
            {
                await ThrottlingServiceCollectionExtensions.WriteTooManyRequestsAsync(httpContext, result.RetryAfter, httpContext.RequestAborted);

                return Results.Empty;
            }
        }

        return await next(context);
    }

    /// <summary>
    /// The key a caller's quota is counted under: the API key, otherwise the user.
    /// </summary>
    /// <param name="user">The caller.</param>
    internal static string GetQuotaKey(ClaimsPrincipal user)
    {
        var keyId = user?.FindFirstValue(AIApiKeys.KeyClaimType);

        if (!string.IsNullOrEmpty(keyId))
        {
            return "key:" + keyId;
        }

        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(userId) ? null : "user:" + userId;
    }
}
