using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Core.Services;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// The embedded checkout a browser frame runs: which checkout, for which platform, and until when the host's credential
/// holds. It travels in a signed cookie that, over HTTPS, lives only in the platform's partition, so it is only sent
/// from the platform's frame.
/// </summary>
public sealed class EmbeddedCheckoutContext
{
    /// <summary>
    /// Gets or sets the agent checkout.
    /// </summary>
    [JsonPropertyName("c")]
    public string CheckoutId { get; set; }

    /// <summary>
    /// Gets or sets the store checkout session the frame may show.
    /// </summary>
    [JsonPropertyName("s")]
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the agent platform whose pages may frame it.
    /// </summary>
    [JsonPropertyName("p")]
    public string PlatformId { get; set; }

    /// <summary>
    /// Gets or sets the UCP version of the session.
    /// </summary>
    [JsonPropertyName("v")]
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host must hold a valid credential for the checkout to be framed.
    /// </summary>
    [JsonPropertyName("a")]
    public bool AuthRequired { get; set; }

    /// <summary>
    /// Gets or sets until when the host's credential holds.
    /// </summary>
    [JsonPropertyName("u")]
    public DateTime? AuthenticatedUntilUtc { get; set; }

    /// <summary>
    /// Returns whether the checkout may be framed now: no credential is needed, or the host's credential still holds.
    /// </summary>
    /// <param name="utcNow">The current time.</param>
    public bool IsAuthorized(DateTime utcNow)
        => !AuthRequired || (AuthenticatedUntilUtc is { } until && until > utcNow);
}

/// <summary>
/// Reads and writes the cookie that carries an <see cref="EmbeddedCheckoutContext"/>.
/// </summary>
public sealed class EmbeddedCheckoutContextCookie
{
    /// <summary>
    /// The longest an embedded checkout's frame keeps its cookie.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(6);

    private readonly SignedTokenService _signedTokenService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedCheckoutContextCookie"/> class.
    /// </summary>
    /// <param name="signedTokenService">The signed token service.</param>
    public EmbeddedCheckoutContextCookie(SignedTokenService signedTokenService)
    {
        _signedTokenService = signedTokenService;
    }

    /// <summary>
    /// Returns the context of the frame the request comes from, or <see langword="null"/> when it carries none or one
    /// that ran out.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    public EmbeddedCheckoutContext Read(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!httpContext.Request.Cookies.TryGetValue(AgenticCommerceStorage.EmbeddedCheckoutContextCookieName, out var value) ||
            string.IsNullOrEmpty(value) ||
            _signedTokenService.Unprotect(AgenticCommerceStorage.EmbeddedCheckoutContextPurpose, value) is not { } json)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<EmbeddedCheckoutContext>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the context to the response. Over HTTPS the cookie is <c>SameSite=None</c>, <c>Secure</c> and
    /// <c>Partitioned</c>, so a browser keeps it for the platform's frame only.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="context">The context.</param>
    public void Write(HttpContext httpContext, EmbeddedCheckoutContext context)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(context);

        var value = _signedTokenService.Protect(AgenticCommerceStorage.EmbeddedCheckoutContextPurpose, JsonSerializer.Serialize(context), Lifetime);
        var secure = httpContext.Request.IsHttps;

        var options = new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Path = httpContext.Request.PathBase.HasValue ? httpContext.Request.PathBase.Value : "/",
            MaxAge = Lifetime,
            Secure = secure,
            SameSite = secure ? SameSiteMode.None : SameSiteMode.Lax,
        };

        if (secure)
        {
            options.Extensions.Add("Partitioned");
        }

        httpContext.Response.Cookies.Append(AgenticCommerceStorage.EmbeddedCheckoutContextCookieName, value, options);
    }
}

/// <summary>
/// Lets the platform frame the store checkout pages of an embedded checkout's session, and keeps the cookies of those
/// pages in the platform's partition. Every other page keeps the site's anti-framing headers.
/// </summary>
public sealed class EmbeddedCheckoutMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedCheckoutMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware.</param>
    public EmbeddedCheckoutMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Handles a request.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="cookie">The embedded checkout cookie.</param>
    /// <param name="embedded">Who may frame embedded checkout, and where.</param>
    public async Task InvokeAsync(HttpContext httpContext, EmbeddedCheckoutContextCookie cookie, EmbeddedCheckoutPolicy embedded)
    {
        if (httpContext.Request.Cookies.ContainsKey(AgenticCommerceStorage.EmbeddedCheckoutContextCookieName) &&
            cookie.Read(httpContext) is { } context &&
            embedded.IsCheckoutPath(httpContext, context.SessionId))
        {
            var origins = await embedded.GetFramingOriginsAsync(context, httpContext.RequestAborted);

            if (origins.Count > 0)
            {
                // The checkout pages run in a frame inside the embedded checkout's own page, so the store's own origin
                // frames them too.
                EmbeddingPolicy.Apply(httpContext.Response, [EmbeddingPolicy.Self, .. origins], partitionCookies: httpContext.Request.IsHttps);
            }
        }

        await _next(httpContext);
    }
}
