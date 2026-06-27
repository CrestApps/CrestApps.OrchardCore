using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore;

namespace CrestApps.OrchardCore.Telephony.Controllers;

/// <summary>
/// Handles the OAuth 2.0 authorization code flow that connects the current user to the configured
/// telephony provider.
/// </summary>
[Authorize]
public sealed class TelephonyOAuthController : Controller
{
    private const string StateCookieName = "telephony_oauth_state";

    private readonly ITelephonyAuthenticationService _authenticationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtector _protector;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyOAuthController"/> class.
    /// </summary>
    /// <param name="authenticationService">The telephony authentication service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="logger">The logger.</param>
    public TelephonyOAuthController(
        ITelephonyAuthenticationService authenticationService,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<TelephonyOAuthController> logger)
    {
        _authenticationService = authenticationService;
        _authorizationService = authorizationService;
        _protector = dataProtectionProvider.CreateProtector("CrestApps.OrchardCore.Telephony.OAuthState");
        _logger = logger;
    }

    /// <summary>
    /// Starts the OAuth authorization flow by redirecting the user to the provider.
    /// </summary>
    /// <param name="returnUrl">An optional local URL to return to when the flow completes outside a popup.</param>
    /// <returns>A redirect to the provider authorization endpoint, or a completion page on failure.</returns>
    public async Task<IActionResult> Connect(string returnUrl = null)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.UseSoftPhone))
        {
            return Forbid();
        }

        var state = Guid.NewGuid().ToString("N");
        var redirectUri = Url.RouteUrl(TelephonyConstants.RouteNames.OAuthCallback, null, Request.Scheme);

        if (string.IsNullOrEmpty(redirectUri))
        {
            return BuildCompletionPage(false, returnUrl);
        }

        var authorizationRequest = await _authenticationService.GetAuthorizationUrlAsync(redirectUri, state);

        if (authorizationRequest is null || string.IsNullOrEmpty(authorizationRequest.Url))
        {
            return BuildCompletionPage(false, returnUrl);
        }

        var payload = _protector.Protect(string.Join('|', state, redirectUri, returnUrl ?? string.Empty, authorizationRequest.CodeVerifier ?? string.Empty));

        Response.Cookies.Append(StateCookieName, payload, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
        });

        return Redirect(authorizationRequest.Url);
    }

    /// <summary>
    /// Receives the provider redirect, validates the state, and exchanges the authorization code for tokens.
    /// </summary>
    /// <param name="code">The authorization code returned by the provider.</param>
    /// <param name="state">The state value returned by the provider.</param>
    /// <param name="error">The error returned by the provider, when the user denied access.</param>
    /// <returns>A completion page that closes the popup or redirects back.</returns>
    public async Task<IActionResult> Callback(string code = null, string state = null, string error = null)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.UseSoftPhone))
        {
            return Forbid();
        }

        var success = false;
        string returnUrl = null;

        if (Request.Cookies.TryGetValue(StateCookieName, out var payload))
        {
            Response.Cookies.Delete(StateCookieName);

            try
            {
                var parts = _protector.Unprotect(payload).Split('|');

                if (parts.Length == 4)
                {
                    var storedState = parts[0];
                    var redirectUri = parts[1];
                    returnUrl = string.IsNullOrEmpty(parts[2]) ? null : parts[2];
                    var codeVerifier = string.IsNullOrEmpty(parts[3]) ? null : parts[3];

                    if (string.IsNullOrEmpty(error) &&
                        !string.IsNullOrEmpty(code) &&
                        !string.IsNullOrEmpty(storedState) &&
                        string.Equals(storedState, state, StringComparison.Ordinal))
                    {
                        success = await _authenticationService.CompleteAuthorizationAsync(code, redirectUri, codeVerifier);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete the telephony OAuth authorization flow.");
            }
        }

        return BuildCompletionPage(success, returnUrl);
    }

    /// <summary>
    /// Disconnects the current user from the configured provider by removing the stored tokens.
    /// </summary>
    /// <returns>An empty success result.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disconnect()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.UseSoftPhone))
        {
            return Forbid();
        }

        await _authenticationService.DisconnectAsync();

        return Ok();
    }

    private ContentResult BuildCompletionPage(bool success, string returnUrl)
    {
        var safeReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        var successLiteral = success ? "true" : "false";
        var returnUrlJson = JsonSerializer.Serialize(safeReturnUrl);
        var message = success ? "You are connected. You can close this window." : "The connection could not be completed. You can close this window.";

        var html = $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8" /><title>Telephony</title></head>
        <body>
            <p>{{message}}</p>
            <script>
                (function () {
                    var success = {{successLiteral}};
                    try {
                        if (window.opener) {
                            window.opener.postMessage({ type: 'telephony-oauth', success: success }, window.location.origin);
                        }
                    } catch (e) { }
                    if (window.opener) {
                        window.close();
                    } else {
                        window.location.href = {{returnUrlJson}};
                    }
                })();
            </script>
        </body>
        </html>
        """;

        return Content(html, "text/html");
    }
}
