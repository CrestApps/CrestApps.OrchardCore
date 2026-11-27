using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Api;

/// <summary>
/// Makes a request safe to send again. A caller that sends <see cref="ModuleApi.IdempotencyKeyHeader"/> and gets no
/// answer, because the network dropped, sends the same request with the same key, and gets the first request's
/// response back instead of a second order, payment or refund. Keys are kept for a day, per tenant, caller and route.
/// The same key with another request (another body or route values) is refused rather than answered with the first
/// response; stored answers are encrypted with the tenant's data protection, since they can hold client secrets and
/// guest tokens; and a caller with no identity at all doesn't use the key, so it never shares another caller's answer.
/// </summary>
public sealed class IdempotencyEndpointFilter : IEndpointFilter
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromHours(24);

    // Long enough for the slowest provider call a request makes: the key must not open to a retry while it runs.
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(10);

    // The purpose predates the move out of Commerce; it stays so answers kept before an upgrade can still be read.
    private const string ProtectionPurpose = "CrestApps.OrchardCore.Commerce.Api.Idempotency";

    private readonly IReadOnlyList<int> _requestArguments;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyEndpointFilter"/> class that fingerprints every argument
    /// that isn't the request's plumbing (the HTTP context, the user, a cancellation token).
    /// </summary>
    public IdempotencyEndpointFilter()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyEndpointFilter"/> class that fingerprints the arguments
    /// the request binds (its body, route and query values), by position.
    /// </summary>
    /// <param name="requestArguments">The positions of the handler's arguments the request binds.</param>
    public IdempotencyEndpointFilter(IReadOnlyList<int> requestArguments)
    {
        _requestArguments = requestArguments;
    }

    /// <summary>
    /// The longest key accepted.
    /// </summary>
    public const int MaxKeyLength = 128;

    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var policy = httpContext.GetEndpoint()?.Metadata.GetMetadata<IdempotencyPolicy>();
        var keyHeader = policy?.KeyHeader ?? ModuleApi.IdempotencyKeyHeader;
        var key = httpContext.Request.Headers[keyHeader].ToString();

        if (string.IsNullOrWhiteSpace(key))
        {
            return policy?.MissingKey?.Invoke(httpContext) ?? await next(context);
        }

        var maxKeyLength = policy?.MaxKeyLength ?? MaxKeyLength;

        if (key.Length > maxKeyLength)
        {
            return policy?.InvalidKey?.Invoke(httpContext) ?? ApiProblems.Validation(keyHeader, $"An idempotency key is at most {maxKeyLength} characters.");
        }

        if (policy?.EchoKey == true)
        {
            httpContext.Response.Headers[keyHeader] = key;
        }

        // A key is kept per caller. A caller the request doesn't identify (no user, no guest token) could share another
        // stranger's answer, so the key isn't used: the request runs as if it had none, and nothing is kept.
        var caller = ResolveCaller(httpContext, policy);

        if (string.IsNullOrEmpty(caller))
        {
            return await next(context);
        }

        var cache = new ProtectedCache(httpContext.RequestServices.GetRequiredService<IDistributedCache>(), httpContext.RequestServices.GetService<IDataProtectionProvider>()?.CreateProtector(ProtectionPurpose));
        var cacheKey = GetCacheKey(httpContext, key, policy);

        // Without a protocol's own fingerprint, the request's arguments are the fingerprint, so the same key with
        // another body is refused instead of answered with the first request's response.
        var fingerprint = policy?.Fingerprint is not null ? policy.Fingerprint(httpContext) : GetFingerprint(context, _requestArguments);
        var checkFingerprint = policy?.Fingerprint is not null || fingerprint is not null;

        if (await cache.GetStringAsync(cacheKey, httpContext.RequestAborted) is { } stored)
        {
            return Replay(stored, httpContext, policy, fingerprint, checkFingerprint);
        }

        var (locker, locked) = await httpContext.RequestServices.GetRequiredService<IDistributedLock>()
            .TryAcquireLockAsync(cacheKey, TimeSpan.FromMilliseconds(500), _lockExpiration);

        if (!locked)
        {
            // The first request with this key is still running; answering it twice is what the key prevents.
            return policy?.InFlight?.Invoke(httpContext) ?? ApiProblems.Conflict(ApiProblems.Codes.IdempotencyInProgress, "A request with this idempotency key is still being processed. Try again in a moment.");
        }

        var lockHeldByResult = false;

        try
        {
            if (await cache.GetStringAsync(cacheKey, httpContext.RequestAborted) is { } storedMeanwhile)
            {
                return Replay(storedMeanwhile, httpContext, policy, fingerprint, checkFingerprint);
            }

            // A key used before the upgrade was kept under the caller as it was named then, in plain text and without the
            // request's fingerprint, so its answer can be neither trusted nor matched. Its request did run, though, so it
            // isn't run again: the caller is told to send it with a new key if it still needs to run.
            if (await cache.ExistsUnprotectedAsync(GetLegacyCacheKey(httpContext, key, policy), httpContext.RequestAborted))
            {
                return policy?.Conflict?.Invoke(httpContext) ?? ApiProblems.Conflict(ApiProblems.Codes.IdempotencyKeyReused, "This idempotency key was used before the store was upgraded; send this request with a new key if it still needs to run.");
            }

            var result = await next(context);

            // Where a created thing is only shows once the result has written its Location header, so a created
            // answer is kept after it runs, and the key stays locked until then.
            if (result is IResult created && Unwrap(result) is IStatusCodeHttpResult { StatusCode: StatusCodes.Status201Created })
            {
                lockHeldByResult = true;

                return new KeepAfterExecutionResult(created, cache, cacheKey, locker, policy, fingerprint);
            }

            await KeepAsync(cache, cacheKey, result, httpContext, policy, fingerprint);

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

    // Only a final answer is kept. A server error, a "too many requests", a conflict that clears by itself (someone
    // else changing the same thing) and a request the caller must correct (400, 422) are worth sending again with the
    // same key. The answer is kept even when the client stopped waiting: the work is done, and its retry must get it.
    // A cache that can't keep it doesn't turn a done request into an error: the work's answer is still sent.
    private static async Task KeepAsync(ProtectedCache cache, string cacheKey, object result, HttpContext httpContext, IdempotencyPolicy policy = null, string fingerprint = null)
    {
        if (Capture(result, httpContext.Response.Headers, ReplayedHeaders(httpContext)) is not { } response || !IsFinal(response))
        {
            return;
        }

        try
        {
            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(response with { Fingerprint = fingerprint }),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = policy?.Lifetime ?? _lifetime },
                CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            httpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger<IdempotencyEndpointFilter>()
                .LogWarning(exception, "The answer of an idempotent request couldn't be kept; a retry with its key will run it again.");
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

    internal static string GetCacheKey(HttpContext httpContext, string key, IdempotencyPolicy policy = null)
        => BuildCacheKey(httpContext, ResolveCaller(httpContext, policy), key);

    // Where a key's answer was kept before each kind of caller had its own prefix. Such an answer expires within a day of
    // the upgrade.
    internal static string GetLegacyCacheKey(HttpContext httpContext, string key, IdempotencyPolicy policy = null)
        => BuildCacheKey(
            httpContext,
            policy?.Caller?.Invoke(httpContext) ??
                httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                httpContext.User?.FindFirstValue("sub") ??
                FirstGuestHeader(httpContext),
            key);

    private static string BuildCacheKey(HttpContext httpContext, string caller, string key)
    {
        var scope = $"{httpContext.Request.PathBase}|{httpContext.Request.Method}|{httpContext.Request.Path}|{caller}|{key}";

        // The prefix predates the move out of Commerce.
        return "commerce-api-idempotency:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(scope)));
    }

    // A guest is known by the first guest header the endpoint's group names that the request carries, such as a cart's
    // token, or a checkout's when there is no cart. Never both: the answer to starting a checkout hands out the checkout
    // token, so a retry of it carries one the first request didn't. Each kind of caller has its own prefix, so a guest
    // whose token is a user's id is never in that user's scope.
    internal static string ResolveCaller(HttpContext httpContext, IdempotencyPolicy policy = null)
        => Prefixed("caller:", policy?.Caller?.Invoke(httpContext)) ??
            Prefixed("user:", httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? httpContext.User?.FindFirstValue("sub")) ??
            Prefixed("guest:", FirstGuestHeader(httpContext)) ??
            string.Empty;

    private static string Prefixed(string prefix, string caller)
        => string.IsNullOrEmpty(caller) ? null : prefix + caller;

    private static string FirstGuestHeader(HttpContext httpContext)
    {
        foreach (var name in httpContext.GetEndpoint()?.Metadata.GetMetadata<IdempotencyGuestHeaders>()?.Headers ?? [])
        {
            if (httpContext.Request.Headers[name].ToString() is { Length: > 0 } value)
            {
                return value;
            }
        }

        return string.Empty;
    }

    // The headers a client acts on: where a created thing is, and the guest headers the first answer handed out.
    private static IEnumerable<string> ReplayedHeaders(HttpContext httpContext)
        => [HeaderNames.Location, .. httpContext?.GetEndpoint()?.Metadata.GetMetadata<IdempotencyGuestHeaders>()?.Headers ?? []];

    internal static StoredResponse Capture(object result, IHeaderDictionary responseHeaders = null, IEnumerable<string> replayedHeaders = null)
    {
        result = Unwrap(result);

        var statusCode = result is IStatusCodeHttpResult { StatusCode: { } code } ? code : StatusCodes.Status200OK;
        var value = result is IValueHttpResult valueResult ? valueResult.Value : result is IResult ? null : result;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in replayedHeaders ?? [HeaderNames.Location])
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
            response.StatusCode != StatusCodes.Status400BadRequest &&
            response.StatusCode != StatusCodes.Status422UnprocessableEntity &&
            !(response.StatusCode == StatusCodes.Status409Conflict &&
                response.Body is { } body &&
                (body.Contains($"\"code\":\"{ApiProblems.Codes.Concurrency}\"", StringComparison.Ordinal) ||
                 body.Contains($"\"code\":\"{ApiProblems.Codes.IdempotencyInProgress}\"", StringComparison.Ordinal)));

    // A hash of the arguments the request bound: its body, route and query values.
    internal static string GetFingerprint(EndpointFilterInvocationContext context, IReadOnlyList<int> requestArguments = null)
    {
        var values = new List<object>();

        for (var i = 0; i < context.Arguments.Count; i++)
        {
            var argument = context.Arguments[i];

            if (requestArguments is not null ? !requestArguments.Contains(i) : IsPlumbing(argument))
            {
                continue;
            }

            values.Add(argument);
        }

        if (values.Count == 0)
        {
            return null;
        }

        try
        {
            return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(values, JsonSerializerOptions.Web)));
        }
        catch (Exception exception) when (exception is NotSupportedException or JsonException or InvalidOperationException)
        {
            // A request whose arguments can't be compared matches no other request, so its key never replays another
            // request's answer; a retry of it is refused instead of done twice.
            return "unfingerprinted:" + Guid.NewGuid().ToString("N");
        }
    }

    private static bool IsPlumbing(object argument)
        => argument is null or HttpContext or HttpRequest or HttpResponse or CancellationToken or ClaimsPrincipal ||
            argument.GetType().Namespace?.StartsWith("Microsoft.", StringComparison.Ordinal) == true ||
            argument.GetType().Namespace?.StartsWith("OrchardCore", StringComparison.Ordinal) == true ||
            argument.GetType().Namespace?.StartsWith("YesSql", StringComparison.Ordinal) == true ||
            (argument.GetType().Namespace?.StartsWith("CrestApps.", StringComparison.Ordinal) == true && argument.GetType().Name.EndsWith("Service", StringComparison.Ordinal)) ||
            (argument.GetType().Namespace?.StartsWith("CrestApps.", StringComparison.Ordinal) == true && argument.GetType().Name.EndsWith("Store", StringComparison.Ordinal));

    private static IResult Replay(string stored, HttpContext httpContext, IdempotencyPolicy policy = null, string fingerprint = null, bool checkFingerprint = false)
    {
        var response = JsonSerializer.Deserialize<StoredResponse>(stored);

        // The same key with another request is a mistake the caller must fix, not a retry.
        if (checkFingerprint && !string.Equals(response.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            return policy?.Conflict?.Invoke(httpContext) ?? ApiProblems.Conflict(ApiProblems.Codes.IdempotencyKeyReused, "This idempotency key was used for another request; send this one with a new key.");
        }

        if (!string.IsNullOrEmpty(policy?.ReplayedHeader))
        {
            httpContext.Response.Headers[policy.ReplayedHeader] = "true";
        }

        foreach (var (name, value) in response.Headers ?? new Dictionary<string, string>())
        {
            httpContext.Response.Headers[name] = value;
        }

        return response.Body is null
            ? Results.StatusCode(response.StatusCode)
            : Results.Content(response.Body, response.ContentType ?? "application/json", statusCode: response.StatusCode);
    }

    internal sealed record StoredResponse(int StatusCode, string Body, string ContentType = null, IReadOnlyDictionary<string, string> Headers = null, string Fingerprint = null);

    // The distributed cache, with what is stored encrypted: a stored answer can hold a client secret or a guest token.
    private sealed class ProtectedCache
    {
        private readonly IDistributedCache _cache;
        private readonly IDataProtector _protector;

        public ProtectedCache(IDistributedCache cache, IDataProtector protector)
        {
            _cache = cache;
            _protector = protector;
        }

        public async Task<string> GetStringAsync(string key, CancellationToken cancellationToken)
        {
            var value = await _cache.GetStringAsync(key, cancellationToken);

            if (value is null || _protector is null)
            {
                return value;
            }

            try
            {
                return _protector.Unprotect(value);
            }
            catch (CryptographicException)
            {
                // Kept before answers were encrypted, or with a key that is gone: treated as not kept.
                return null;
            }
        }

        // Whether anything is kept under the key, read without decrypting it.
        public async Task<bool> ExistsUnprotectedAsync(string key, CancellationToken cancellationToken)
            => await _cache.GetAsync(key, cancellationToken) is not null;

        public Task SetStringAsync(string key, string value, DistributedCacheEntryOptions options, CancellationToken cancellationToken)
            => _cache.SetStringAsync(key, _protector is null ? value : _protector.Protect(value), options, cancellationToken);
    }

    // Runs a created answer, then keeps it with the headers it wrote, and only then lets a retry with the same key in.
    private sealed class KeepAfterExecutionResult : IResult, INestedHttpResult, IStatusCodeHttpResult
    {
        private readonly ProtectedCache _cache;
        private readonly string _cacheKey;
        private readonly ILocker _locker;
        private readonly IdempotencyPolicy _policy;
        private readonly string _fingerprint;

        public KeepAfterExecutionResult(
            IResult result,
            ProtectedCache cache,
            string cacheKey,
            ILocker locker,
            IdempotencyPolicy policy,
            string fingerprint)
        {
            Result = result;
            _cache = cache;
            _cacheKey = cacheKey;
            _locker = locker;
            _policy = policy;
            _fingerprint = fingerprint;
        }

        public IResult Result { get; }

        public int? StatusCode
            => StatusCodes.Status201Created;

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            await using (_locker)
            {
                await Result.ExecuteAsync(httpContext);
                await KeepAsync(_cache, _cacheKey, Result, httpContext, _policy, _fingerprint);
            }
        }
    }
}

