using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce.Controllers;

/// <summary>
/// The calls the UCP embedded checkout page makes to its own store: the checkout's state, which it sends the platform,
/// and the platform's credential, which it has the store check. Both are authorized by the signed token the page was
/// given.
/// </summary>
[Feature(AgenticCommerceConstants.Features.Ucp)]
public sealed class EmbeddedCheckoutController : Controller
{
    private const int MaxBodyBytes = 16 * 1024;

    private readonly EmbeddedCheckoutService _embedded;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedCheckoutController"/> class.
    /// </summary>
    /// <param name="embedded">The embedded checkout service.</param>
    /// <param name="clock">The clock.</param>
    public EmbeddedCheckoutController(EmbeddedCheckoutService embedded, IClock clock)
    {
        _embedded = embedded;
        _clock = clock;
    }

    /// <summary>
    /// Returns the checkout as UCP JSON, as <c>{ "checkout": { ... } }</c>.
    /// </summary>
    /// <param name="token">The embedded checkout's token.</param>
    [AllowAnonymous]
    [HttpGet(AgentSiteUrls.EmbeddedPath + "/{token}/checkout")]
    public async Task<IActionResult> Checkout(string token)
    {
        Response.Headers.CacheControl = "no-store";

        if (await _embedded.FindCheckoutAsync(token, HttpContext.RequestAborted) is not { } checkout ||
            await _embedded.GetCheckoutJsonAsync(checkout, HttpContext.RequestAborted) is not { } json)
        {
            return NotFound();
        }

        return Content(new JsonObject { ["checkout"] = json }.ToJsonString(), "application/json");
    }

    /// <summary>
    /// Checks the credential the platform gave in answer to <c>ec.ready</c> or <c>ec.auth</c>, sent as
    /// <c>{ "credential": "..." }</c>, and answers <c>{ "valid": true, "expires_in": seconds }</c> or
    /// <c>{ "valid": false }</c>.
    /// </summary>
    /// <param name="token">The embedded checkout's token.</param>
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpPost(AgentSiteUrls.EmbeddedPath + "/{token}/auth")]
    public async Task<IActionResult> Auth(string token)
    {
        Response.Headers.CacheControl = "no-store";

        if (await _embedded.FindCheckoutAsync(token, HttpContext.RequestAborted) is not { } checkout)
        {
            return NotFound();
        }

        var body = await RequestBodyReader.ReadAsync(Request, MaxBodyBytes, HttpContext.RequestAborted);

        if (body.IsTooLarge || ReadCredential(body.Body) is not { Length: > 0 } credential ||
            await _embedded.AuthorizeAsync(HttpContext, checkout, credential) is not { } until)
        {
            return Content(new JsonObject { ["valid"] = false }.ToJsonString(), "application/json");
        }

        var expiresIn = Math.Max(0, (int)(until - _clock.UtcNow).TotalSeconds);

        return Content(new JsonObject { ["valid"] = true, ["expires_in"] = expiresIn }.ToJsonString(), "application/json");
    }

    private static string ReadCredential(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(body) is JsonObject json && json["credential"] is JsonValue value && value.TryGetValue<string>(out var credential) ? credential : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
