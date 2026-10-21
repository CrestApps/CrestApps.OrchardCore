using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using OrchardCore.Locking;
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
            return Replay(stored, httpContext);
        }

        var (locker, locked) = await httpContext.RequestServices.GetRequiredService<IDistributedLock>()
            .TryAcquireLockAsync(cacheKey, TimeSpan.FromMilliseconds(500), TimeSpan.FromMinutes(2));

        if (!locked)
        {
            // The first request with this key is still running; answering it twice is what the key prevents.
            return ApiProblems.Conflict(ApiProblems.Codes.IdempotencyInProgress, "A request with this idempotency key is still being processed. Try again in a moment.");
        }

        var lockHeldByResult = false;

        try
        {
            if (await cache.GetStringAsync(cacheKey, httpContext.RequestAborted) is { } storedMeanwhile)
            {
                return Replay(storedMeanwhile, httpContext);
            }

            var result = await next(context);

            // Where a created thing is only shows once the result has written its Location header, so a created
            // answer is kept after it runs, and the key stays locked until then.
            if (result is IResult created && Unwrap(result) is IStatusCodeHttpResult { StatusCode: StatusCodes.Status201Created })
            {
                lockHeldByResult = true;

                return new KeepAfterExecutionResult(created, cache, cacheKey, locker);
            }

            await KeepAsync(cache, cacheKey, result, httpContext);

            return result;
        }
        finally
        {
            if (!lockHeldByResult)
            {
                await locker.DisposeAsync();
            }
        }
    }

    // Only a final answer is kept. A server error, a "too many requests", and a conflict that clears by itself (someone
    // else changing the same thing) are worth sending again with the same key.
    private static async Task KeepAsync(IDistributedCache cache, string cacheKey, object result, HttpContext httpContext)
    {
        if (Capture(result, httpContext.Response.Headers) is { } response && IsFinal(response))
        {
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(response), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _lifetime }, httpContext.RequestAborted);
        }
    }

    // A typed union, such as Results<Ok<T>, ProblemHttpResult>, wraps the result it returned.
    private static object Unwrap(object result)
    {
        while (result is INestedHttpResult nested)
        {
            result = nested.Result;
        }

        return result;
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

    // The headers a client acts on: the guest tokens the first answer handed out, and where a created thing is.
    private static readonly string[] _replayedHeaders = [CommerceApi.CartTokenHeader, CommerceApi.CheckoutTokenHeader, HeaderNames.Location];

    internal static StoredResponse Capture(object result, IHeaderDictionary responseHeaders = null)
    {
        result = Unwrap(result);

        var statusCode = result is IStatusCodeHttpResult { StatusCode: { } code } ? code : StatusCodes.Status200OK;
        var value = result is IValueHttpResult valueResult ? valueResult.Value : result is IResult ? null : result;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in _replayedHeaders)
        {
            if (responseHeaders is not null && responseHeaders.TryGetValue(name, out var headerValue) && !string.IsNullOrEmpty(headerValue))
            {
                headers[name] = headerValue.ToString();
            }
        }

        return new StoredResponse(
            statusCode,
            value is null ? null : JsonSerializer.Serialize(value, value.GetType(), JsonSerializerOptions.Web),
            value is Microsoft.AspNetCore.Mvc.ProblemDetails ? "application/problem+json" : "application/json",
            headers);
    }

    /// <summary>
    /// Returns whether an answer is the request's final one, and kept for a retry; otherwise a retry with the same key
    /// is tried afresh.
    /// </summary>
    internal static bool IsFinal(StoredResponse response)
        => response.StatusCode < StatusCodes.Status500InternalServerError &&
            response.StatusCode != StatusCodes.Status429TooManyRequests &&
            !(response.StatusCode == StatusCodes.Status409Conflict &&
                response.Body is { } body &&
                (body.Contains($"\"code\":\"{ApiProblems.Codes.Concurrency}\"", StringComparison.Ordinal) ||
                 body.Contains($"\"code\":\"{ApiProblems.Codes.IdempotencyInProgress}\"", StringComparison.Ordinal)));

    private static IResult Replay(string stored, HttpContext httpContext)
    {
        var response = JsonSerializer.Deserialize<StoredResponse>(stored);

        foreach (var (name, value) in response.Headers ?? new Dictionary<string, string>())
        {
            httpContext.Response.Headers[name] = value;
        }

        return response.Body is null
            ? Results.StatusCode(response.StatusCode)
            : Results.Content(response.Body, response.ContentType ?? "application/json", statusCode: response.StatusCode);
    }

    internal sealed record StoredResponse(int StatusCode, string Body, string ContentType = null, IReadOnlyDictionary<string, string> Headers = null);

    // Runs a created answer, then keeps it with the headers it wrote, and only then lets a retry with the same key in.
    private sealed class KeepAfterExecutionResult : IResult, INestedHttpResult
    {
        private readonly IDistributedCache _cache;
        private readonly string _cacheKey;
        private readonly ILocker _locker;

        public KeepAfterExecutionResult(
            IResult result,
            IDistributedCache cache,
            string cacheKey,
            ILocker locker)
        {
            Result = result;
            _cache = cache;
            _cacheKey = cacheKey;
            _locker = locker;
        }

        public IResult Result { get; }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            await using (_locker)
            {
                await Result.ExecuteAsync(httpContext);
                await KeepAsync(_cache, _cacheKey, Result, httpContext);
            }
        }
    }
}

public static class IdempotencyEndpointExtensions
{
    /// <summary>
    /// Makes the endpoint safe to call again with the same <see cref="CommerceApi.IdempotencyKeyHeader"/>.
    /// </summary>
    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter<IdempotencyEndpointFilter>();
}