public static class IdempotencyEndpointExtensions
{
    /// <summary>
    /// Makes the endpoint safe to call again with the same <see cref="ModuleApi.IdempotencyKeyHeader"/>.
    /// </summary>
    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilterFactory(CreateFilter);

    /// <summary>
    /// Makes an endpoint idempotent with a protocol's own rules.
    /// </summary>
    /// <param name="builder">The endpoint.</param>
    /// <param name="policy">The protocol's rules.</param>
    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder, IdempotencyPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return builder.WithMetadata(policy).AddEndpointFilterFactory(CreateFilter);
    }

    // The arguments the request binds (not services, the HTTP context or a cancellation token) are what a request is
    // fingerprinted by.
    private static EndpointFilterDelegate CreateFilter(EndpointFilterFactoryContext context, EndpointFilterDelegate next)
    {
        var isService = context.ApplicationServices.GetService<IServiceProviderIsService>();
        var requestArguments = new List<int>();
        var parameters = context.MethodInfo.GetParameters();

        for (var i = 0; i < parameters.Length; i++)
        {
            var type = parameters[i].ParameterType;

            if (type == typeof(HttpContext) || type == typeof(CancellationToken) || type == typeof(ClaimsPrincipal) ||
                type == typeof(HttpRequest) || type == typeof(HttpResponse) ||
                parameters[i].IsDefined(typeof(Microsoft.AspNetCore.Mvc.FromServicesAttribute), inherit: true) ||
                isService?.IsService(type) == true ||
                (type.IsInterface && !type.IsGenericType))
            {
                continue;
            }

            requestArguments.Add(i);
        }

        var filter = new IdempotencyEndpointFilter(requestArguments);

        return invocationContext => filter.InvokeAsync(invocationContext, next);
    }
}
