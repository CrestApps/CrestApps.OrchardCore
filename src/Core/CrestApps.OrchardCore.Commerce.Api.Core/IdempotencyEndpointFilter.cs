using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Commerce.Api;

/// <summary>
/// Makes a request safe to send again. A caller that sends <see cref="CommerceApi.IdempotencyKeyHeader"/> and gets no
/// answer, because the network dropped, sends the same request with the same key, and gets the first request's
/// response back instead of a second order, payment or refund. Keys are kept for a day, per tenant, caller and route.
/// </summary>
public sealed class IdempotencyEndpointFilter : IEndpointFilter
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// The longest key accepted.
    /// </summary>
    public const int MaxKeyLength = 128;

    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var key = httpContext.Request.Headers[CommerceApi.IdempotencyKeyHeader].ToString();

        if (string.IsNullOrWhiteSpace(key))
        {
            return await next(context);
        }

        if (key.Length > MaxKeyLength)
        {
            return ApiProblems.Validation(CommerceApi.IdempotencyKeyHeader, $"An idempotency key is at most {MaxKeyLength} characters.");
        }

        var cache = httpContext.RequestServices.GetRequiredService<IDistributedCache>();
        var cacheKey = GetCacheKey(httpContext, key);

        if (await cache.GetStringAsync(cacheKey, httpContext.RequestAborted) is { } stored)
        {
            return Replay(stored);
        }

        var (locker, locked) = await httpContext.RequestServices.GetRequiredService<IDistributedLock>()
            .TryAcquireLockAsync(cacheKey, TimeSpan.FromMilliseconds(500), TimeSpan.FromMinutes(2));

        if (!locked)
        {
            // The first request with this key is still running; answering it twice is what the key prevents.
            return ApiProblems.Conflict(ApiProblems.Codes.IdempotencyInProgress, "A request with this idempotency key is still being processed. Try again in a moment.");
        }

        await using (locker)
        {
            if (await cache.GetStringAsync(cacheKey, httpContext.RequestAborted) is { } storedMeanwhile)
            {
                return Replay(storedMeanwhile);
            }

            var result = await next(context);

            // Only an answer is kept: a server error is worth trying again.
            if (Capture(result) is { } response && response.StatusCode < StatusCodes.Status500InternalServerError)
            {
                await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(response), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _lifetime }, httpContext.RequestAborted);
            }

            return result;
        }
    }

    internal static string GetCacheKey(HttpContext httpContext, string key)
    {
        // A guest is known by their cart's token, or their checkout's when they have no cart. Never both: the answer to
        // starting a checkout hands out the checkout token, so a retry of it carries one the first request didn't.
        var caller = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
            httpContext.User?.FindFirstValue("sub") ??
            FirstNonEmpty(httpContext.Request.Headers[CommerceApi.CartTokenHeader].ToString(), httpContext.Request.Headers[CommerceApi.CheckoutTokenHeader].ToString());

        var scope = $"{httpContext.Request.PathBase}|{httpContext.Request.Method}|{httpContext.Request.Path}|{caller}|{key}";

        return "commerce-api-idempotency:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(scope)));
    }

    private static string FirstNonEmpty(string first, string second)
        => string.IsNullOrEmpty(first) ? second : first;

    internal static StoredResponse Capture(object result)
    {
        // A typed union, such as Results<Ok<T>, ProblemHttpResult>, wraps the result it returned.
        while (result is INestedHttpResult nested)
        {
            result = nested.Result;
        }

        var statusCode = result is IStatusCodeHttpResult { StatusCode: { } code } ? code : StatusCodes.Status200OK;
        var value = result is IValueHttpResult valueResult ? valueResult.Value : result is IResult ? null : result;

        return new StoredResponse(statusCode, value is null ? null : JsonSerializer.Serialize(value, value.GetType(), JsonSerializerOptions.Web));
    }

    private static IResult Replay(string stored)
    {
        var response = JsonSerializer.Deserialize<StoredResponse>(stored);

        return response.Body is null
            ? Results.StatusCode(response.StatusCode)
            : Results.Content(response.Body, "application/json", statusCode: response.StatusCode);
    }

    internal sealed record StoredResponse(int StatusCode, string Body);
}

public static class IdempotencyEndpointExtensions
{
    /// <summary>
    /// Makes the endpoint safe to call again with the same <see cref="CommerceApi.IdempotencyKeyHeader"/>.
    /// </summary>
    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter<IdempotencyEndpointFilter>();
}
